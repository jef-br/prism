using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Prism.Core;

/// <summary>
/// CLIP ONNX boundary: sole class permitted to access the InferenceSession.
/// Encodes images via a vision ONNX model and compares them against text prompts
/// when the ONNX model supports both image and text encoding in the same file.
///
/// Degrades gracefully at every step:
/// when the model file or tokenizer files are absent, <see cref="IsReady"/> is false
/// and <see cref="ClassifyImage"/> returns an empty array.
/// </summary>
public sealed class ImageClassifier : IDisposable
{
    // ONNX tensor names for the CLIP vision encoder.
    private const string TensorPixelValues = "pixel_values";
    private const string TensorImageEmbeds = "image_embeds";

    // ONNX tensor names for the CLIP text encoder (may be absent in vision-only exports).
    private const string TensorInputIds      = "input_ids";
    private const string TensorAttentionMask = "attention_mask";
    private const string TensorTextEmbeds    = "text_embeds";

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
    /// enabling full zero-shot classification via cosine similarity.
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
            session = new InferenceSession(modelPath);

            IReadOnlyDictionary<string, NodeMetadata> inputMeta  = session.InputMetadata;
            IReadOnlyDictionary<string, NodeMetadata> outputMeta = session.OutputMetadata;

            supportsTextEncoding =
                inputMeta.ContainsKey(TensorInputIds) &&
                inputMeta.ContainsKey(TensorAttentionMask) &&
                outputMeta.ContainsKey(TensorTextEmbeds);

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
    /// Encodes the image against each prompt and returns classification tokens
    /// ranked by descending cosine similarity.
    /// Returns an empty array when <see cref="IsReady"/> is false.
    /// </summary>
    /// <param name="imagePath">Absolute path to the normalized JPEG image.</param>
    /// <param name="prompts">Text prompts to rank against the image.</param>
    public ClassificationToken[] ClassifyImage(string imagePath, string[] prompts)
    {
        if (!IsReady || prompts.Length == 0)
            return [];

        float[] imageEmbedding = EncodeImage(imagePath);
        var tokens = new List<ClassificationToken>(prompts.Length);

        foreach (string prompt in prompts)
        {
            float[] textEmbedding = EncodeText(prompt);
            float similarity = CosineSimilarity(imageEmbedding, textEmbedding);
            tokens.Add(new ClassificationToken { Label = prompt, Confidence = similarity });
        }

        return [.. tokens.OrderByDescending(t => t.Confidence)];
    }

    // ─── Image encoding ───────────────────────────────────────────────────────

    private float[] EncodeImage(string imagePath)
    {
        DenseTensor<float> pixelValues = PreprocessImage(imagePath);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(TensorPixelValues, pixelValues)
        };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs =
            session!.Run(inputs, [TensorImageEmbeds]);

#if DEBUG
        ValidateOutputShape(outputs[0], TensorImageEmbeds, expectedRank: 2, expectedDim0: 1);
#endif

        return [.. outputs[0].AsEnumerable<float>()];
    }

    private static DenseTensor<float> PreprocessImage(string imagePath)
    {
        // Input shape: [1, 3, H, W] — CHW layout, RGB channel order, normalized.
        var tensor = new DenseTensor<float>([1, 3, InputHeight, InputWidth]);

        using Image<Rgb24> image = Image.Load<Rgb24>(imagePath);

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

    // ─── Text encoding ────────────────────────────────────────────────────────

    private float[] EncodeText(string prompt)
    {
        long[] tokenIds = tokenizer!.Encode(prompt);
        long[] attentionMask = tokenIds.Select(id => id == 0L ? 0L : 1L).ToArray();

        int seqLen = tokenIds.Length;
        var inputIdTensor   = new DenseTensor<long>([1, seqLen]);
        var attentionTensor = new DenseTensor<long>([1, seqLen]);

        for (int i = 0; i < seqLen; i++)
        {
            inputIdTensor[0, i]   = tokenIds[i];
            attentionTensor[0, i] = attentionMask[i];
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(TensorInputIds,      inputIdTensor),
            NamedOnnxValue.CreateFromTensor(TensorAttentionMask, attentionTensor)
        };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs =
            session!.Run(inputs, [TensorTextEmbeds]);

        return [.. outputs[0].AsEnumerable<float>()];
    }

    // ─── Similarity ───────────────────────────────────────────────────────────

    private static float CosineSimilarity(float[] a, float[] b)
    {
        int len = Math.Min(a.Length, b.Length);
        if (len == 0) return 0f;

        float dot = 0f, normA = 0f, normB = 0f;
        for (int i = 0; i < len; i++)
        {
            dot   += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        float denom = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denom == 0f ? 0f : dot / denom;
    }

    // ─── Debug contract validation ────────────────────────────────────────────

#if DEBUG
    private static void ValidateOutputShape(
        DisposableNamedOnnxValue output, string name, int expectedRank, long expectedDim0)
    {
        long[] shape = output.AsTensor<float>().Dimensions.ToArray().Select(d => (long)d).ToArray();
        if (shape.Length != expectedRank || shape[0] != expectedDim0)
        {
            throw new InvalidOperationException(
                $"CLIP {name} shape mismatch: expected [{expectedDim0}, D], " +
                $"got [{string.Join(", ", shape)}]");
        }
    }
#endif

    // ─── IDisposable ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        session?.Dispose();
    }
}
