using Prism.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace PrismCoreTests.Analyzers;

/// <summary>
/// Synthetic-image tests for the wave-3 visual analyzers: subject geometry from the
/// foreground-box fallback, dominant/product colors with background exclusion, background
/// color naming, and exposure flags.
/// </summary>
public class VisualAnalyzerTests {
    // Mirrors the shipped analyzer_Config.json sections exercised below.
    private static readonly Analyzer_SubjectGeometry.Config SubjectGeometryCfg = new() {
        ForegroundColorDistance = 0.15f,
        MinForegroundFraction = 0.005f,
        FallbackConfidence = 0.60f,
        BoxAreaCoverageConfidenceDiscount = 0.9f
    };

    private static readonly ColorAnalyzerConfig ColorsCfg = new() {
        BucketCount = 4,
        BinsPerChannel = 8,
        MinBucketShare = 0.02f,
        BackgroundDistance = 0.12f,
        MinSampleFraction = 0.02f,
        DominantColorsConfidence = 0.70f,
        ProductColorConfidence = 0.80f,
        BackgroundColorConfidence = 0.85f,
        Palette = new Dictionary<string, string> {
            ["black"] = "#000000",
            ["white"] = "#ffffff",
            ["grey"] = "#808080",
            ["red"] = "#cc0000",
            ["blue"] = "#0044cc",
            ["green"] = "#00aa44",
            ["yellow"] = "#ffdd00",
            ["orange"] = "#ff8800",
            ["pink"] = "#ff66aa",
            ["purple"] = "#7733aa",
            ["brown"] = "#8b5a2b",
            ["beige"] = "#d9c7a7"
        }
    };

    private static readonly Analyzer_Exposure.Config ExposureCfg = new() {
        HighLuminance = 0.98f,
        LowLuminance = 0.02f,
        FlaggedFraction = 0.25f,
        Confidence = 0.70f
    };

    private static readonly Analyzer_MultipleProducts.Config MultipleProductsCfg = new() {
        OverlapIou = 0.10f,
        Confidence = 0.70f
    };

    private static readonly YoloAnalyzerConfig YoloCfg = new() {
        ConfidenceThreshold = 0.40f,
        MaxDetections = 32,
        HumanMinConfidence = 0.50f,
        AbsenceConfidence = 0.60f,
        HeroPersonMinArea = 0.15f
    };

    private static readonly SkinToneAnalyzerConfig SkinToneCfg = new() {
        LumaMin = 0.10f,
        LumaMax = 0.95f,
        CbMin = 0.30f,
        CbMax = 0.53f,
        CrMin = 0.52f,
        CrMax = 0.68f
    };

