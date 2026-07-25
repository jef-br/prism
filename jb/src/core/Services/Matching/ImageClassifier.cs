using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Prism.Services.Matching;

/// <summary>
/// CLIP ONNX boundary: sole class permitted to access the InferenceSession.
/// This export is a single combined CLIP graph — image and text branches are entangled, so every
/// <see cref="InferenceSession.Run"/> must supply all three inputs (pixel_values, input_ids,
/// attention_mask) together. <see cref="ClassifyImage"/> encodes the image and every prompt in one Run
/// and returns the model's raw <c>logits_per_image</c> per prompt (≈ logit_scale × cosine). The caller
/// (<see cref="ClassificationService"/>) softmaxes these within each mutually-exclusive feature group to
/// obtain calibrated 0–1 probabilities — the correct CLIP zero-shot computation.
///
/// One shared, process-wide instance (the session is expensive to load) — see <see cref="GetShared"/>.
/// Every <see cref="InferenceSession.Run"/> call is serialized by an internal lock so concurrent jobs
/// (see PrismJobCoordinator's MaxConcurrentJobs) can share the session safely; callers do not need to
/// synchronize access themselves.
///
/// Degrades gracefully: when the model or tokenizer files are absent, <see cref="IsReady"/> is false
/// and <see cref="ClassifyImage"/> returns an empty array.
/// </summary>
public sealed class ImageClassifier : IDisposable {
    // ONNX tensor names — inputs.
    private const string TensorPixelValues = "pixel_values";
    private const string TensorInputIds = "input_ids";
    private const string TensorAttentionMask = "attention_mask";

    // ONNX tensor name — output. logits_per_image has shape [image_batch, text_batch] = [1, N].
    private const string TensorLogitsPerImage = "logits_per_image";

    // CLIP ViT-B/32 image preprocessing — 224×224 CHW, RGB, ImageNet-style normalization.
    private const int InputWidth = 224;
    private const int InputHeight = 224;

    // Normalization constants in RGB channel order.
    private static readonly float[] NormMean = [0.48145467f, 0.45782751f, 0.40821072f];
    private static readonly float[] NormStd = [0.26862955f, 0.26130259f, 0.27577711f];

    private InferenceSession? session;
    private ClipTokenizer? tokenizer;
    private bool supportsTextEncoding;
    private bool supportsImageBatch;
    private bool disposed;

    // Tokenized prompt tensors cached by prompt-array reference — the prompt catalogue is static per
    // process, so tokenization runs once instead of once per image. Only touched from within RunLock
    // (both callers of GetPromptTensors hold it), so no separate synchronisation is needed here.
    private string[]? cachedPrompts;
    private DenseTensor<long>? cachedInputIds;
    private DenseTensor<long>? cachedAttentionMask;

    private static ImageClassifier? shared;
    private static readonly object SharedLock = new();

    // Guards every session.Run() call (and the prompt-tensor cache above) — the DML execution
    // provider does not support concurrent InferenceSession.Run calls on the same session.
    private static readonly object RunLock = new();

    /// <summary>
    /// Returns the process-wide shared classifier, initializing it from the supplied model/tokenizer
    /// paths on first use. Later calls ignore the paths and return the same instance.
    /// </summary>
    public static ImageClassifier GetShared(string modelPath, string vocabPath, string mergesPath) {
        if (shared is not null) return shared;
        lock (SharedLock) {
            if (shared is null) {
                ImageClassifier classifier = new();
                classifier.Initialize(modelPath, vocabPath, mergesPath);
                shared = classifier;
            }
        }
        return shared;
    }

    /// <summary>
    /// True when the ONNX session is loaded and text encoding is available,
    /// enabling full zero-shot classification.
    /// </summary>
    public bool IsReady => this.session is not null && this.tokenizer is not null && this.supportsTextEncoding;

    /// <summary>
    /// True when the model's pixel_values batch dimension is dynamic, so multiple images can share
    /// one <see cref="InferenceSession.Run"/> — amortizing the text branch across the batch.
    /// </summary>
    public bool SupportsImageBatch => this.IsReady && this.supportsImageBatch;

