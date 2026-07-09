using Prism.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Prism.Core.Tests.Analyzers;

/// <summary>
/// Smoke tests for the YOLO26 detector boundary: the model must load from its deployed location
/// (resolved by PrismConfigLocator) and produce parseable, normalized detections without throwing.
/// </summary>
public class YoloDetectorTests
{
    // Resolve the model exactly as production does: beside Prism_Config.json, then the
    // PRISM_ONNX_MODEL_DIR override (how CI deploys it), then the git-ignored source-tree copy
    // (dev convenience). Source-tree-only resolution fails on a fresh CI checkout, where the
    // 37 MB model is not committed.
    private static string? FindModel()
    {
        return PrismConfigLocator.FindModelAsset("Services/Matching/Analyzers/ONNX/yolo26s.onnx");
    }

    [Fact]
    public void Initialize_WithBundledModel_IsReady()
    {
        string? modelPath = FindModel();
        Assert.NotNull(modelPath);

        using var detector = new YoloDetector();
        detector.Initialize(modelPath!);
        Assert.True(detector.IsReady);
    }

    [Fact]
    public void Detect_OnSyntheticImage_ReturnsWithoutThrowing()
    {
        string? modelPath = FindModel();
        Assert.NotNull(modelPath);

        using var detector = new YoloDetector();
        detector.Initialize(modelPath!);

        using var image = new Image<Rgba32>(320, 480, new Rgba32(200, 60, 60));
        IReadOnlyList<YoloDetection> detections = detector.Detect(image, new YoloAnalyzerConfig());

        // A flat synthetic image should produce few or no detections — the assertion is that the
        // output tensor parsed and every surviving box is normalized and ordered.
        foreach (YoloDetection d in detections)
        {
            Assert.InRange(d.X1, 0f, 1f);
            Assert.InRange(d.Y2, 0f, 1f);
            Assert.True(d.X2 >= d.X1);
            Assert.True(d.Y2 >= d.Y1);
            Assert.True(d.Confidence >= 0.40f);
        }
    }

    [Fact]
    public void Initialize_WithMissingFile_IsNotReadyAndDetectReturnsEmpty()
    {
        using var detector = new YoloDetector();
        detector.Initialize(Path.Combine(Path.GetTempPath(), "does-not-exist.onnx"));
        Assert.False(detector.IsReady);

        using var image = new Image<Rgba32>(64, 64);
        Assert.Empty(detector.Detect(image, new YoloAnalyzerConfig()));
    }
}
