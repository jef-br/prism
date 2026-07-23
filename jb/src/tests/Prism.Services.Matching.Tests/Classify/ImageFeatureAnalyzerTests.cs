using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace PrismCoreTests.Classify;

/// <summary>
/// Unit tests for <see cref="ImageFeatureAnalyzer"/>: CPU-only feature extraction.
/// Synthetic JPEG (and PNG) images are created in a per-test temp directory and deleted on dispose.
/// </summary>
public sealed class ImageFeatureAnalyzerTests : IDisposable
{
    // 400×400 gives strip = max(2, int(400 * 0.08)) = 32 px, corner region = 40 px.
    private const int W = 400;
    private const int H = 400;
    private const int StripPx = 32;    // 8 % of 400
    private const int CornerPx = 40;   // 10 % of 400

    private readonly string _tempDir;

    public ImageFeatureAnalyzerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "prism-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort cleanup */ }
    }

    //  Aspect ratio 

    [Fact]
    public void Analyze_WideImage_RecordsCorrectAspectRatio()
    {
        string path = CreateJpeg("wide", 800, 400, White);
        var snap = Analyze(path);
        Assert.Equal("2.0000", snap.GetValue("aspect-ratio"));
    }

    [Fact]
    public void Analyze_TallImage_RecordsCorrectAspectRatio()
    {
        string path = CreateJpeg("tall", 400, 800, White);
        var snap = Analyze(path);
        Assert.Equal("0.5000", snap.GetValue("aspect-ratio"));
    }

    [Fact]
    public void Analyze_SquareImage_RecordsAspectRatioOne()
    {
        string path = CreateJpeg("square", 500, 500, White);
        var snap = Analyze(path);
        Assert.Equal("1.0000", snap.GetValue("aspect-ratio"));
    }

    [Fact]
    public void Analyze_PortraitImage_RecordsCorrectAspectRatio()
    {
        string path = CreateJpeg("portrait", 400, 600, White);
        var snap = Analyze(path);
        Assert.Equal("0.6667", snap.GetValue("aspect-ratio"));
    }

    //  Background detection 

    [Fact]
    public void Analyze_SolidWhiteBackground_WhiteBackgroundTrue()
    {
        string path = CreateJpeg("white", W, H, White);
        var snap = Analyze(path);
        Assert.Equal("true", snap.GetValue("white-background"));
    }

    [Fact]
    public void Analyze_SolidGrayBackground_WhiteBackgroundFalse()
    {
        // RGB(128, 128, 128) normalized ≈ 0.502 — below the 0.90 threshold.
        string path = CreateJpeg("gray", W, H, (img) => Fill(img, 0, 0, W, H, new Rgba32(128, 128, 128)));
        var snap = Analyze(path);
        Assert.Equal("false", snap.GetValue("white-background"));
    }

    [Fact]
    public void Analyze_SolidBackground_ReportsSolidColor()
    {
        // Low corner variance → SOLIDCOLOR.
        string path = CreateJpeg("solid", W, H, White);
        var snap = Analyze(path);
        Assert.Equal("SOLIDCOLOR", snap.GetValue("background-type"));
    }

    [Fact]
    public void Analyze_HighVarianceCorners_LifestyleBackgroundTrue()
    {
        // Four vivid corner colors produce corner variance >> 0.040 → lifestyle-background.
        string path = CreateJpeg("lifestyle", W, H, img =>
        {
            White(img);
            Fill(img, 0,         0,         CornerPx, CornerPx, new Rgba32(255,   0,   0)); // TL red
            Fill(img, W-CornerPx, 0,        CornerPx, CornerPx, new Rgba32(  0, 255,   0)); // TR green
            Fill(img, 0,         H-CornerPx, CornerPx, CornerPx, new Rgba32(  0,   0, 255)); // BL blue
            Fill(img, W-CornerPx, H-CornerPx, CornerPx, CornerPx, new Rgba32(255, 255,   0)); // BR yellow
        });
        var snap = Analyze(path);
        Assert.Equal("true", snap.GetValue("lifestyle-background"));
    }

    [Fact]
    public void Analyze_TransparentPixels_TransparentBackgroundTrue()
    {
        // PNG with fully transparent corners.
        string path = CreatePng("alpha", W, H, img =>
        {
            White(img);
            // Transparent corners — A=0.
            Fill(img, 0,         0,         CornerPx, CornerPx, new Rgba32(0, 0, 0, 0));
            Fill(img, W-CornerPx, 0,        CornerPx, CornerPx, new Rgba32(0, 0, 0, 0));
            Fill(img, 0,         H-CornerPx, CornerPx, CornerPx, new Rgba32(0, 0, 0, 0));
            Fill(img, W-CornerPx, H-CornerPx, CornerPx, CornerPx, new Rgba32(0, 0, 0, 0));
        });
        var snap = Analyze(path);
        Assert.Equal("true", snap.GetValue("transparent-background"));
        Assert.Equal("true", snap.GetValue("clipping-path"));
    }

    [Fact]
    public void Analyze_OpaqueJpeg_TransparentBackgroundFalse()
    {
        string path = CreateJpeg("opaque", W, H, White);
        var snap = Analyze(path);
        Assert.Equal("false", snap.GetValue("transparent-background"));
        Assert.Equal("false", snap.GetValue("clipping-path"));
    }

    //  Border intersections + occlusion level 

    // Pattern for intersection tests: solid white image with a contrasting dark
    // rectangle. The dark rectangle's position controls which borders are touched.

    [Fact]
    public void Analyze_ProductFullyInFrame_ZeroIntersectionsAndOcclusionFullProduct()
    {
        // Dark rect inset well inside the strip boundary — touches no edge strip.
        string path = CreateJpeg("inframe", W, H, img =>
        {
            White(img);
            Fill(img, 80, 80, 240, 240, Dark);
        });
        var snap = Analyze(path);
        Assert.Equal("0",            snap.GetValue("intersection-count"));
        Assert.Equal("true",         snap.GetValue("fully-in-frame"));
        Assert.Equal("full-product", snap.GetValue("occlusion-level"));
        Assert.Equal("false",        snap.GetValue("intersects-top"));
        Assert.Equal("false",        snap.GetValue("intersects-bottom"));
        Assert.Equal("false",        snap.GetValue("intersects-left"));
        Assert.Equal("false",        snap.GetValue("intersects-right"));
    }

    [Fact]
    public void Analyze_ProductTouchingBottomEdge_OneIntersectionAndOcclusionMostlyVisible()
    {
        // Dark rect from (80,80) to (319,399): reaches bottom edge only.
        string path = CreateJpeg("bottom", W, H, img =>
        {
            White(img);
            Fill(img, 80, 80, 240, H - 80, Dark);
        });
        var snap = Analyze(path);
        Assert.Equal("1",               snap.GetValue("intersection-count"));
        Assert.Equal("false",           snap.GetValue("fully-in-frame"));
        Assert.Equal("mostly-visible",  snap.GetValue("occlusion-level"));
        Assert.Equal("true",            snap.GetValue("intersects-bottom"));
        Assert.Equal("false",           snap.GetValue("intersects-top"));
        Assert.Equal("false",           snap.GetValue("intersects-left"));
        Assert.Equal("false",           snap.GetValue("intersects-right"));
    }

    [Fact]
    public void Analyze_ProductTouchingBottomAndRight_TwoIntersectionsAndOcclusionPartial()
    {
        // Dark rect (80,80)→(399,399) but with corners restored to white so
        // SampleCorners sees a clean white background regardless of BR corner overlap.
        string path = CreateJpeg("bottom-right", W, H, img =>
        {
            White(img);
            Fill(img, 80, 80, W - 80, H - 80, Dark);
            RestoreCorners(img); // keep all 4 corner zones white
        });
        var snap = Analyze(path);
        Assert.Equal("2",                   snap.GetValue("intersection-count"));
        Assert.Equal("partially-occluded",  snap.GetValue("occlusion-level"));
        Assert.Equal("true",                snap.GetValue("intersects-bottom"));
        Assert.Equal("true",                snap.GetValue("intersects-right"));
        Assert.Equal("false",               snap.GetValue("intersects-top"));
        Assert.Equal("false",               snap.GetValue("intersects-left"));
    }

    [Fact]
    public void Analyze_ProductTouchingThreeEdges_ThreeIntersectionsAndOcclusionCloseup()
    {
        // Dark rect (80,0)→(399,399): top, bottom, right touched; left clear.
        // TR and BR corner zones restored to white.
        string path = CreateJpeg("three-edges", W, H, img =>
        {
            White(img);
            Fill(img, 80, 0, W - 80, H, Dark);
            RestoreCorners(img);
        });
        var snap = Analyze(path);
        Assert.Equal("3",       snap.GetValue("intersection-count"));
        Assert.Equal("closeup", snap.GetValue("occlusion-level"));
        Assert.Equal("true",    snap.GetValue("intersects-top"));
        Assert.Equal("true",    snap.GetValue("intersects-bottom"));
        Assert.Equal("true",    snap.GetValue("intersects-right"));
        Assert.Equal("false",   snap.GetValue("intersects-left"));
    }

    [Fact]
    public void Analyze_ProductTouchingAllFourEdges_FourIntersectionsAndOcclusionCloseup()
    {
        // Dark everywhere, then repaint the 4 corner areas white so background is detected as white.
        // Intersection strips are all filled with dark → 4 intersections.
        string path = CreateJpeg("all-edges", W, H, img =>
        {
            Fill(img, 0, 0, W, H, Dark);   // start: all dark
            // Restore the 4 corner regions so background sampling sees white.
            Fill(img, 0,         0,         CornerPx, CornerPx, White255);
            Fill(img, W-CornerPx, 0,        CornerPx, CornerPx, White255);
            Fill(img, 0,         H-CornerPx, CornerPx, CornerPx, White255);
            Fill(img, W-CornerPx, H-CornerPx, CornerPx, CornerPx, White255);
        });
        var snap = Analyze(path);
        Assert.Equal("4",       snap.GetValue("intersection-count"));
        Assert.Equal("closeup", snap.GetValue("occlusion-level"));
        Assert.Equal("true",    snap.GetValue("intersects-top"));
        Assert.Equal("true",    snap.GetValue("intersects-bottom"));
        Assert.Equal("true",    snap.GetValue("intersects-left"));
        Assert.Equal("true",    snap.GetValue("intersects-right"));
    }

    //  Skin tone 

    [Fact]
    public void Analyze_AllWhiteImage_SkinToneAreaIsZero()
    {
        // White pixels fail Y < 0.95 → no skin tone detected.
        string path = CreateJpeg("white-skin", W, H, White);
        var snap = Analyze(path);
        Assert.Equal("0.0000", snap.GetValue("skin-tone-area"));
    }

    [Fact]
    public void Analyze_ImageWithSkinToneRegion_SkinToneAreaAboveZero()
    {
        // RGB(210, 170, 140) passes YCbCr skin-tone ranges for light skin tones.
        // Paint the right half so ≈50 % of sampled pixels are skin-tone.
        string path = CreateJpeg("skin", W, H, img =>
        {
            White(img);
            Fill(img, W / 2, 0, W / 2, H, new Rgba32(210, 170, 140));
        });
        var snap = Analyze(path);
        double area = double.Parse(snap.GetValue("skin-tone-area"), CultureInfo.InvariantCulture);
        Assert.True(area > 0.05, $"Expected skin-tone-area > 0.05, got {area}");
    }

    //  Unknown features 

    [Fact]
    public void Analyze_AllModelDependentFeatures_AreRecordedAsUnknown()
    {
        string path = CreateJpeg("unk", W, H, White);
        var snap = Analyze(path);

        string[] modelFeatures =
        [
            "hero-is-human", "hero-orientation", "has-human", "human-count",
            "has-head", "head-visible", "has-face", "face-visible",
            "body-visible", "pose-type", "contains-mannequin", "product-type-label",
            "packaging-visible", "multiple-products", "overlap-count",
            "scale-reference-present", "logo-present", "material-texture-visible",
            "text-present", "top-view", "shadow-present", "reflection-present",
            "lighting", "camera-angle", "product-coverage-ratio",
            "image-occupancy", "crop-tightness", "dominant-colors", "product-color",
            "background-color", "indoor", "outdoor", "symmetry-score",
            "product-aspect-ratio", "vertical-centering", "horizontal-centering"
        ];

        foreach (string feature in modelFeatures)
        {
            string actual = snap.GetValue(feature);
            Assert.True(actual == "UNKNOWN",
                $"Feature '{feature}' should be UNKNOWN after CPU-only analysis, got '{actual}'");
        }
    }

    [Fact]
    public void Analyze_UnknownFeatures_HaveZeroConfidence()
    {
        string path = CreateJpeg("unk-conf", W, H, White);
        var snap = Analyze(path);

        foreach (var (id, value) in snap.All)
        {
            if (value.Value == "UNKNOWN")
            {
                Assert.True(value.Confidence == 0.0,
                    $"Feature '{id}' is UNKNOWN but has non-zero confidence {value.Confidence}");
            }
        }
    }

    [Fact]
    public void Analyze_NoFeatureHasNullValue()
    {
        string path = CreateJpeg("no-null", W, H, White);
        var snap = Analyze(path);

        Assert.NotEmpty(snap.All);
        foreach (var (id, value) in snap.All)
        {
            Assert.NotNull(value.Value);
            Assert.False(string.IsNullOrEmpty(value.Value),
                $"Feature '{id}' has empty value");
        }
    }

    //  Helpers 

    private static readonly Rgba32 White255 = new(255, 255, 255, 255);
    private static readonly Rgba32 Dark     = new(20,  20,  20,  255);

    private static void White(Image<Rgba32> img) => Fill(img, 0, 0, img.Width, img.Height, White255);

    /// <summary>
    /// Repaints the four corner zones (10% of image dimensions) to white so that
    /// <see cref="ImageFeatureAnalyzer"/>'s corner-based background sampling
    /// always detects a clean white background, even when dark product pixels
    /// extend into those zones.
    /// </summary>
    private static void RestoreCorners(Image<Rgba32> img)
    {
        int cw = Math.Max(1, img.Width  / 10);
        int ch = Math.Max(1, img.Height / 10);
        Fill(img, 0,             0,              cw, ch, White255); // TL
        Fill(img, img.Width - cw, 0,             cw, ch, White255); // TR
        Fill(img, 0,             img.Height - ch, cw, ch, White255); // BL
        Fill(img, img.Width - cw, img.Height - ch, cw, ch, White255); // BR
    }

    private static void Fill(Image<Rgba32> img, int x, int y, int width, int height, Rgba32 color)
    {
        int endX = Math.Min(x + width,  img.Width);
        int endY = Math.Min(y + height, img.Height);
        for (int py = y; py < endY; py++)
            for (int px = x; px < endX; px++)
                img[px, py] = color;
    }

    private string CreateJpeg(string name, int width, int height, Action<Image<Rgba32>> paint)
    {
        string path = Path.Combine(_tempDir, $"{name}.jpg");
        using var img = new Image<Rgba32>(width, height);
        paint(img);
        img.SaveAsJpeg(path);
        return path;
    }

    private string CreatePng(string name, int width, int height, Action<Image<Rgba32>> paint)
    {
        string path = Path.Combine(_tempDir, $"{name}.png");
        using var img = new Image<Rgba32>(width, height);
        paint(img);
        img.SaveAsPng(path);
        return path;
    }

    private static ImageFeatureSnapshot Analyze(string imagePath)
    {
        var snap = new ImageFeatureSnapshot();
        using var image = Image.Load<Rgba32>(imagePath);
        ImageFeatureAnalyzer.Analyze(image, snap, AnalyzerParameters.FromConfig(), ClassifyParameters.FromConfig().ImageFeatureAnalyzer);
        return snap;
    }
}
