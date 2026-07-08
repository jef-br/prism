using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Prism.Core;

/// <summary>
/// YOLOv8n ONNX boundary: sole class permitted to access the detector InferenceSession.
/// Detects the 80 COCO object classes in a product image and returns normalized boxes —
/// the subject box, person detections, and object counts feeding the analyzer chain.
/// One shared instance per process (the session is expensive); Run calls are serialized
/// by the sequential refinement loop.
/// </summary>
public sealed class YoloDetector : IDisposable
{
    // Tensor names for the standard ultralytics YOLOv8 ONNX export.
    private const string TensorImages  = "images";
    private const string TensorOutput0 = "output0";

    // YOLOv8n preprocessing — 640×640 CHW, RGB, pixel/255 normalization, no letterboxing
    // (plain resize; boxes are normalized back against the resized frame so aspect distortion cancels).
    private const int InputWidth  = 640;
    private const int InputHeight = 640;

    private InferenceSession? session;
    private string inputName  = TensorImages;
    private string outputName = TensorOutput0;
    private bool disposed;

    private static YoloDetector? shared;
    private static readonly object SharedLock = new();

    /// <summary>True when the ONNX session is loaded and detection is available.</summary>
    public bool IsReady => session is not null;

    /// <summary>
    /// Returns the process-wide shared detector, initializing it from <paramref name="modelPath"/>
    /// on first use. Later calls ignore the path.
    /// </summary>
    public static YoloDetector GetShared(string modelPath)
    {
        if (shared is not null) return shared;
        lock (SharedLock)
        {
            if (shared is null)
            {
                YoloDetector detector = new();
                detector.Initialize(modelPath);
                shared = detector;
            }
        }
        return shared;
    }

    /// <summary>
    /// Loads the YOLOv8n ONNX model. Does not throw on a missing file — sets
    /// <see cref="IsReady"/> to false instead; startup validation guarantees presence in production.
    /// </summary>
    public void Initialize(string modelPath)
    {
        if (!File.Exists(modelPath)) return;

        try
        {
            session = new InferenceSession(modelPath);
            inputName  = session.InputMetadata.Keys.FirstOrDefault() ?? TensorImages;
            outputName = session.OutputMetadata.Keys.FirstOrDefault() ?? TensorOutput0;
        }
        catch
        {
            session?.Dispose();
            session = null;
        }
    }

    /// <summary>
    /// Runs detection on the pre-loaded image and returns NMS-filtered detections with
    /// normalized [0,1] boxes, strongest first. Empty when the session is unavailable.
    /// </summary>
    public IReadOnlyList<YoloDetection> Detect(Image<Rgba32> image, YoloAnalyzerConfig cfg)
    {
        if (!IsReady) return [];

        DenseTensor<float> input = PreprocessImage(image);
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, input) };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs =
            session!.Run(inputs, [outputName]);

        Tensor<float> prediction = outputs.First(o => o.Name == outputName).AsTensor<float>();
        return Postprocess(prediction, cfg);
    }

    // Resizes to 640×640 (RGB, /255) and lays pixels out CHW. The source image is not mutated.
    private static DenseTensor<float> PreprocessImage(Image<Rgba32> image)
    {
        var tensor = new DenseTensor<float>([1, 3, InputHeight, InputWidth]);

        using Image<Rgba32> resized = image.Clone(ctx => ctx.Resize(InputWidth, InputHeight));
        resized.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < InputHeight; y++)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < InputWidth; x++)
                {
                    Rgba32 p = row[x];
                    tensor[0, 0, y, x] = p.R / 255f;
                    tensor[0, 1, y, x] = p.G / 255f;
                    tensor[0, 2, y, x] = p.B / 255f;
                }
            }
        });

        return tensor;
    }

    // Output is [1, 84, N] (4 box coords + 80 class scores per anchor, N ≈ 8400). Per anchor the
    // best class is taken; survivors above the confidence threshold go through class-wise NMS.
    private static IReadOnlyList<YoloDetection> Postprocess(Tensor<float> prediction, YoloAnalyzerConfig cfg)
    {
        int anchors = prediction.Dimensions[2];
        int classes = prediction.Dimensions[1] - 4;

        List<YoloDetection> candidates = [];
        for (int a = 0; a < anchors; a++)
        {
            int bestClass = -1;
            float bestScore = 0f;
            for (int c = 0; c < classes; c++)
            {
                float score = prediction[0, 4 + c, a];
                if (score > bestScore) { bestScore = score; bestClass = c; }
            }
            if (bestClass < 0 || bestScore < cfg.ConfidenceThreshold) continue;

            // Box coords are center-x, center-y, width, height in 640-space; normalize to [0,1].
            float cx = prediction[0, 0, a] / InputWidth;
            float cy = prediction[0, 1, a] / InputHeight;
            float w  = prediction[0, 2, a] / InputWidth;
            float h  = prediction[0, 3, a] / InputHeight;

            string name = bestClass < YoloClassNames.Names.Length ? YoloClassNames.Names[bestClass] : $"class-{bestClass}";
            candidates.Add(new YoloDetection(bestClass, name, bestScore,
                Math.Clamp(cx - w / 2f, 0f, 1f), Math.Clamp(cy - h / 2f, 0f, 1f),
                Math.Clamp(cx + w / 2f, 0f, 1f), Math.Clamp(cy + h / 2f, 0f, 1f)));
        }

        return ApplyClassWiseNms(candidates, cfg);
    }

    // Standard greedy NMS per class: keep the strongest box, drop same-class boxes overlapping
    // it above the IoU threshold, repeat.
    private static IReadOnlyList<YoloDetection> ApplyClassWiseNms(List<YoloDetection> candidates, YoloAnalyzerConfig cfg)
    {
        List<YoloDetection> kept = [];
        foreach (IGrouping<int, YoloDetection> group in candidates.GroupBy(d => d.ClassId))
        {
            List<YoloDetection> remaining = [.. group.OrderByDescending(d => d.Confidence)];
            while (remaining.Count > 0)
            {
                YoloDetection best = remaining[0];
                kept.Add(best);
                remaining.RemoveAll(d => IntersectionOverUnion(best, d) > cfg.NmsIouThreshold);
            }
        }

        return [.. kept.OrderByDescending(d => d.Confidence).Take(cfg.MaxDetections)];
    }

    private static float IntersectionOverUnion(YoloDetection a, YoloDetection b)
    {
        float ix = MathF.Max(0f, MathF.Min(a.X2, b.X2) - MathF.Max(a.X1, b.X1));
        float iy = MathF.Max(0f, MathF.Min(a.Y2, b.Y2) - MathF.Max(a.Y1, b.Y1));
        float inter = ix * iy;
        float union = a.Area + b.Area - inter;
        return union <= 0f ? 0f : inter / union;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        session?.Dispose();
        session = null;
    }
}
