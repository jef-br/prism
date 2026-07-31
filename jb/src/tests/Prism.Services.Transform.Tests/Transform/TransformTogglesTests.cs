using Xunit;

namespace PrismCoreTests.Transform;

/// <summary>
/// T-4860: the three seeding toggles are computed from the resolved seed + detector result, and the
/// shadow-accounting toggle trims the box bottom so a cast shadow is not centred as product.
/// </summary>
public class TransformTogglesTests {
    private static readonly TransformParameters Parameters = new() {
        Crop = new() { WhiteSpaceMargin = 0.042, CropCoverage = 0.8, CropExtensionOneSided = 0.14, CropExtensionBiDirectional = 0.25, ShadowBottomShrinkFraction = 0.06, SubjectPromotionMinConfidence = 0.35 },
        ProblemImageProcessor = new() { MinInputPx = 570, MinOutputPx = 800, MaxUpscale = 1.42 },
        BgStretch = new() { Tier1MaxRatio = 1.25f, Tier2MaxRatio = 1.42f, Tier4MinRatio = 2.50f, FeatherPx = 16 },
        DetailCropper = new() { AdjacentCropCap = 0.14 },
        LowContrastEnhancement = new() { ClipLimit = 2.0, TileSize = 8 },
        HeadCutter = new() { FaceHeightCutFactor = 0.75 },
        Output = new() { JpegOutputQuality = 100 }
    };

    [Fact]
    public void Resolve_AllThreeToggles_FireOnTheirSignals() {
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "a.jpg" };
        lambda.Features.Set("product-color", "white", 1.0, "clip");
        lambda.Features.Set("background-color", "white", 1.0, "clip");
        lambda.Features.Set("background-type", "REALLIFE", 1.0, "clip");
        TransformSeed seed = TransformSeed.Resolve(lambda, null);
        SubjectDetectionResult subject = new() { HasHardShadowEvidence = true };

        TransformToggles toggles = TransformToggles.Resolve(seed, subject);

        Assert.True(toggles.ProductNearBackground);
        Assert.True(toggles.NonFlatBackground);
        Assert.True(toggles.ShadowAccounting);
    }

    [Fact]
    public void Resolve_FlatBackgroundDistinctColour_NoToggles() {
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "b.jpg" };
        lambda.Features.Set("product-color", "navy", 1.0, "clip");
        lambda.Features.Set("background-color", "white", 1.0, "clip");
        lambda.Features.Set("background-type", "SOLIDCOLOR", 1.0, "clip");
        TransformSeed seed = TransformSeed.Resolve(lambda, null);

        TransformToggles toggles = TransformToggles.Resolve(seed, new SubjectDetectionResult { HasHardShadowEvidence = false });

        Assert.False(toggles.ProductNearBackground);
        Assert.False(toggles.NonFlatBackground);
        Assert.False(toggles.ShadowAccounting);
    }

    [Fact]
    public void ShadowAccounting_TrimsBoxBottom_WhenNotIntersectingBottom() {
        ImageRecord_LAMBDA lambda = new() {
            InitialFullName = "c.jpg", Width = 1000, Height = 1000,
            Subject = new SubjectDetectionResult {
                Producer = "classical-cv", IsWholeFrameFallback = false, Confidence = 0.9, HasHardShadowEvidence = true,
                Box = new BoundingBox { X = 100, Y = 100, Width = 800, Height = 800, Left = 100, Top = 100, Right = 900, Bottom = 900 }
            }
        };

        ImageTransformer.FinalizeGeometry(lambda, Parameters, null);
        ImageTransformer.TransformImage(lambda, null, false, Parameters);

        // 0.06 * 800 = 48px trimmed from the bottom → height 752, bottom 852.
        Assert.Equal(752, lambda.BoundingBox!.Value.Height);
        Assert.Equal(852, lambda.BoundingBox!.Value.Bottom);
    }

    [Fact]
    public void NoShadowEvidence_LeavesBoxUnchanged() {
        ImageRecord_LAMBDA lambda = new() {
            InitialFullName = "d.jpg", Width = 1000, Height = 1000,
            Subject = new SubjectDetectionResult {
                Producer = "classical-cv", IsWholeFrameFallback = false, Confidence = 0.9, HasHardShadowEvidence = false,
                Box = new BoundingBox { X = 100, Y = 100, Width = 800, Height = 800, Left = 100, Top = 100, Right = 900, Bottom = 900 }
            }
        };

        ImageTransformer.FinalizeGeometry(lambda, Parameters, null);
        ImageTransformer.TransformImage(lambda, null, false, Parameters);

        Assert.Equal(800, lambda.BoundingBox!.Value.Height);
    }

    // T-4860: the shrink is scoped to the Tx_CenterAndStretch route only. An intersecting image (routed
    // to Tx_CropSquare) must keep its box unshrunk even with hard-shadow evidence present.
    [Fact]
    public void ShadowAccounting_CropSquareRoute_LeavesBoxUnshrunk() {
        ImageRecord_LAMBDA lambda = new() {
            InitialFullName = "e.jpg", Width = 1000, Height = 1000,
            Subject = new SubjectDetectionResult {
                Producer = "classical-cv", IsWholeFrameFallback = false, Confidence = 0.9, HasHardShadowEvidence = true,
                IntersectsLeft = true,
                Box = new BoundingBox { X = 0, Y = 100, Width = 800, Height = 800, Left = 0, Top = 100, Right = 800, Bottom = 900 }
            }
        };

        ImageTransformer.FinalizeGeometry(lambda, Parameters, null);
        ImageTransformer.TransformImage(lambda, null, false, Parameters);

        Assert.Equal(nameof(Tx_CropSquare), lambda.OutputRecord?.TransformerType);
        Assert.Equal(800, lambda.BoundingBox!.Value.Height);
    }

    // T-4860: SOLIDCOLOR is the only flat background-type; REALLIFE and UNKNOWN must not collapse to
    // the same "flat" reading — each case is asserted explicitly against the toggle.
    [Fact]
    public void NonFlatBackground_SolidColor_IsFlat() {
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "f.jpg" };
        lambda.Features.Set("background-type", "SOLIDCOLOR", 1.0, "clip");
        TransformSeed seed = TransformSeed.Resolve(lambda, null);

        TransformToggles toggles = TransformToggles.Resolve(seed, null);

        Assert.False(toggles.NonFlatBackground);
    }

    [Fact]
    public void NonFlatBackground_RealLife_IsNonFlat() {
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "g.jpg" };
        lambda.Features.Set("background-type", "REALLIFE", 1.0, "clip");
        TransformSeed seed = TransformSeed.Resolve(lambda, null);

        TransformToggles toggles = TransformToggles.Resolve(seed, null);

        Assert.True(toggles.NonFlatBackground);
    }

    [Fact]
    public void NonFlatBackground_Unknown_IsNonFlat_NotTreatedAsSolidColor() {
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "h.jpg" };
        lambda.Features.Set("background-type", "UNKNOWN", 1.0, "clip");
        TransformSeed seed = TransformSeed.Resolve(lambda, null);

        TransformToggles toggles = TransformToggles.Resolve(seed, null);

        Assert.True(toggles.NonFlatBackground);
    }
}
