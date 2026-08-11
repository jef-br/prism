using OpenCvSharp;
using Xunit;

namespace PrismCoreTests.Transform;

/// <summary>
/// Unit tests for <see cref="Tx_DetailCropper"/>'s gravitational-anchor decision tree. Each test
/// builds a small synthetic in-memory JPEG (solid background + a drawn rectangle standing in for
/// the salient object) sized and bbox-tagged so exactly one branch of the 1/2/3/4-intersection
/// decision tree fires deterministically. Numbers are hand-verified against the anchor/margin/
/// containment formulas (margin 0.042, matching shipped <c>transform_Config.json</c>).
/// </summary>
public class Tx_DetailCropperTests {
    private const double WhiteSpaceMargin = 0.042;

    private static readonly CropTransformSettings CropCfg = new() { WhiteSpaceMargin = WhiteSpaceMargin, ShadowBottomShrinkFraction = 0.06, SubjectPromotionMinConfidence = 0.35 };
    private static readonly BgStretchConfig BgStretchCfg = new() { Tier1MaxRatio = 1.25f, Tier2MaxRatio = 1.42f, Tier4MinRatio = 2.50f, FeatherPx = 16 };
    private static readonly HeadCutterConfig HeadCutterCfg = new() { FaceHeightCutFactor = 0.75 };

    private static Tx_DetailCropper NewCropper(bool headcut = false, Mat? colorMat = null) =>
        new(headcut, colorMat, CropCfg, BgStretchCfg, HeadCutterCfg);

    //  1 intersection — touched edge anchors with margin; free axis shrinks (fits in frame)

    [Fact]
    public void Transform_OneEdge_Bottom_FreeAxisShrinks() {
        // img 1000x1000, bbox L300 T400 R700 B1000 (touches bottom only, H=600).
        // Touched axis: targetExtent=round(600*1.042)=625, start=1000-625=375 -> side=625.
        // Free axis centered on bbox X-center=500: start=500-625/2=188, fits within [0,1000].
        byte[] jpeg = MakeJpeg(1000, 1000);
        BoundingBox bbox = MakeBox(300, 400, 400, 600);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, bottom: true);