    // A 200×200 white canvas with a centered 100×100 solid square of the given color.
    private static Image<Rgba32> CenteredSquare(Rgba32 color) {
        var image = new Image<Rgba32>(200, 200, new Rgba32(255, 255, 255));
        image.ProcessPixelRows(accessor => {
            for (int y = 50; y < 150; y++) {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 50; x < 150; x++) row[x] = color;
            }
        });
        return image;
    }

    [Fact]
    public void SubjectGeometry_CenteredSquare_MeasuresBoxAndCentering() {
        using Image<Rgba32> image = CenteredSquare(new Rgba32(200, 40, 40));
        var snapshot = new ImageFeatureSnapshot();

        SubjectBox? subject = Analyzer_SubjectGeometry.Analyze(image, [], snapshot, SubjectGeometryCfg);

        Assert.NotNull(subject);
        Assert.Equal("foreground", subject!.Source);
        // Centered square: both centering scores near 1, occupancy near 0.25.
        Assert.True(double.Parse(snapshot.GetValue("vertical-centering"), System.Globalization.CultureInfo.InvariantCulture) > 0.9);
        Assert.True(double.Parse(snapshot.GetValue("horizontal-centering"), System.Globalization.CultureInfo.InvariantCulture) > 0.9);
        double occupancy = double.Parse(snapshot.GetValue("image-occupancy"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(occupancy, 0.20, 0.32);
    }

    [Fact]
    public void SubjectGeometry_YoloDetection_WinsOverFallback() {
        using Image<Rgba32> image = CenteredSquare(new Rgba32(200, 40, 40));
        var snapshot = new ImageFeatureSnapshot();
        var detection = new YoloDetection(39, "bottle", 0.9f, 0.1f, 0.1f, 0.6f, 0.9f);

        SubjectBox? subject = Analyzer_SubjectGeometry.Analyze(image, [detection], snapshot, SubjectGeometryCfg);

        Assert.NotNull(subject);
        Assert.Equal("yolo", subject!.Source);
    }

    [Fact]
    public void DominantColors_RedSquareOnWhite_ExcludesBackgroundAndFindsRed() {
        using Image<Rgba32> image = CenteredSquare(new Rgba32(200, 40, 40));
        var snapshot = new ImageFeatureSnapshot();
        var cfg = ColorsCfg;
        SubjectBox subject = new(0.25f, 0.25f, 0.75f, 0.75f, 0.9f, "foreground");

        IReadOnlyList<ColorBucket> buckets = Analyzer_DominantColors.Analyze(image, subject, snapshot, cfg, SkinToneCfg);

        Assert.NotEmpty(buckets);
        Assert.True(buckets[0].R > 0.6f && buckets[0].G < 0.35f);
        Assert.NotEqual("UNKNOWN", snapshot.GetValue("dominant-colors"));

        Analyzer_ProductColor.Analyze(buckets, snapshot, cfg);
        Assert.Equal("red", snapshot.GetValue("product-color"));
    }

    [Fact]
    public void DominantColors_WhiteOnWhite_StaysUnknown() {
        // White product on white background: exclusion eats everything — never guess.
        using var image = new Image<Rgba32>(200, 200, new Rgba32(250, 250, 250));
        var snapshot = new ImageFeatureSnapshot();
        SubjectBox subject = new(0.25f, 0.25f, 0.75f, 0.75f, 0.6f, "foreground");

        IReadOnlyList<ColorBucket> buckets = Analyzer_DominantColors.Analyze(image, subject, snapshot, ColorsCfg, SkinToneCfg);

        Assert.Empty(buckets);
        Assert.Equal("UNKNOWN", snapshot.GetValue("dominant-colors"));
    }

    [Fact]
    public void BackgroundColor_SolidWhite_NamedWhite() {
        using Image<Rgba32> image = CenteredSquare(new Rgba32(200, 40, 40));
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("background-type", "SOLIDCOLOR", 0.9, "imagesharp");

        Analyzer_BackgroundColor.Analyze(image, snapshot, ColorsCfg);

        Assert.Equal("white", snapshot.GetValue("background-color"));
    }

    [Fact]
    public void BackgroundColor_RealLifeBackground_StaysUnknown() {
        using Image<Rgba32> image = CenteredSquare(new Rgba32(200, 40, 40));
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("background-type", "REALLIFE", 0.9, "imagesharp");

        Analyzer_BackgroundColor.Analyze(image, snapshot, ColorsCfg);

        Assert.Equal("UNKNOWN", snapshot.GetValue("background-color"));
    }

    [Fact]
    public void Exposure_NearBlackImage_FlagsUnderexposed() {
        using var image = new Image<Rgba32>(100, 100, new Rgba32(2, 2, 2));
        var snapshot = new ImageFeatureSnapshot();

        Analyzer_Exposure.Analyze(image, snapshot, ExposureCfg, ColorsCfg);

        Assert.Equal("true", snapshot.GetValue("underexposed"));
        Assert.Equal("false", snapshot.GetValue("overexposed"));
    }

    [Fact]
    public void Exposure_WhitePackshot_BackgroundExcluded_NotOverexposed() {
        // Mid-grey product on solid white: the white background must not flag overexposure.
        using Image<Rgba32> image = CenteredSquare(new Rgba32(120, 120, 120));
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("background-type", "SOLIDCOLOR", 0.9, "imagesharp");

        Analyzer_Exposure.Analyze(image, snapshot, ExposureCfg, ColorsCfg);

        Assert.Equal("false", snapshot.GetValue("overexposed"));
    }

    [Fact]
    public void MultipleProducts_TwoOverlappingObjects_CountsBoth() {
        var snapshot = new ImageFeatureSnapshot();
        var a = new YoloDetection(39, "bottle", 0.8f, 0.1f, 0.1f, 0.5f, 0.9f);
        var b = new YoloDetection(41, "cup", 0.7f, 0.3f, 0.2f, 0.7f, 0.8f);

        Analyzer_MultipleProducts.Analyze([a, b], snapshot, MultipleProductsCfg);

        Assert.Equal("true", snapshot.GetValue("multiple-products"));
        Assert.Equal("1", snapshot.GetValue("overlap-count"));
    }

    [Fact]
    public void HasHuman_DominantPerson_SetsHeroIsHumanTrue() {
        var snapshot = new ImageFeatureSnapshot();
        var person = new YoloDetection(0, "person", 0.9f, 0.2f, 0.0f, 0.8f, 1.0f); // 60% of frame

        Analyzer_HasHuman.Analyze([person], snapshot, YoloCfg);

        Assert.Equal("true", snapshot.GetValue("has-human"));
        Assert.Equal("TRUE", snapshot.GetValue("hero-is-human"));
    }

    [Fact]
    public void HasHuman_NoPerson_SetsHeroIsHumanFalse() {
        var snapshot = new ImageFeatureSnapshot();

        Analyzer_HasHuman.Analyze([], snapshot, YoloCfg);

        Assert.Equal("false", snapshot.GetValue("has-human"));
        Assert.Equal("FALSE", snapshot.GetValue("hero-is-human"));
    }

    [Fact]
    public void HasHuman_TinyPerson_LeavesHeroIsHumanUnknown() {
        // A small person in frame (scale reference, bystander) is not the hero.
        var snapshot = new ImageFeatureSnapshot();
        var person = new YoloDetection(0, "person", 0.9f, 0.45f, 0.45f, 0.55f, 0.55f); // 1% of frame

        Analyzer_HasHuman.Analyze([person], snapshot, YoloCfg);

        Assert.Equal("true", snapshot.GetValue("has-human"));
        Assert.Equal("UNKNOWN", snapshot.GetValue("hero-is-human"));
    }

    [Fact]
    public void HasHuman_DoesNotOverwriteStrongerHeroEvidence() {
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("hero-is-human", "TRUE", 0.95, "clip");

        Analyzer_HasHuman.Analyze([], snapshot, YoloCfg);

        Assert.Equal("TRUE", snapshot.GetValue("hero-is-human"));
    }

    [Fact]
    public void MultipleProducts_NoDetections_StaysUnknown() {
        var snapshot = new ImageFeatureSnapshot();

        Analyzer_MultipleProducts.Analyze([], snapshot, MultipleProductsCfg);

        Assert.Equal("UNKNOWN", snapshot.GetValue("multiple-products"));
    }
}