    /// <summary>
    /// Loads the CLIP ONNX model and BPE tokenizer from the supplied paths.
    /// Does not throw on missing files — sets <see cref="IsReady"/> to false instead; startup
    /// validation (ClassificationService.ResolveClassifierPaths) guarantees presence in production.
    /// A model or tokenizer file that is present but fails to load throws
    /// <see cref="PrismConfigurationException"/> — corrupt assets never degrade silently (T-4110).
    /// </summary>
    /// <param name="modelPath">Absolute path to model_uint8.onnx.</param>
    /// <param name="vocabPath">Absolute path to vocab.json.</param>
    /// <param name="mergesPath">Absolute path to merges.txt.</param>
    public void Initialize(string modelPath, string vocabPath, string mergesPath) {
        if (!File.Exists(modelPath)) return;

        try {
            this.session = OnnxSessionFactory.Create(modelPath);

            IReadOnlyDictionary<string, NodeMetadata> inputMeta = this.session.InputMetadata;
            IReadOnlyDictionary<string, NodeMetadata> outputMeta = this.session.OutputMetadata;

            this.supportsTextEncoding =
                inputMeta.ContainsKey(TensorInputIds) &&
                inputMeta.ContainsKey(TensorAttentionMask) &&
                outputMeta.ContainsKey(TensorLogitsPerImage);

            // Dynamic batch dims are reported as <= 0; a fixed 1 forces one image per Run.
            this.supportsImageBatch =
                inputMeta.TryGetValue(TensorPixelValues, out NodeMetadata? pixelMeta) &&
                pixelMeta.Dimensions.Length == 4 &&
                pixelMeta.Dimensions[0] <= 0;

            if (File.Exists(vocabPath) && File.Exists(mergesPath))
                this.tokenizer = new ClipTokenizer(vocabPath, mergesPath);
        }
        catch (Exception loadException) {
            this.session?.Dispose();
            this.session = null;
            this.tokenizer = null;
            this.supportsTextEncoding = false;
            throw new PrismConfigurationException(
                $"CLIP ONNX assets at '{modelPath}' are present but failed to load — a file is " +
                $"corrupt, truncated, or an incompatible export: {loadException.Message}", loadException);
        }
    }