        Tx_DetailCropper cropper = NewCropper();
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.OutputRecord!.TransformStatus);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.OutputRecord.TransformerType);
        Assert.Equal(625, lambda.OutputRecord.OutputWidth);
        Assert.Equal(625, lambda.OutputRecord.OutputHeight);
        Assert.Equal(string.Empty, lambda.OutputRecord.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 625);
    }

    //  1 intersection — free axis must extend (bbox does not fit centered within the frame)

    [Fact]
    public void Transform_OneEdge_Top_FreeAxisExtends() {
        // img 700x1000, bbox L250 T0 R450 B300 (touches top only, H=300).
        // Touched axis: targetExtent=round(300*1.042)=313, start=0 -> side=313.
        // Free axis centered on bbox X-center=350: start=350-313/2=194, extent=313, fits within
        // [0,700] here (313 < 700) -- use a narrower frame to force extension instead.
        byte[] jpeg = MakeJpeg(200, 1000);
        BoundingBox bbox = MakeBox(0, 0, 200, 300); // touches top; X-center=100
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true);

        // Touched axis: targetExtent=round(300*1.042)=313, start=0 -> side=313.
        // Free axis centered on X-center=100: start=100-313/2=-56 -> does not fit in [0,200] ->
        // extend. Available crop: start=0, extent=min(200,-56+313)-0=200. srcOffset(extendTowardStart
        // is true for CenteredAxis) = side-availableExtent=313-200=113.
        Tx_DetailCropper cropper = NewCropper();
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.OutputRecord!.TransformStatus);
        Assert.Equal(313, lambda.OutputRecord.OutputWidth);
        Assert.Equal(313, lambda.OutputRecord.OutputHeight);
        Assert.Equal("background-stretch", lambda.OutputRecord.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 313);
    }

    //  2 opposing edges — free axis shrinks (fits in frame)

    [Fact]
    public void Transform_TwoOpposing_FreeAxisShrinks() {
        // img 1000x1200, bbox L300 T0 R700 B1200 (touches top+bottom). Pinned side=imgW=1000.
        // Free axis (vertical) centered on frame center 600: start=600-500=100, fits [0,1200].
        byte[] jpeg = MakeJpeg(1000, 1200);
        BoundingBox bbox = MakeBox(300, 0, 400, 1200);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, bottom: true);

        Tx_DetailCropper cropper = NewCropper();
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.OutputRecord!.TransformStatus);
        Assert.Equal(1000, lambda.OutputRecord.OutputWidth);
        Assert.Equal(1000, lambda.OutputRecord.OutputHeight);
        Assert.Equal(string.Empty, lambda.OutputRecord.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 1000);
    }

    //  2 opposing edges — free axis must extend symmetrically

    [Fact]
    public void Transform_TwoOpposing_FreeAxisExtendsSymmetrically() {
        // img 1000x800, bbox L300 T0 R700 B800 (touches top+bottom). Pinned side=imgW=1000.
        // Free axis (vertical) centered on frame center 400: start=400-500=-100 -> does not fit
        // [0,800] -> extend. Available crop: start=0, extent=min(800,-100+1000)-0=800.
        // srcOffset(symmetric)=(1000-800)/2=100.
        byte[] jpeg = MakeJpeg(1000, 800);
        BoundingBox bbox = MakeBox(300, 0, 400, 800);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, bottom: true);

        Tx_DetailCropper cropper = NewCropper();
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.OutputRecord!.TransformStatus);
        Assert.Equal(1000, lambda.OutputRecord.OutputWidth);
        Assert.Equal(1000, lambda.OutputRecord.OutputHeight);
        Assert.Equal("background-stretch", lambda.OutputRecord.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 1000);

        // Symmetric extension: the source content (rows [0,800) of the crop) should land centered
        // in the output, i.e. rows [100,900) -- verify via a banded image in a dedicated test below.
    }

    [Fact]
    public void Transform_TwoOpposing_ExtendsSymmetrically_BandLandsCentered() {
        // Crop (rows [0,800) of the source, unshifted since the free axis fits shrink-wise up to
        // its own extent) lands at output rows [100,900) once extended to the 1000-tall canvas.
        // A band only in the source's middle third ([300,500)) marks known-content rows so the
        // extended top/bottom margins (white background, not part of the band) are distinguishable.
        byte[] jpeg = MakeBandedJpeg(1000, 800, bandTop: 300, bandBottom: 500);
        BoundingBox bbox = MakeBox(300, 0, 400, 800);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, bottom: true);

        Tx_DetailCropper cropper = NewCropper();
        cropper.Transform(lambda);

        using Mat result = Cv2.ImDecode(lambda.ProcessedBytes!, ImreadModes.Color);
        Assert.True(IsWhite(result, row: 450), "Expected source band centered in the output (300+100=400 to 500+100=600).");
        Assert.True(IsBlack(result, row: 20), "Expected background fill above the shifted band.");
        Assert.True(IsBlack(result, row: 980), "Expected background fill below the shifted band.");
    }

    //  2 adjacent edges — both axes already square (pure crop, no extension)

    [Fact]
    public void Transform_TwoAdjacent_TopLeft_AlreadySquare_NoExtension() {
        byte[] jpeg = MakeJpeg(1000, 800);
        BoundingBox bbox = MakeBox(0, 0, 300, 300); // touches top+left, W=H=300
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, left: true);

        Tx_DetailCropper cropper = NewCropper();
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.OutputRecord!.TransformStatus);
        Assert.Equal(300, lambda.OutputRecord.OutputWidth);
        Assert.Equal(300, lambda.OutputRecord.OutputHeight);
        Assert.Equal(string.Empty, lambda.OutputRecord.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 300);
    }

    //  2 adjacent edges — shorter axis extends away from the anchor corner (four corner rotations)

    [Theory]
    [InlineData(true, true)]   // top-left
    [InlineData(true, false)]  // top-right
    [InlineData(false, true)]  // bottom-left
    [InlineData(false, false)] // bottom-right
    public void Transform_TwoAdjacent_ShorterAxisExtendsAwayFromCorner(bool top, bool left) {
        // img 500x800, bbox W=300 H=600 anchored at the requested corner -> side=max(300,600)=600.
        // Width (300) must grow to 600, but the 500-wide frame cannot hold a flush-anchored 600px
        // window (600 > 500) -> extension via Tx_util_BgStretch is required, not just an enlarged crop.
        int bboxX = left ? 0 : 200;
        int bboxY = top ? 0 : 200;
        byte[] jpeg = MakeJpeg(500, 800);
        BoundingBox bbox = MakeBox(bboxX, bboxY, 300, 600);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: top, bottom: !top, left: left, right: !left);

        Tx_DetailCropper cropper = NewCropper();
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.OutputRecord!.TransformStatus);
        Assert.Equal(600, lambda.OutputRecord.OutputWidth);
        Assert.Equal(600, lambda.OutputRecord.OutputHeight);
        Assert.Equal("background-stretch", lambda.OutputRecord.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 600);
    }

    [Fact]
    public void Transform_TwoAdjacent_TopLeft_AnchorCornerPixelsDoNotMove() {
        // Band across the anchor corner's flush rows must remain at the same output rows: the
        // width axis (300->600) grows rightward (away from the left anchor), the height axis stays
        // put, so a horizontal band near the top-left corner should not shift vertically.
        byte[] jpeg = MakeBandedJpeg(1000, 800, bandTop: 0, bandBottom: 100);
        BoundingBox bbox = MakeBox(0, 0, 300, 600); // touches top+left
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, left: true);

        Tx_DetailCropper cropper = NewCropper();
        cropper.Transform(lambda);

        using Mat result = Cv2.ImDecode(lambda.ProcessedBytes!, ImreadModes.Color);
        Assert.True(IsWhite(result, row: 50), "Expected the band to remain flush at the top (anchor edge, unmoved).");
    }

    //  3 edges — open axis shrinks (fits in frame)

    [Fact]
    public void Transform_ThreeEdges_OpenBottom_Shrinks() {
        // img 1000x1200, bbox L0 T0 R1000 B300 (top+left+right touched, open bottom). Pinned
        // side=imgW=1000. Open axis anchored flush at top (touches top): start=0, extent=1000,
        // fits within [0,1200].
        byte[] jpeg = MakeJpeg(1000, 1200);
        BoundingBox bbox = MakeBox(0, 0, 1000, 300);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, left: true, right: true);

        Tx_DetailCropper cropper = NewCropper();
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.OutputRecord!.TransformStatus);
        Assert.Equal(1000, lambda.OutputRecord.OutputWidth);
        Assert.Equal(1000, lambda.OutputRecord.OutputHeight);
        Assert.Equal(string.Empty, lambda.OutputRecord.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 1000);
    }

    //  3 edges — open axis must extend

    [Fact]
    public void Transform_ThreeEdges_OpenBottom_Extends() {
        // img 1000x900, bbox L0 T0 R1000 B400 (top+left+right touched, open bottom). Pinned
        // side=imgW=1000. Open axis flush at top: start=0, extent=1000 -> does not fit [0,900] ->
        // extend. Available crop: start=0, extent=900. Not symmetric, extendTowardStart=false
        // (touches top -> flush at start -> ExtendTowardStart=!true=false) -> srcOffset=0.
        byte[] jpeg = MakeJpeg(1000, 900);
        BoundingBox bbox = MakeBox(0, 0, 1000, 400);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, left: true, right: true);

        Tx_DetailCropper cropper = NewCropper();
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.OutputRecord!.TransformStatus);
        Assert.Equal(1000, lambda.OutputRecord.OutputWidth);
        Assert.Equal(1000, lambda.OutputRecord.OutputHeight);
        Assert.Equal("background-stretch", lambda.OutputRecord.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 1000);
    }

    [Theory]
    [InlineData(false, true, true, true)]  // open top (bottom+left+right touched)
    [InlineData(true, false, true, true)]  // open bottom (top+left+right touched)
    [InlineData(true, true, false, true)]  // open left (top+bottom+right touched)
    [InlineData(true, true, true, false)]  // open right (top+bottom+left touched)
    public void Transform_ThreeEdges_AllOpenSideRotations_ProduceSquare(bool top, bool bottom, bool left, bool right) {
        byte[] jpeg = MakeJpeg(1000, 1000);
        BoundingBox bbox = MakeBox(0, 0, 1000, 1000);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: top, bottom: bottom, left: left, right: right);

        Tx_DetailCropper cropper = NewCropper();
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.OutputRecord!.TransformStatus);
        AssertSquareJpeg(lambda.ProcessedBytes!, lambda.OutputRecord.OutputWidth!.Value);
    }

    //  4 edges — always a centered local square crop, no extension

    [Fact]
    public void Transform_FourEdges_NoExtension() {
        byte[] jpeg = MakeJpeg(1000, 800);
        BoundingBox bbox = MakeBox(0, 0, 1000, 800);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, bottom: true, left: true, right: true);

        Tx_DetailCropper cropper = NewCropper();
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.OutputRecord!.TransformStatus);
        Assert.Equal(800, lambda.OutputRecord.OutputWidth);
        Assert.Equal(800, lambda.OutputRecord.OutputHeight);
        Assert.Equal(string.Empty, lambda.OutputRecord.BackgroundFillMethod);
        Assert.Contains("4-edge", lambda.OutputRecord.Warnings[0]);
        AssertSquareJpeg(lambda.ProcessedBytes!, 800);
    }

    //  ProcessedBytes null -> Ko

    [Fact]
    public void Transform_NullProcessedBytes_ReturnsKo() {
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "img.jpg", Width = 1000, Height = 1000 };
        lambda.BoundingBox = MakeBox(0, 0, 500, 500);

        Tx_DetailCropper cropper = NewCropper();
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ko, lambda.OutputRecord!.TransformStatus);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.OutputRecord.TransformerType);
    }

    //  Process() parity — self-derived bbox, no lambda (degenerate 0-intersection full-frame case)

    [Fact]
    public void Process_NoLambda_FullFrameBbox_TreatedAsFourEdges() {
        // With no lambda, Process() self-derives a full-frame bbox whose edges all touch the
        // frame (Left<=0, Right>=imgW, Top<=0, Bottom>=imgH) -- a 4-intersection pattern, so this
        // exercises FourEdges: a centered square crop at the smaller original dimension.
        byte[] jpeg = MakeJpeg(1000, 800);

        Tx_DetailCropper cropper = NewCropper();
        byte[] result = cropper.Process(jpeg, stride: 0, upscale_factor: 0f, lambda: null);

        AssertSquareJpeg(result, 800);
    }

    [Fact]
    public void Process_WithLambda_MatchesTransformDecision() {
        byte[] jpeg = MakeJpeg(1000, 1000);
        BoundingBox bbox = MakeBox(300, 400, 400, 600);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, bottom: true);

        Tx_DetailCropper cropper = NewCropper();
        byte[] result = cropper.Process(jpeg, stride: 0, upscale_factor: 0f, lambda: lambda);

        AssertSquareJpeg(result, 625);
    }

    //  Headcut integration

    [Fact]
    public void Transform_HeadcutRequested_CallsHeadCutterBeforeReadingBoundingBox() {
        // Tx_DetailCropper.Transform must call Tx_util_HeadCutter.Analyze first (mirroring
        // Tx_CenterAndStretch), before reading BoundingBox/ProcessedBytes, whenever headcut is
        // requested and a colorMat is available. This repo/test environment ships no Haar cascade
        // file, and Tx_util_HeadCutter.Analyze's own CascadeClassifier.Load(...) call throws
        // FileNotFoundException in that case. This test pins down that Tx_DetailCropper does
        // invoke Analyze on the headcut path -- proven by the call reaching Tx_util_HeadCutter and
        // surfacing that exact exception -- rather than silently skipping the call.
        byte[] jpeg = MakeJpeg(1000, 1000);
        BoundingBox bbox = MakeBox(300, 400, 400, 600);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, bottom: true);
        lambda.Features.Set("has-human", "true", 1.0, "test");

        using Mat colorMat = Cv2.ImDecode(jpeg, ImreadModes.Color);
        Tx_DetailCropper cropper = NewCropper(headcut: true, colorMat: colorMat);

        var ex = Assert.Throws<FileNotFoundException>(() => cropper.Transform(lambda));
        Assert.Contains("haarcascade", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Transform_HeadcutNotRequested_SkipsHeadCutterEntirely() {
        byte[] jpeg = MakeJpeg(1000, 1000);
        BoundingBox bbox = MakeBox(300, 400, 400, 600);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, bottom: true);
        lambda.Features.Set("has-human", "true", 1.0, "test");

        using Mat colorMat = Cv2.ImDecode(jpeg, ImreadModes.Color);
        Tx_DetailCropper cropper = NewCropper(headcut: false, colorMat: colorMat);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.OutputRecord!.TransformStatus);
        AssertSquareJpeg(lambda.ProcessedBytes!, 625);
    }

    //  Helpers

    /// <summary>Builds a solid-gray JPEG of the given size with a white rectangle drawn in a corner (stand-in for a salient object) and returns its encoded bytes.</summary>
    private static byte[] MakeJpeg(int width, int height) {
        using Mat mat = new(height, width, MatType.CV_8UC3, new Scalar(120, 120, 120));
        Cv2.Rectangle(mat, new Rect(0, 0, Math.Min(50, width), Math.Min(50, height)), Scalar.White, thickness: -1);
        Cv2.ImEncode(".jpg", mat, out byte[] jpeg);
        return jpeg;
    }

    /// <summary>Builds a solid-black JPEG with a solid-white horizontal band across [<paramref name="bandTop"/>, <paramref name="bandBottom"/>).</summary>
    private static byte[] MakeBandedJpeg(int width, int height, int bandTop, int bandBottom) {
        using Mat mat = new(height, width, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(0, bandTop, width, bandBottom - bandTop), Scalar.White, thickness: -1);
        Cv2.ImEncode(".jpg", mat, out byte[] jpeg);
        return jpeg;
    }

    private static bool IsWhite(Mat img, int row) => IsNear(img.Get<Vec3b>(row, img.Cols / 2), 255);
    private static bool IsBlack(Mat img, int row) => IsNear(img.Get<Vec3b>(row, img.Cols / 2), 0);
    private static bool IsNear(Vec3b px, int target) =>
        Math.Abs(px.Item0 - target) <= 15 && Math.Abs(px.Item1 - target) <= 15 && Math.Abs(px.Item2 - target) <= 15;

    private static BoundingBox MakeBox(int x, int y, int w, int h) => new() {
        X = x,
        Y = y,
        Width = w,
        Height = h,
        Left = x,
        Top = y,
        Right = x + w,
        Bottom = y + h
    };

    private static ImageRecord_LAMBDA MakeLambda(
        byte[] processedBytes, BoundingBox bbox,
        bool top = false, bool bottom = false, bool left = false, bool right = false) {
        using Mat decoded = Cv2.ImDecode(processedBytes, ImreadModes.Color);
        ImageRecord_LAMBDA lambda = new() {
            InitialFullName = "img.jpg",
            Width = decoded.Cols,
            Height = decoded.Rows,
            ProcessedBytes = processedBytes,
            BoundingBox = bbox
        };

        if (top) lambda.Features.Set("intersects-top", "true", 1.0, "test");
        if (bottom) lambda.Features.Set("intersects-bottom", "true", 1.0, "test");
        if (left) lambda.Features.Set("intersects-left", "true", 1.0, "test");
        if (right) lambda.Features.Set("intersects-right", "true", 1.0, "test");

        return lambda;
    }

    /// <summary>Decodes <paramref name="jpeg"/> and asserts it is a valid square image of the expected side.</summary>
    private static void AssertSquareJpeg(byte[] jpeg, int expectedSide) {
        using Mat decoded = Cv2.ImDecode(jpeg, ImreadModes.Color);
        Assert.False(decoded.Empty());
        Assert.Equal(expectedSide, decoded.Cols);
        Assert.Equal(expectedSide, decoded.Rows);
    }
}
