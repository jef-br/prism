using Xunit;

namespace PrismCoreTests.Transform;

/// <summary>
/// T-4870: the detection + toggle evidence is folded into OutputRecord.SafeSummaryText (the carrier the
/// Export transform-manifest reads), without ever placing the pixel mask there.
/// </summary>
public class TransformEvidenceTests {
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
    public void Evidence_RecordsSubjectAndToggles_InSafeSummary() {
        ImageRecord_LAMBDA lambda = new() {
            InitialFullName = "img.jpg", Width = 1000, Height = 1000,
            SelectedPhenotype = "front-packshot",
            Subject = new SubjectDetectionResult {
                Producer = "classical-cv", IsWholeFrameFallback = false, Confidence = 0.9, HasHardShadowEvidence = true,
                MaskPng = [1, 2, 3],
                Box = new BoundingBox { X = 100, Y = 100, Width = 800, Height = 800, Left = 100, Top = 100, Right = 900, Bottom = 900 }
            }
        };

        ImageTransformer.FinalizeGeometry(lambda, Parameters, null);
        ImageTransformer.TransformImage(lambda, null, false, Parameters);

        string summary = lambda.OutputRecord!.SafeSummaryText;
        Assert.Contains("promoted=True", summary);
        Assert.Contains("subject.producer=classical-cv", summary);
        Assert.Contains("subject.conf=0.90", summary);
        Assert.Contains("subject.hardShadow=True", summary);
        Assert.Contains("toggle.shadow=True", summary);
        // The original transformer summary is preserved ahead of the evidence.
        Assert.Contains("Center-and-stretch", summary);
    }

    [Fact]
    public void Evidence_NoSubject_RecordsSubjectNone() {
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "img.jpg", Width = 1000, Height = 1000, BoundingBox = null };

        ImageTransformer.TransformImage(lambda, null, false, Parameters);

        Assert.Contains("subject=none", lambda.OutputRecord!.SafeSummaryText);
    }
}
