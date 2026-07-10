using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Prism.Services.Matching;

/// <summary>
/// YOLO26 ONNX boundary: sole class permitted to access the detector InferenceSession.
/// Detects the 80 COCO object classes in a product image and returns normalized boxes —
/// the subject box, person detections, and object counts feeding the analyzer chain.
/// One shared, process-wide instance (bc expensive to load); every <see cref="InferenceSession.Run"/> call is serialized by <see cref="RunLock"/>
/// Concurrent jobs (see PrismJobCoordinator's MaxConcurrentJobs) can share the session safely.
/// </summary>
public sealed class YoloDetector : IDisposable {
    // Tensor names for the ultralytics YOLO26 end-to-end ONNX export.
    private const string TensorImages = "images";
    private const string TensorOutput0 = "output0";

    // YOLO26 preprocessing — 640×640 CHW, RGB, pixel/255 normalization, no letterboxing
    // (plain resize; boxes are normalized back against the resized frame so aspect distortion cancels).
    private const int InputWidth = 640;
    private const int InputHeight = 640;

    private InferenceSession? session;
    private string inputName = TensorImages;
    private string outputName = TensorOutput0;
    private bool disposed;

    private static YoloDetector? shared;
    private static readonly object SharedLock = new();

    // Guards every session.Run() call — the DML execution provider does not support
    // concurrent InferenceSession.Run calls on the same session.
    private static readonly object RunLock = new();

    /// <summary>True when the ONNX session is loaded and detection is available.</summary>
    public bool IsReady => session is not null;

    /// <summary>
    /// Returns the process-wide shared detector, initializing it from <paramref name="modelPath"/>
    /// on first use. Later calls ignore the path.
    /// </summary>
    public static YoloDetector GetShared( string modelPath ) {
        if (shared is not null) return shared;
        lock (SharedLock) {
            if (shared is null) {
                YoloDetector detector = new();
                detector.Initialize(modelPath);
                shared = detector;
            }
        }
        return shared;
    }

    /// <summary>
    /// Loads the YOLO26 ONNX model. Does not throw on a missing file — sets
    /// <see cref="IsReady"/> to false instead; startup validation guarantees presence in production.
    /// </summary>
    public void Initialize( string modelPath ) {
        if (!File.Exists(modelPath)) return;

        try {
            session = new InferenceSession(modelPath);
            inputName = session.InputMetadata.Keys.FirstOrDefault() ?? TensorImages;
            outputName = session.OutputMetadata.Keys.FirstOrDefault() ?? TensorOutput0;
        } catch {
            session?.Dispose();
            session = null;
        }
    }

    /// <summary>
    /// Runs detection on the pre-loaded image and returns NMS-filtered detections with
    /// normalized [0,1] boxes, strongest first. Empty when the session is unavailable.
    /// </summary>
    public IReadOnlyList<YoloDetection> Detect( Image<Rgba32> image, YoloAnalyzerConfig cfg ) {
        if (!IsReady) return [];

        DenseTensor<float> input = PreprocessImage(image);
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, input) };

        lock (RunLock) {
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs =
                session!.Run(inputs, [outputName]);

            Tensor<float> prediction = outputs.First(o => o.Name == outputName).AsTensor<float>();
            return Postprocess(prediction, cfg);
        }
    }

    // Resizes to 640×640 (RGB, /255) and lays pixels out CHW. The source image is not mutated.
    private static DenseTensor<float> PreprocessImage( Image<Rgba32> image ) {
        var tensor = new DenseTensor<float>([1, 3, InputHeight, InputWidth]);

        using Image<Rgba32> resized = image.Clone(ctx => ctx.Resize(InputWidth, InputHeight));
        resized.ProcessPixelRows(accessor => {
            for (int y = 0; y < InputHeight; y++) {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < InputWidth; x++) {
                    Rgba32 p = row[x];
                    tensor[0, 0, y, x] = p.R / 255f;
                    tensor[0, 1, y, x] = p.G / 255f;
                    tensor[0, 2, y, x] = p.B / 255f;
                }
            }
        });

        return tensor;
    }

    // YOLO26 exports an end-to-end (NMS-free) head: output is [1, 300, 6], each row
    // [x1, y1, x2, y2, score, classId] with box coords in 640-input pixel space and detections
    // already NMS-filtered and ranked. Unused slots are zero-padded (score 0). We threshold on
    // confidence, normalize boxes to [0,1] of the resized frame, and cap at MaxDetections.
    private static IReadOnlyList<YoloDetection> Postprocess( Tensor<float> prediction, YoloAnalyzerConfig cfg ) {
        int detections = prediction.Dimensions[1];

        List<YoloDetection> kept = [];
        for (int d = 0; d < detections; d++) {
            float score = prediction[0, d, 4];
            if (score < cfg.ConfidenceThreshold) continue;

            int classId = (int) prediction[0, d, 5];

            // Box coords are x1,y1,x2,y2 in 640-space; normalize to [0,1] of the resized frame.
            float x1 = prediction[0, d, 0] / InputWidth;
            float y1 = prediction[0, d, 1] / InputHeight;
            float x2 = prediction[0, d, 2] / InputWidth;
            float y2 = prediction[0, d, 3] / InputHeight;

            string name = classId >= 0 && classId < YoloClassNames.Names.Length ? YoloClassNames.Names[classId] : $"class-{classId}";
            kept.Add(new YoloDetection(classId, name, score,
                Math.Clamp(x1, 0f, 1f), Math.Clamp(y1, 0f, 1f),
                Math.Clamp(x2, 0f, 1f), Math.Clamp(y2, 0f, 1f)));
        }

        return [.. kept.OrderByDescending(d => d.Confidence).Take(cfg.MaxDetections)];
    }

    /// <inheritdoc/>
    public void Dispose() {
        if (disposed) return;
        disposed = true;
        session?.Dispose();
        session = null;
    }
}