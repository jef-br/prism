using Xunit;

namespace PrismCoreTests.Transform;

/// <summary>
/// T-4955: promoting the subject box rewrites the four <c>intersects-*</c> booleans, and
/// <c>intersection-count</c> / <c>fully-in-frame</c> are derived from exactly those four. Leaving the
/// derived pair holding the pre-promotion heuristic values made the snapshot contradict itself on 36
/// of 86 SPACINI29 images. The invariant asserted here is agreement, not any particular value: the
/// phenotype rules read both halves in a single evaluation, so a snapshot that says one edge is
/// touched and the count is zero satisfies two mutually-exclusive rules at once.
/// </summary>
public class SubjectPromotionConsistencyTests {
    private static readonly TransformParameters Parameters = new() {
        Crop = new() { WhiteSpaceMargin = 0.042, CropCoverage = 0.8, CropExtensionOneSided = 0.14, CropExtensionBiDirectional = 0.25, ShadowBottomShrinkFraction = 0.06, SubjectPromotionMinConfidence = 0.35 },
        ProblemImageProcessor = new() { MinInputPx = 570, MinOutputPx = 800, MaxUpscale = 1.42 },
        BgStretch = new() { Tier1MaxRatio = 1.25f, Tier2MaxRatio = 1.42f, Tier4MinRatio = 2.50f, FeatherPx = 16 },
        DetailCropper = new() { AdjacentCropCap = 0.14 },
        LowContrastEnhancement = new() { ClipLimit = 2.0, TileSize = 8 },
        HeadCutter = new() { FaceHeightCutFactor = 0.75 },
        Output = new() { JpegOutputQuality = 100 }
    };

    [Theory]
    [InlineData(false, false, false, false, 0)]
    [InlineData(true, false, false, false, 1)]
    [InlineData(true, true, false, false, 2)]
    [InlineData(true, true, true, false, 3)]
    [InlineData(true, true, true, true, 4)]
    public void Promotion_RecomputesIntersectionCount_FromThePromotedFlags(bool top, bool bottom, bool left, bool right, int expected) {
        // The pre-promotion snapshot deliberately disagrees with the detector on every field, which is
        // what the Classified-stage heuristic produced on the 42% of images this defect was measured on.
        ImageRecord_LAMBDA lambda = StaleSnapshot(top, bottom, left, right);

        ImageTransformer.FinalizeGeometry(lambda, Parameters, null);

        Assert.Equal(expected.ToString(), lambda.Features.GetValue("intersection-count"));
        Assert.Equal(expected == 0 ? "true" : "false", lambda.Features.GetValue("fully-in-frame"));
    }

    [Fact]
    public void Promotion_LeavesNoContradiction_BetweenFlagsAndCount() {
        // The exact contradiction T-4970 found on 23211041_03_A.jpg: one edge flagged, count zero.
        ImageRecord_LAMBDA lambda = StaleSnapshot(top: true, bottom: false, left: false, right: false);

        ImageTransformer.FinalizeGeometry(lambda, Parameters, null);

        int flagged = 0;
        foreach (string edge in new[] { "intersects-top", "intersects-bottom", "intersects-left", "intersects-right" })
            if (lambda.Features.GetValue(edge) == "true") flagged++;

        Assert.Equal(flagged.ToString(), lambda.Features.GetValue("intersection-count"));
        Assert.Equal(flagged == 0 ? "true" : "false", lambda.Features.GetValue("fully-in-frame"));
    }

    [Fact]
    public void NoPromotion_LeavesTheClassifiedStageValuesAlone() {
        // Below the confidence floor nothing is promoted, so the derived pair must not be rewritten
        // either — the Classified-stage measurement is still the only measurement on this record.
        ImageRecord_LAMBDA lambda = StaleSnapshot(top: true, bottom: true, left: false, right: false, confidence: 0.20);

        ImageTransformer.FinalizeGeometry(lambda, Parameters, null);

        Assert.Equal("0", lambda.Features.GetValue("intersection-count"));
        Assert.Equal("true", lambda.Features.GetValue("fully-in-frame"));
    }

    private static ImageRecord_LAMBDA StaleSnapshot(bool top, bool bottom, bool left, bool right, double confidence = 0.9) {
        ImageRecord_LAMBDA lambda = new() {
            InitialFullName = "img.jpg", Width = 1000, Height = 1000,
            SelectedPhenotype = "front-packshot",
            Subject = new SubjectDetectionResult {
                Producer = "classical-cv", IsWholeFrameFallback = false, Confidence = confidence,
                IntersectsTop = top, IntersectsBottom = bottom, IntersectsLeft = left, IntersectsRight = right,
                Box = new BoundingBox { X = 100, Y = 100, Width = 800, Height = 800, Left = 100, Top = 100, Right = 900, Bottom = 900 }
            }
        };

        lambda.Features.Set("intersects-top", "false", 0.85, "heuristic");
        lambda.Features.Set("intersects-bottom", "false", 0.85, "heuristic");
        lambda.Features.Set("intersects-left", "false", 0.85, "heuristic");
        lambda.Features.Set("intersects-right", "false", 0.85, "heuristic");
        lambda.Features.Set("intersection-count", "0", 0.85, "heuristic");
        lambda.Features.Set("fully-in-frame", "true", 0.85, "heuristic");
        return lambda;
    }
}
