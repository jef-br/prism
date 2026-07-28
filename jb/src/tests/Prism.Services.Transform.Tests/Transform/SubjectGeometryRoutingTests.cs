using Xunit;

namespace PrismCoreTests.Transform;

/// <summary>
/// T-4850: a confident subject detection supersedes the legacy salient bbox for routing and geometry.
/// A real Subject promotes a box even when BoundingBox is null (so no false ProblemImageProcessor
/// route); its per-edge intersects drive the crop-vs-centre decision; the whole-frame fallback is
/// ignored so the legacy bbox stands.
/// </summary>
public class SubjectGeometryRoutingTests {
    private static readonly TransformParameters Parameters = new() {
        Crop = new() { WhiteSpaceMargin = 0.042, CropCoverage = 0.8, CropExtensionOneSided = 0.14, CropExtensionBiDirectional = 0.25, ShadowBottomShrinkFraction = 0.06 },
        ProblemImageProcessor = new() { MinInputPx = 570, MinOutputPx = 800, MaxUpscale = 1.42 },
        BgStretch = new() { Tier1MaxRatio = 1.25f, Tier2MaxRatio = 1.42f, Tier4MinRatio = 2.50f, FeatherPx = 16 },
        DetailCropper = new() { AdjacentCropCap = 0.14 },
        LowContrastEnhancement = new() { ClipLimit = 2.0, TileSize = 8 },
        HeadCutter = new() { FaceHeightCutFactor = 0.75 },
        Output = new() { JpegOutputQuality = 100 }
    };

    [Fact]
    public void ConfidentSubject_PromotesBox_EvenWhenBoundingBoxNull() {
        ImageRecord_LAMBDA lambda = new() {
            InitialFullName = "img.jpg", Width = 1000, Height = 1000, BoundingBox = null,
            Subject = Subject(intersect: false, wholeFrameFallback: false)
        };

        ImageTransformer.TransformImage(lambda, null, false, Parameters);

        Assert.Equal(nameof(Tx_CenterAndStretch), lambda.OutputRecord?.TransformerType);
        Assert.NotNull(lambda.BoundingBox);
    }

    [Fact]
    public void ConfidentSubject_WithIntersect_RoutesToCropSquare() {
        ImageRecord_LAMBDA lambda = new() {
            InitialFullName = "img.jpg", Width = 1000, Height = 1000, BoundingBox = null,
            Subject = Subject(intersect: true, wholeFrameFallback: false)
        };

        ImageTransformer.TransformImage(lambda, null, false, Parameters);

        Assert.Equal(nameof(Tx_CropSquare), lambda.OutputRecord?.TransformerType);
    }

    [Fact]
    public void WholeFrameFallbackSubject_IsIgnored_LegacyNullRoutesToProblemProcessor() {
        ImageRecord_LAMBDA lambda = new() {
            InitialFullName = "img.jpg", Width = 1000, Height = 1000, BoundingBox = null,
            Subject = Subject(intersect: false, wholeFrameFallback: true)
        };

        ImageTransformer.TransformImage(lambda, null, false, Parameters);

        Assert.Equal(nameof(Tx_ProblemImageProcessor), lambda.OutputRecord?.TransformerType);
    }

    private static SubjectDetection Subject(bool intersect, bool wholeFrameFallback) => new() {
        Producer = "classical-cv",
        IsWholeFrameFallback = wholeFrameFallback,
        Confidence = 0.9,
        IntersectsTop = intersect,
        Box = new BoundingBox { X = 100, Y = 100, Width = 800, Height = 800, Left = 100, Top = 100, Right = 900, Bottom = 900 }
    };
}
