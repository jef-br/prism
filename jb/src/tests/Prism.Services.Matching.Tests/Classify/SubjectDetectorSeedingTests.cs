using OpenCvSharp;
using Prism.Services.Matching;
using Xunit;

namespace PrismCoreTests.Classify;

/// <summary>
/// Pins the seeded detection contract (T-4860): the unseeded entry point must keep behaving exactly as it
/// did before seeding existed, and every seeded branch — flat, real-life, and unknown background — must
/// still return usable geometry rather than degrading to the whole-frame fallback.
/// </summary>
public class SubjectDetectorSeedingTests {
    private static SubjectDetectorConfig Config() =>
        ConfigLoader.Section<SubjectDetectorConfig>("ClassifyConfig.json", "SubjectDetector");

    // Saturated blob on white: chroma-separated, so every branch should find it regardless of seeding.
    private static Mat ProductOnWhite() {
        Mat img = new(200, 200, MatType.CV_8UC3, new Scalar(255, 255, 255));
        Cv2.Rectangle(img, new Rect(60, 60, 70, 70), new Scalar(40, 40, 200), thickness: -1);
        return img;
    }

    private static SubjectSeedHint Seed(string backgroundType, string productColor, string backgroundColor) {
        ImageFeatureSnapshot features = new();
        features.Set("background-type", backgroundType, 1.0, "test");
        features.Set("product-color", productColor, 1.0, "test");
        features.Set("background-color", backgroundColor, 1.0, "test");
        return SubjectSeedHint.Resolve(features, family: null);
    }

    [Fact]
    public void UnseededOverload_MatchesExplicitNullSeed() {
        // The one-arg overload is the documented "no Excel/CLIP context" entry point. It must stay a pure
        // alias — if these ever diverge, every existing caller silently changes behaviour.
        using Mat img = ProductOnWhite();
        SubjectDetector detector = new(Config());

        SubjectDetectionResult implicitly = detector.Detect(img);
        SubjectDetectionResult explicitly = detector.Detect(img, null);

        Assert.Equal(implicitly.Box.Left, explicitly.Box.Left);
        Assert.Equal(implicitly.Box.Top, explicitly.Box.Top);
        Assert.Equal(implicitly.Box.Width, explicitly.Box.Width);
        Assert.Equal(implicitly.Box.Height, explicitly.Box.Height);
        Assert.Equal(implicitly.IsWholeFrameFallback, explicitly.IsWholeFrameFallback);
        Assert.Equal(implicitly.HasHardShadowEvidence, explicitly.HasHardShadowEvidence);
    }

    [Theory]
    [InlineData("SOLIDCOLOR", "red", "white")]   // flat + distinct colours: CLAHE skipped, no escalation
    [InlineData("SOLIDCOLOR", "white", "white")] // flat + matching colours: CLAHE runs
    [InlineData("REALLIFE", "red", "white")]     // straight to HeroDetectionOnSteroids
    [InlineData("UNKNOWN", "red", "white")]      // non-flat: speckle pass, escalates only on high residual
    public void SeededBranches_StillBoxTheProduct(string backgroundType, string productColor, string backgroundColor) {
        using Mat img = ProductOnWhite();

        SubjectDetectionResult d = new SubjectDetector(Config()).Detect(img, Seed(backgroundType, productColor, backgroundColor));

        Assert.False(d.IsWholeFrameFallback, $"seed({backgroundType}) degraded to whole-frame instead of finding the product");
        Assert.True(d.Box.Left <= 70, $"box left {d.Box.Left} does not cover the product starting at x=60");
        Assert.True(d.Box.Right >= 120, $"box right {d.Box.Right} does not cover the product ending at x=130");
        Assert.True(d.Box.Width < 180, $"box width {d.Box.Width} is effectively the whole frame");
    }

    [Fact]
    public void RealLifeSeed_TakesSteroidsPath_WithoutLosingTheProduct() {
        // The escalated pass analyses at a higher resolution and applies a stricter significant-blob bar.
        // The risk it introduces is over-filtering: a stricter bar could drop the product itself. This pins
        // that it does not, on an image large enough that the two analysis sizes genuinely differ.
        using Mat img = new(1200, 1200, MatType.CV_8UC3, new Scalar(255, 255, 255));
        Cv2.Rectangle(img, new Rect(300, 300, 500, 500), new Scalar(40, 40, 200), thickness: -1);

        SubjectDetectionResult d = new SubjectDetector(Config()).Detect(img, Seed("REALLIFE", "red", "white"));

        Assert.False(d.IsWholeFrameFallback);
        Assert.True(d.Box.Left <= 320, $"box left {d.Box.Left} lost the product edge at x=300");
        Assert.True(d.Box.Right >= 780, $"box right {d.Box.Right} lost the product edge at x=800");
    }
}
