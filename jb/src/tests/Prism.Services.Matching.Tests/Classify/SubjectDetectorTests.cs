using OpenCvSharp;
using Xunit;

namespace PrismCoreTests.Classify;

/// <summary>
/// T-4830: the classical-CV SubjectDetector isolates the product against a studio sweep by chroma +
/// texture. Synthetic cases: a coloured subject on white is boxed inside the frame; a flat frame yields
/// no detection (whole frame, zero confidence); a subject running off an edge sets that intersect flag.
/// </summary>
public class SubjectDetectorTests {
    private static SubjectDetectorConfig Config() => new() {
        MaxAnalysisSize = 1024,
        TextureWindow = 7,
        TextureDetailSigma = 4.0,
        OutlierSpreadMultiplier = 4.0,
        MinComponentAreaFraction = 0.0005,
        MinComponentAreaRatio = 0.05,
        MinComponentAreaPixels = 25.0,
        WholeFrameFraction = 0.985,
        ShadowEdgeKernel = 15,
        CannySigma = 0.33,
        CannyCloseKernel = 5,
        BorderRingFraction = 0.02,
        ChromaFloor = 2.0,
        TextureFloor = 2.0,
        ClaheClipLimit = 2.0,
        ClaheTileSize = 8,
        BleedContact = 0.2,
        HardShadowEvidenceFraction = 0.01
    };

    [Fact]
    public void Detect_ColouredSubjectOnWhite_BoxesInsideFrame_NoIntersects() {
        using Mat img = White(200, 200);
        Cv2.Rectangle(img, new Rect(50, 50, 100, 100), new Scalar(40, 40, 200), thickness: -1);

        SubjectDetection d = new SubjectDetector(Config()).Detect(img);

        Assert.Equal("classical-cv", d.Producer);
        Assert.True(d.Box.Width < 200 && d.Box.Height < 200, "should not be the whole frame");
        // Box should enclose the drawn square (allowing detector margin + morphology growth).
        Assert.True(d.Box.Left <= 55 && d.Box.Right >= 145, $"box {d.Box.Left}..{d.Box.Right} should cover the subject");
        Assert.False(d.IntersectsTop || d.IntersectsBottom || d.IntersectsLeft || d.IntersectsRight);
        Assert.NotNull(d.MaskPng);
        Assert.NotEmpty(d.MaskPng!);
        Assert.True(d.Confidence > 0.2);
    }

    [Fact]
    public void Detect_FlatFrame_YieldsWholeFrameNoDetection() {
        using Mat img = White(200, 200);

        SubjectDetection d = new SubjectDetector(Config()).Detect(img);

        Assert.Equal(200, d.Box.Width);
        Assert.Equal(200, d.Box.Height);
        Assert.Equal(0.0, d.Confidence);
    }

    [Fact]
    public void Detect_SubjectRunningOffBottom_SetsBottomIntersect() {
        using Mat img = White(200, 200);
        Cv2.Rectangle(img, new Rect(50, 120, 100, 80), new Scalar(40, 180, 40), thickness: -1);

        SubjectDetection d = new SubjectDetector(Config()).Detect(img);

        Assert.True(d.IntersectsBottom, "subject runs off the bottom edge");
        Assert.False(d.IntersectsTop, "subject does not reach the top edge");
    }

    private static Mat White(int w, int h) => new(h, w, MatType.CV_8UC3, new Scalar(255, 255, 255));
}
