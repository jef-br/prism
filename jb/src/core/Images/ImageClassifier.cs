using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Prism.Core;

/// <summary>
/// CLIP ONNX boundary: sole class permitted to access the InferenceSession.
/// This export is a single combined CLIP graph — image and text branches are entangled, so every
/// <see cref="InferenceSession.Run"/> must supply all three inputs (pixel_values, input_ids,
/// attention_mask) together. <see cref="ClassifyImage"/> encodes the image and every prompt in one Run
/// and returns the model's raw <c>logits_per_image</c> per prompt (≈ logit_scale × cosine). The caller
/// (<see cref="ClassificationService"/>) softmaxes these within each mutually-exclusive feature group to
/// obtain calibrated 0–1 probabilities — the correct CLIP zero-shot computation.
///
/// Degrades gracefully: when the model or tokenizer files are absent, <see cref="IsReady"/> is false
/// and <see cref="ClassifyImage"/> returns an empty array.
/// </summary>
public sealed class ImageClassifier : IDisposable
{
    // ONNX tensor names — inputs.
    private const string TensorPixelValues   = "pixel_values";
    private const string TensorInputIds      = "input_ids";
    private const string TensorAttentionMask = "attention_mask";

    // ONNX tensor name — output. logits_per_image has shape [image_batch, text_batch] = [1, N].
    private const string TensorLogitsPerImage = "logits_per_image";

    // CLIP ViT-B/32 image preprocessing — 224×224 CHW, RGB, ImageNet-style normalization.
    private const int InputWidth  = 224;
    private const int InputHeight = 224;

    // Normalization constants in RGB channel order.
    private static readonly float[] NormMean = [0.48145467f, 0.45782751f, 0.40821072f];
    private static readonly float[] NormStd  = [0.26862955f, 0.26130259f, 0.27577711f];

    private InferenceSession? session;
    private ClipTokenizer? tokenizer;
    private bool supportsTextEncoding;
    private bool disposed;

    /// <summary>
    /// True when the ONNX session is loaded and text encoding is available,
    /// enabling full zero-shot classification.
    /// </summary>
    public bool IsReady => session is not null && tokenizer is not null && supportsTextEncoding;

    /// <summary>
    /// Loads the CLIP ONNX model and BPE tokenizer from the supplied paths.
    /// Does not throw on missing files — sets <see cref="IsReady"/> to false instead.
    /// </summary>
    /// <param name="modelPath">Absolute path to model_uint8.onnx.</param>
    /// <param name="vocabPath">Absolute path to vocab.json.</param>
    /// <param name="mergesPath">Absolute path to merges.txt.</param>
    public void Initialize(string modelPath, string vocabPath, string mergesPath)
    {
        if (!File.Exists(modelPath)) return;

        try
        {
            var opts = new SessionOptions();
            if (OperatingSystem.IsWindows())
                opts.AppendExecutionProvider_DML(0);
            session = new InferenceSession(modelPath, opts);

            IReadOnlyDictionary<string, NodeMetadata> inputMeta  = session.InputMetadata;
            IReadOnlyDictionary<string, NodeMetadata> outputMeta = session.OutputMetadata;

            supportsTextEncoding =
                inputMeta.ContainsKey(TensorInputIds) &&
                inputMeta.ContainsKey(TensorAttentionMask) &&
                outputMeta.ContainsKey(TensorLogitsPerImage);

            if (File.Exists(vocabPath) && File.Exists(mergesPath))
                tokenizer = new ClipTokenizer(vocabPath, mergesPath);
        }
        catch
        {
            // Graceful degradation — classifier reports IsReady = false.
            session?.Dispose();
            session   = null;
            tokenizer = null;
            supportsTextEncoding = false;
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
    public ClassificationToken[] ClassifyImage(Image<Rgba32> image, string[] prompts)
    {
        if (!IsReady || prompts.Length == 0)
            return [];

        // The graph is entangled: one Run must carry the image AND all prompts together.
        DenseTensor<float> pixelValues = PreprocessImage(image);
        (DenseTensor<long> inputIds, DenseTensor<long> attentionMask) = TokenizePrompts(prompts);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(TensorPixelValues,   pixelValues),
            NamedOnnxValue.CreateFromTensor(TensorInputIds,      inputIds),
            NamedOnnxValue.CreateFromTensor(TensorAttentionMask, attentionMask)
        };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs =
            session!.Run(inputs, [TensorLogitsPerImage]);

        // logits_per_image is [1, N] — row 0 holds the image-vs-each-prompt logit.
        Tensor<float> logits = outputs.First(o => o.Name == TensorLogitsPerImage).AsTensor<float>();

        var tokens = new ClassificationToken[prompts.Length];
        for (int i = 0; i < prompts.Length; i++)
            tokens[i] = new ClassificationToken { Label = prompts[i], Confidence = logits[0, i] };

        return tokens;
    }

    //  Image encoding

    private static DenseTensor<float> PreprocessImage(Image<Rgba32> sourceImage)
    {
        // Input shape: [1, 3, H, W] — CHW layout, RGB channel order, normalized.
        // Clone as Rgb24 to avoid mutating the shared image (resize changes pixel dimensions).
        var tensor = new DenseTensor<float>([1, 3, InputHeight, InputWidth]);

        using Image<Rgb24> image = sourceImage.CloneAs<Rgb24>();

        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(InputWidth, InputHeight),
            Mode = ResizeMode.Crop
        }));

        for (int y = 0; y < InputHeight; y++)
        {
            for (int x = 0; x < InputWidth; x++)
            {
                Rgb24 px = image[x, y];
                tensor[0, 0, y, x] = (px.R / 255f - NormMean[0]) / NormStd[0];
                tensor[0, 1, y, x] = (px.G / 255f - NormMean[1]) / NormStd[1];
                tensor[0, 2, y, x] = (px.B / 255f - NormMean[2]) / NormStd[2];
            }
        }

        return tensor;
    }

    //  Text encoding 

    /// <summary>
    /// Tokenizes every prompt to the CLIP context length (77) and stacks them into batched
    /// [N, 77] input_ids and attention_mask tensors. Padding tokens (id 0) get an attention mask of 0.
    /// </summary>
    private (DenseTensor<long> InputIds, DenseTensor<long> AttentionMask) TokenizePrompts(string[] prompts)
    {
        long[][] encoded = prompts.Select(p => tokenizer!.Encode(p)).ToArray();
        int rows = encoded.Length;
        int seqLen = encoded[0].Length;

        var inputIds      = new DenseTensor<long>([rows, seqLen]);
        var attentionMask = new DenseTensor<long>([rows, seqLen]);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < seqLen; c++)
            {
                long id = encoded[r][c];
                inputIds[r, c]      = id;
                attentionMask[r, c] = id == 0L ? 0L : 1L;
            }
        }

        return (inputIds, attentionMask);
    }

    //  IDisposable 

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        session?.Dispose();
    }
}