    /// <summary>
    /// Encodes the image against every prompt in a single combined inference pass and returns one token
    /// per prompt carrying the raw <c>logits_per_image</c> value in <see cref="ClassificationToken.Confidence"/>.
    /// Tokens are returned in prompt order (not ranked) — the caller groups and softmaxes them per feature.
    /// Returns an empty array when <see cref="IsReady"/> is false.
    /// </summary>
    /// <param name="image">Pre-loaded normalized image (Rgba32). Not mutated by this method.</param>
    /// <param name="prompts">Text prompts to score against the image.</param>
    public ClassificationToken[] ClassifyImage(Image<Rgba32> image, string[] prompts) {
        if (!this.IsReady || prompts.Length == 0)
            return [];

        // The graph is entangled: one Run must carry the image AND all prompts together.
        DenseTensor<float> pixelValues = PreprocessImage(image);

        lock (RunLock) {
            (DenseTensor<long> inputIds, DenseTensor<long> attentionMask) = this.GetPromptTensors(prompts);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(TensorPixelValues,   pixelValues),
                NamedOnnxValue.CreateFromTensor(TensorInputIds,      inputIds),
                NamedOnnxValue.CreateFromTensor(TensorAttentionMask, attentionMask)
            };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs =
                this.session!.Run(inputs, [TensorLogitsPerImage]);

            // logits_per_image is [1, N] — row 0 holds the image-vs-each-prompt logit.
            Tensor<float> logits = outputs.First(o => o.Name == TensorLogitsPerImage).AsTensor<float>();

            var tokens = new ClassificationToken[prompts.Length];
            for (int i = 0; i < prompts.Length; i++)
                tokens[i] = new ClassificationToken { Label = prompts[i], Confidence = logits[0, i] };

            return tokens;
        }
    }

    /// <summary>
    /// Encodes several images against every prompt. When the model's batch dimension is dynamic all
    /// images share one Run ([B, 3, H, W] → logits [B, N]) so the text branch is computed once per
    /// batch; otherwise falls back to one Run per image. Returns one token array per image, in input
    /// order. Empty arrays when <see cref="IsReady"/> is false.
    /// </summary>
    /// <param name="images">Pre-loaded normalized images (Rgba32). Not mutated by this method.</param>
    /// <param name="prompts">Text prompts to score against every image.</param>
    public ClassificationToken[][] ClassifyImages(IReadOnlyList<Image<Rgba32>> images, string[] prompts) {
        if (!this.IsReady || prompts.Length == 0 || images.Count == 0)
            return [.. images.Select(_ => Array.Empty<ClassificationToken>())];

        if (images.Count == 1 || !this.SupportsImageBatch)
            return [.. images.Select(image => this.ClassifyImage(image, prompts))];

        var pixelValues = new DenseTensor<float>([images.Count, 3, InputHeight, InputWidth]);
        for (int b = 0; b < images.Count; b++)
            PreprocessImageInto(images[b], pixelValues, b);

        lock (RunLock) {
            (DenseTensor<long> inputIds, DenseTensor<long> attentionMask) = this.GetPromptTensors(prompts);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(TensorPixelValues,   pixelValues),
                NamedOnnxValue.CreateFromTensor(TensorInputIds,      inputIds),
                NamedOnnxValue.CreateFromTensor(TensorAttentionMask, attentionMask)
            };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs =
                this.session!.Run(inputs, [TensorLogitsPerImage]);

            Tensor<float> logits = outputs.First(o => o.Name == TensorLogitsPerImage).AsTensor<float>();

            var results = new ClassificationToken[images.Count][];
            for (int b = 0; b < images.Count; b++) {
                var tokens = new ClassificationToken[prompts.Length];
                for (int i = 0; i < prompts.Length; i++)
                    tokens[i] = new ClassificationToken { Label = prompts[i], Confidence = logits[b, i] };
                results[b] = tokens;
            }

            return results;
        }
    }

    //  Image encoding

    private static DenseTensor<float> PreprocessImage(Image<Rgba32> sourceImage) {
        // Input shape: [1, 3, H, W] — CHW layout, RGB channel order, normalized.
        var tensor = new DenseTensor<float>([1, 3, InputHeight, InputWidth]);
        PreprocessImageInto(sourceImage, tensor, 0);
        return tensor;
    }

    /// <summary>
    /// Writes one image into batch slot <paramref name="imageIndex"/> of a [B, 3, H, W] tensor.
    /// Clones as Rgb24 to avoid mutating the shared image (resize changes pixel dimensions), then
    /// fills the slot via row spans and direct flat-buffer writes (index = c·H·W + y·W + x).
    /// </summary>
    private static void PreprocessImageInto(Image<Rgba32> sourceImage, DenseTensor<float> tensor, int imageIndex) {
        using Image<Rgb24> image = sourceImage.CloneAs<Rgb24>();

        image.Mutate(ctx => ctx.Resize(new ResizeOptions {
            Size = new Size(InputWidth, InputHeight),
            Mode = ResizeMode.Crop
        }));

        image.ProcessPixelRows(accessor => {
            const int plane = InputHeight * InputWidth;
            Span<float> data = tensor.Buffer.Span.Slice(imageIndex * 3 * plane, 3 * plane);

            for (int y = 0; y < accessor.Height; y++) {
                Span<Rgb24> row = accessor.GetRowSpan(y);
                int rowOffset = y * InputWidth;

                for (int x = 0; x < row.Length; x++) {
                    Rgb24 px = row[x];
                    int idx = rowOffset + x;
                    // Channel index 2 = B (RGB order, matching NormMean/NormStd); 255 is the max
                    // 8-bit channel value — fixed layout, never changes.
#pragma warning disable S109
                    data[idx] = (px.R / 255f - NormMean[0]) / NormStd[0];
                    data[plane + idx] = (px.G / 255f - NormMean[1]) / NormStd[1];
                    data[2 * plane + idx] = (px.B / 255f - NormMean[2]) / NormStd[2];
#pragma warning restore S109
                }
            }
        });
    }

    //  Text encoding

    /// <summary>
    /// Returns the tokenized tensors for <paramref name="prompts"/>, re-tokenizing only when the
    /// prompt array instance changes (the catalogue hands out one cached instance per process).
    /// </summary>
    private (DenseTensor<long> InputIds, DenseTensor<long> AttentionMask) GetPromptTensors(string[] prompts) {
        if (!ReferenceEquals(this.cachedPrompts, prompts)) {
            (this.cachedInputIds, this.cachedAttentionMask) = this.TokenizePrompts(prompts);
            this.cachedPrompts = prompts;
        }

        return (this.cachedInputIds!, this.cachedAttentionMask!);
    }

    /// <summary>
    /// Tokenizes every prompt to the CLIP context length (77) and stacks them into batched
    /// [N, 77] input_ids and attention_mask tensors. Padding tokens (id 0) get an attention mask of 0.
    /// </summary>
    private (DenseTensor<long> InputIds, DenseTensor<long> AttentionMask) TokenizePrompts(string[] prompts) {
        long[][] encoded = prompts.Select(p => this.tokenizer!.Encode(p)).ToArray();
        int rows = encoded.Length;
        int seqLen = encoded[0].Length;

        var inputIds = new DenseTensor<long>([rows, seqLen]);
        var attentionMask = new DenseTensor<long>([rows, seqLen]);

        for (int r = 0; r < rows; r++) {
            for (int c = 0; c < seqLen; c++) {
                long id = encoded[r][c];
                inputIds[r, c] = id;
                attentionMask[r, c] = id == 0L ? 0L : 1L;
            }
        }

        return (inputIds, attentionMask);
    }

    //  IDisposable 

    /// <inheritdoc/>
    public void Dispose() {
        if (this.disposed) return;
        this.disposed = true;
        this.session?.Dispose();
    }
}
