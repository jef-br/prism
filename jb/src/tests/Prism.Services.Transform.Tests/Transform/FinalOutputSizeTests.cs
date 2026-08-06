using Xunit;

namespace PrismCoreTests.Transform;

/// <summary>
/// T-4910: the shared exact final-output-size calculator. Sizes are pinned as literal pixel counts,
/// not recomputed from the same formula the code under test uses — a test that re-derives the answer
/// proves only that the expression was copied, not that it is right.
/// </summary>
public class FinalOutputSizeTests {
    private const double Margin = 0.042;
    private const int Bar = 800;

    // The canonical worked example the centre-and-stretch canvas geometry was confirmed against.
    [Fact]
    public void CanvasSize_WorkedExample_1800BboxYields1948() {
        Assert.Equal(1948, FinalOutputSize.CenterAndStretchCanvasSize(1800, Margin));
    }

    [Fact]
    public void LongestDimension_ZeroIntersection_IsMarginedCanvasAroundBbox() {
        ImageRecord_LAMBDA lambda = Lambda(BoxOf(1800, 1200), intersects: false);

        Assert.Equal(1948, FinalOutputSize.LongestDimension(lambda, 3000, 3000, Margin));
    }

    // Decision 4: a bleeding subject gets no whitespace term. The square crop is the image's shorter
    // side, so the bbox does not enter the size at all.
    [Fact]
    public void LongestDimension_Bleed_IsShorterImageSide_WithNoMarginTerm() {
        ImageRecord_LAMBDA lambda = Lambda(BoxOf(1800, 1200), intersects: true);

        Assert.Equal(1200, FinalOutputSize.LongestDimension(lambda, 1600, 1200, Margin));
    }

    // 740 is the smallest bbox longest side whose canvas reaches 800 at this margin: 739 gives 798.
    // Both halves are asserted, so the boundary is pinned from below as well as above.
    [Fact]
    public void RequiredBboxSide_ForTheBar_Is740_And739Misses() {
        Assert.Equal(800, FinalOutputSize.CenterAndStretchCanvasSize(740, Margin));
        Assert.Equal(798, FinalOutputSize.CenterAndStretchCanvasSize(739, Margin));
    }

    [Fact]
    public void MinimalScale_ZeroIntersection_ScalesBboxToExactlyTheRequiredSide() {
        ImageRecord_LAMBDA lambda = Lambda(BoxOf(600, 400), intersects: false);

        double scale = FinalOutputSize.MinimalScaleToReach(Bar, lambda, 1000, 1000, Margin);

        Assert.Equal(740, (int)Math.Round(600 * scale));
        Assert.Equal(Bar, FinalOutputSize.CenterAndStretchCanvasSize((int)Math.Round(600 * scale), Margin));
    }

    [Fact]
    public void MinimalScale_AlreadyClearsTheBar_IsExactlyOne() {
        ImageRecord_LAMBDA lambda = Lambda(BoxOf(740, 500), intersects: false);

        Assert.Equal(1.0, FinalOutputSize.MinimalScaleToReach(Bar, lambda, 1000, 1000, Margin));
    }

    [Fact]
    public void MinimalScale_Bleed_TakesTheShorterImageSideToTheBar() {
        ImageRecord_LAMBDA lambda = Lambda(BoxOf(700, 500), intersects: true);

        double scale = FinalOutputSize.MinimalScaleToReach(Bar, lambda, 900, 700, Margin);

        Assert.Equal(Bar, (int)Math.Round(700 * scale));
        Assert.Equal(Bar, FinalOutputSize.LongestDimension(lambda, (int)Math.Round(900 * scale), (int)Math.Round(700 * scale), Margin));
    }

    // A bbox one pixel above the required side must not be scaled at all — the "as little as possible"
    // half of the contract, which a formula that always upscales below the bar would silently break.
    [Fact]
    public void MinimalScale_OnePixelAboveRequired_DoesNotUpscale() {
        Assert.Equal(1.0, FinalOutputSize.MinimalScaleToReach(Bar, Lambda(BoxOf(741, 500), intersects: false), 1000, 1000, Margin));
        Assert.True(FinalOutputSize.MinimalScaleToReach(Bar, Lambda(BoxOf(739, 500), intersects: false), 1000, 1000, Margin) > 1.0);
    }

    [Fact]
    public void RoutesToCenterAndStretch_RequiresBothABoxAndNoIntersect() {
        Assert.True(FinalOutputSize.RoutesToCenterAndStretch(Lambda(BoxOf(700, 500), intersects: false)));
        Assert.False(FinalOutputSize.RoutesToCenterAndStretch(Lambda(BoxOf(700, 500), intersects: true)));
        Assert.False(FinalOutputSize.RoutesToCenterAndStretch(new ImageRecord_LAMBDA { InitialFullName = "x.jpg", BoundingBox = null }));
    }

    private static BoundingBox BoxOf(int width, int height) => new() {
        X = 10,
        Y = 10,
        Width = width,
        Height = height,
        Left = 10,
        Top = 10,
        Right = 10 + width,
        Bottom = 10 + height
    };

    private static ImageRecord_LAMBDA Lambda(BoundingBox box, bool intersects) {
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "img.jpg", BoundingBox = box };
        lambda.Features.Set("intersects-left", intersects ? "true" : "false", 1.0, "test");
        return lambda;
    }
}
