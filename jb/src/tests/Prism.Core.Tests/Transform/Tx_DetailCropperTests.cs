using OpenCvSharp;
using Xunit;

namespace PrismCoreTests.Transform;

/// <summary>
/// Unit tests for <see cref="Tx_DetailCropper"/>'s pixel-level decision tree. Each test builds a
/// small synthetic in-memory JPEG (solid background + a drawn rectangle standing in for the
/// salient object) sized and bbox-tagged so exactly one branch of the 0/1/2/3/4-edge decision
/// tree fires deterministically. Config values match the shipped <c>Prism_Config.json</c>
/// (Coverage 0.8, OneSided 0.14, BiDirectional 0.25).
/// </summary>
public class Tx_DetailCropperTests
{
    private const double Coverage      = 0.8;
    private const double OneSided      = 0.14;
    private const double BiDirectional = 0.25;

    //  0 edges — greedy crop, Coverage floor

    [Fact]
    public void Transform_ZeroEdges_ProducesCoverageFloorCrop()
    {
        byte[] jpeg = MakeJpeg(1000, 1000);
        BoundingBox bbox = MakeBox(350, 350, 300, 300);   // centered, no edge touched
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        // side = ceil(sqrt(1,000,000 * 0.8)) = 895 (tight bbox square of 300 removes more than
        // the 20% Coverage floor allows, so the crop falls back to the Coverage-derived side).
        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult.TransformerType);
        Assert.Equal(895, lambda.TransformationResult.OutputWidth);
        Assert.Equal(895, lambda.TransformationResult.OutputHeight);
        Assert.Equal(string.Empty, lambda.TransformationResult.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 895);
    }

    //  1 edge — pinned axis crops toward bbox, free axis extends via Tx_util_BgStretch

    [Fact]
    public void Transform_OneEdge_Top_ExtendsFreeAxis()
    {
        // Narrow frame (700x1000) with the bbox pinned to the top: the vertical axis cannot move,
        // and the ideal square side (749) exceeds the original width (700), forcing a horizontal
        // extension through Tx_util_BgStretch.
        byte[] jpeg = MakeJpeg(700, 1000);
        BoundingBox bbox = MakeBox(250, 0, 200, 300);     // Y=0 touches top only
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult.TransformerType);
        Assert.Equal(749, lambda.TransformationResult.OutputWidth);
        Assert.Equal(749, lambda.TransformationResult.OutputHeight);
        Assert.Equal("background-stretch", lambda.TransformationResult.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 749);
    }

    //  1 edge — Right-touched: regression test for a coordinate-shift bug where the free axis'
    //  center was incorrectly adjusted by the pinned axis' crop offset (pinnedFixedStart), which
    //  only manifests when the touched edge is Bottom/Right (pinnedFixedStart != 0) and the
    //  Coverage-derived side is smaller than the pinned axis' original extent. A Top/Left-touched
    //  case alone (as in the test above) cannot catch this, since pinnedFixedStart is always 0 there.

    [Fact]
    public void Transform_OneEdge_Right_ExtendsFreeAxisSymmetricallyAroundBboxCenter()
    {
        // 1000x700 frame, bbox touches the right edge only, centered vertically (Y-center=350,
        // the exact vertical midpoint of the 700-tall frame). idealSide=749 exceeds the free
        // (vertical) axis' extent of 700, forcing an extension that must land symmetrically
        // (srcY=24) around Y=350 — not shifted by the pinned axis' 251px crop offset
        // (1000-749=251), which was the bug (the buggy srcY=275 would place source rows
        // [200,500) at output rows [475,775), overshooting the 749-tall canvas entirely and
        // leaving the canvas center, row 374, outside the band — i.e. black, not white).
        //
        // Source image: solid black, with a solid white band across rows [200,500) — matching
        // the bbox's Y-range — so the band's landing position in the output directly reveals
        // whether centering used the correct (unshifted) or buggy (shifted) srcY.
        byte[] jpeg = MakeBandedJpeg(1000, 700, bandTop: 200, bandBottom: 500);
        BoundingBox bbox = MakeBox(800, 200, 200, 300);   // X=[800,1000) touches right; Y-center=350
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, right: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(749, lambda.TransformationResult.OutputWidth);
        Assert.Equal(749, lambda.TransformationResult.OutputHeight);
        Assert.Equal("background-stretch", lambda.TransformationResult.BackgroundFillMethod);

        // Correctly centered: the white band [200,500) shifts by srcY=24 to land at [224,524) —
        // comfortably containing the canvas center row (374). A regression of the fixed bug would
        // shift the band far past the canvas (buggy srcY=275 => band at [475,775), outside [0,749)),
        // leaving row 374 black instead of white.
        using Mat result = Cv2.ImDecode(lambda.ProcessedBytes!, ImreadModes.Color);
        Assert.True(IsWhite(result, row: 374), "Expected the source band to be centered on the canvas midpoint.");
        Assert.True(IsBlack(result, row: 20), "Expected background near the top edge, outside the shifted band.");
        Assert.True(IsBlack(result, row: 728), "Expected background near the bottom edge, outside the shifted band.");
    }

    //  1 edge — off-center bbox on the free axis: regression test for a second coordinate bug
    //  found by code review, where the extension offset (side/2 - freeCenter) was never clamped
    //  to the valid placement range. When the bbox sits closer to one end of the free axis than
    //  the canvas's own half-width, the ideal (unclamped) offset goes negative, crashing
    //  Tx_util_BgStretch the same way the already-fixed Tx_CenterAndStretch bug did.

    [Fact]
    public void Transform_OneEdge_Top_OffCenterBboxOnFreeAxis_ClampsInsteadOfGoingNegative()
    {
        // 700x1000 frame, touches top only, bbox X=[600,700) — far toward the right edge of the
        // free (horizontal) axis. idealSide=749 exceeds the free axis' 700px extent, but centering
        // the bbox exactly would require srcX = 374 - 650 = -276 (negative -> would have crashed).
        // Clamped to [0, side-freeOriginalExtent]=[0,49], the correct placement is srcX=0 (source
        // flush against the canvas' left edge, all 49px of extension on the right).
        byte[] jpeg = MakeVerticalBandedJpeg(700, 1000, bandLeft: 600, bandRight: 700);
        BoundingBox bbox = MakeBox(600, 0, 100, 300);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        var exception = Record.Exception(() => cropper.Transform(lambda));

        Assert.Null(exception);
        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(749, lambda.TransformationResult.OutputWidth);
        Assert.Equal("background-stretch", lambda.TransformationResult.BackgroundFillMethod);

        // srcX=0 means the free axis is unshifted: the white band at source columns [600,700)
        // must land at the same columns [600,700) in the output.
        using Mat result = Cv2.ImDecode(lambda.ProcessedBytes!, ImreadModes.Color);
        Assert.True(IsWhiteAt(result, row: 500, col: 650), "Expected the band to remain at its original column, unshifted (srcX clamped to 0).");
        Assert.True(IsBlackAt(result, row: 500, col: 10), "Expected background to the left of the unshifted band.");
    }

    private static byte[] MakeVerticalBandedJpeg(int width, int height, int bandLeft, int bandRight)
    {
        using Mat mat = new(height, width, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(bandLeft, 0, bandRight - bandLeft, height), Scalar.White, thickness: -1);
        Cv2.ImEncode(".jpg", mat, out byte[] jpeg);
        return jpeg;
    }

    private static bool IsWhiteAt(Mat img, int row, int col) => IsNear(img.Get<Vec3b>(row, col), 255);
    private static bool IsBlackAt(Mat img, int row, int col) => IsNear(img.Get<Vec3b>(row, col), 0);

    /// <summary>Builds a solid-black JPEG with a solid-white horizontal band across [<paramref name="bandTop"/>, <paramref name="bandBottom"/>).</summary>
    private static byte[] MakeBandedJpeg(int width, int height, int bandTop, int bandBottom)
    {
        using Mat mat = new(height, width, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(0, bandTop, width, bandBottom - bandTop), Scalar.White, thickness: -1);
        Cv2.ImEncode(".jpg", mat, out byte[] jpeg);
        return jpeg;
    }

    private static bool IsWhite(Mat img, int row) => IsNear(img.Get<Vec3b>(row, img.Cols / 2), 255);
    private static bool IsBlack(Mat img, int row) => IsNear(img.Get<Vec3b>(row, img.Cols / 2), 0);
    private static bool IsNear(Vec3b px, int target) =>
        Math.Abs(px.Item0 - target) <= 15 && Math.Abs(px.Item1 - target) <= 15 && Math.Abs(px.Item2 - target) <= 15;

    //  2 opposing edges — within BiDirectional budget

    [Fact]
    public void Transform_TwoOpposing_WithinBudget_ExtendsFreeAxis()
    {
        // Top+Bottom pinned at imgW=1000; free axis (height=850) needs a 17.6% extension to
        // reach 1000, under the 25% BiDirectional budget.
        byte[] jpeg = MakeJpeg(1000, 850);
        BoundingBox bbox = MakeBox(300, 0, 400, 850);     // spans full height: touches top+bottom
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, bottom: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult.TransformerType);
        Assert.Equal(1000, lambda.TransformationResult.OutputWidth);
        Assert.Equal(1000, lambda.TransformationResult.OutputHeight);
        Assert.Equal("background-stretch", lambda.TransformationResult.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 1000);
    }

    //  2 opposing edges — exceeds BiDirectional budget → local square crop fallback

    [Fact]
    public void Transform_TwoOpposing_ExceedsBudget_FallsBackToLocalSquareCrop()
    {
        // Top+Bottom pinned at imgW=1000; free axis (height=500) would need a 100% extension —
        // far beyond the 25% BiDirectional budget — so this falls back to a local square crop.
        byte[] jpeg = MakeJpeg(1000, 500);
        BoundingBox bbox = MakeBox(300, 0, 400, 500);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, bottom: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult.TransformerType);
        Assert.Equal(500, lambda.TransformationResult.OutputWidth);
        Assert.Equal(500, lambda.TransformationResult.OutputHeight);
        Assert.Equal(string.Empty, lambda.TransformationResult.BackgroundFillMethod);
        Assert.Contains("exceeds", lambda.TransformationResult.Warnings[0], StringComparison.OrdinalIgnoreCase);
        AssertSquareJpeg(lambda.ProcessedBytes!, 500);
    }

    //  2 adjacent edges — capped crop + background stretch to square

    [Fact]
    public void Transform_TwoAdjacent_TopLeft_CropThenStretchReachesSquare()
    {
        // Width (1000) is capped at a 14% reduction toward height (800): target = 860. Height
        // then stretches 800 -> 860 via Tx_util_BgStretch to reach the final square.
        byte[] jpeg = MakeJpeg(1000, 800);
        BoundingBox bbox = MakeBox(0, 0, 300, 300);       // touches top and left only
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, left: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult.TransformerType);
        Assert.Equal(860, lambda.TransformationResult.OutputWidth);
        Assert.Equal(860, lambda.TransformationResult.OutputHeight);
        Assert.Equal("background-stretch", lambda.TransformationResult.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 860);
    }

    //  3 edges — within OneSided budget

    [Fact]
    public void Transform_ThreeEdges_WithinBudget_ExtendsOpenSide()
    {
        // Top+Left+Right pinned (imgW=1000); the open bottom side (height=900) needs an 11.1%
        // extension to reach 1000, under the 14% OneSided budget.
        byte[] jpeg = MakeJpeg(1000, 900);
        BoundingBox bbox = MakeBox(0, 0, 1000, 400);      // spans full width, open at the bottom
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, left: true, right: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult.TransformerType);
        Assert.Equal(1000, lambda.TransformationResult.OutputWidth);
        Assert.Equal(1000, lambda.TransformationResult.OutputHeight);
        Assert.Equal("background-stretch", lambda.TransformationResult.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 1000);
    }

    //  3 edges — exceeds OneSided budget -> local square crop fallback

    [Fact]
    public void Transform_ThreeEdges_ExceedsBudget_FallsBackToLocalSquareCrop()
    {
        // Top+Left+Right pinned (imgW=1000); the open bottom side (height=500) would need a 100%
        // extension — far beyond the 14% OneSided budget — so this falls back to a local crop.
        byte[] jpeg = MakeJpeg(1000, 500);
        BoundingBox bbox = MakeBox(0, 0, 1000, 200);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, left: true, right: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult.TransformerType);
        Assert.Equal(500, lambda.TransformationResult.OutputWidth);
        Assert.Equal(500, lambda.TransformationResult.OutputHeight);
        Assert.Equal(string.Empty, lambda.TransformationResult.BackgroundFillMethod);
        Assert.Contains("exceeds", lambda.TransformationResult.Warnings[0], StringComparison.OrdinalIgnoreCase);
        AssertSquareJpeg(lambda.ProcessedBytes!, 500);
    }

    //  4 edges — no open side, immediate local square crop

    [Fact]
    public void Transform_FourEdges_FallsBackToLocalSquareCropImmediately()
    {
        byte[] jpeg = MakeJpeg(1000, 800);
        BoundingBox bbox = MakeBox(0, 0, 1000, 800);      // fills the whole frame
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, bottom: true, left: true, right: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult.TransformerType);
        Assert.Equal(800, lambda.TransformationResult.OutputWidth);
        Assert.Equal(800, lambda.TransformationResult.OutputHeight);
        Assert.Equal(string.Empty, lambda.TransformationResult.BackgroundFillMethod);
        Assert.Contains("4-edge", lambda.TransformationResult.Warnings[0]);
        AssertSquareJpeg(lambda.ProcessedBytes!, 800);
    }

    //  ProcessedBytes null -> Ko

    [Fact]
    public void Transform_NullProcessedBytes_ReturnsKo()
    {
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "img.jpg", Width = 1000, Height = 1000 };
        lambda.BoundingBox = MakeBox(0, 0, 500, 500);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ko, lambda.TransformationResult!.Status);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult.TransformerType);
    }

    //  Process() parity — self-derived bbox, no lambda

    [Fact]
    public void Process_NoLambda_ZeroEdgeFullFrame_ProducesCoverageFloorCrop()
    {
        // With no lambda, Tx_DetailCropper.Process() self-derives a full-frame bbox (its
        // documented degenerate 0-intersection fallback), so this exercises the 0-edge branch
        // using the same Coverage-floor math as the lambda-driven test above.
        byte[] jpeg = MakeJpeg(1000, 1000);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        byte[] result = cropper.Process(jpeg, stride: 0, upscale_factor: 0f, lambda: null);

        // idealSide = max(1000,1000) = 1000 (full-frame bbox); tight square already retains
        // 100% of the area (>= Coverage), so the full frame is kept unmodified as a 1000x1000 crop.
        AssertSquareJpeg(result, 1000);
    }

    [Fact]
    public void Process_WithLambda_MatchesTransformDecision()
    {
        // Passing the same lambda used by Transform() should produce byte-identical output —
        // Process() and Transform() share the same decision-tree helpers when lambda is supplied.
        byte[] jpeg = MakeJpeg(1000, 1000);
        BoundingBox bbox = MakeBox(350, 350, 300, 300);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        byte[] result = cropper.Process(jpeg, stride: 0, upscale_factor: 0f, lambda: lambda);

        AssertSquareJpeg(result, 895);
    }

    //  Headcut integration

    [Fact]
    public void Transform_HeadcutRequested_CallsHeadCutterBeforeReadingBoundingBox()
    {
        // Tx_DetailCropper.Transform must call Tx_util_HeadCutter.Analyze first (mirroring
        // Tx_CenterAndStretch), before reading BoundingBox/ProcessedBytes, whenever headcut is
        // requested and a colorMat is available. This repo/test environment ships no Haar cascade
        // file (jb/src/core has no haarcascade_frontalface_default.xml), and
        // Tx_util_HeadCutter.Analyze's own CascadeClassifier.Load(...) call throws
        // FileNotFoundException in that case rather than returning false as its surrounding code
        // assumes (a pre-existing gap in Tx_util_HeadCutter from ticket T-2200, out of this
        // ticket's scope to fix). This test pins down that Tx_DetailCropper does invoke Analyze
        // on the headcut path — proven by the call reaching Tx_util_HeadCutter and surfacing that
        // exact exception — rather than silently skipping the call.
        byte[] jpeg = MakeJpeg(1000, 1000);
        BoundingBox bbox = MakeBox(350, 350, 300, 300);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox);
        lambda.Features.Set("has-human", "true", 1.0, "test");

        using Mat colorMat = Cv2.ImDecode(jpeg, ImreadModes.Color);
        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: true, colorMat: colorMat);

        var ex = Assert.Throws<FileNotFoundException>(() => cropper.Transform(lambda));
        Assert.Contains("haarcascade", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Transform_HeadcutNotRequested_SkipsHeadCutterEntirely()
    {
        // headcut: false must skip Tx_util_HeadCutter.Analyze entirely — no exception, even
        // though the same environment has no Haar cascade file available.
        byte[] jpeg = MakeJpeg(1000, 1000);
        BoundingBox bbox = MakeBox(350, 350, 300, 300);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox);
        lambda.Features.Set("has-human", "true", 1.0, "test");

        using Mat colorMat = Cv2.ImDecode(jpeg, ImreadModes.Color);
        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: colorMat);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        AssertSquareJpeg(lambda.ProcessedBytes!, 895);
    }

    //  1 edge — Bottom and Left (pinned axis always safe cases, but test for regression)

    [Fact]
    public void Transform_OneEdge_Bottom_ExtendsFreeAxis()
    {
        // Narrow frame (700x1000) with the bbox pinned to the bottom: the vertical axis cannot move,
        // and the ideal square side (749) exceeds the original width (700), forcing a horizontal
        // extension through Tx_util_BgStretch.
        byte[] jpeg = MakeJpeg(700, 1000);
        BoundingBox bbox = MakeBox(250, 700, 200, 300); // Y=[700,1000) touches bottom only
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, bottom: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult.TransformerType);
        Assert.Equal(749, lambda.TransformationResult.OutputWidth);
        Assert.Equal(749, lambda.TransformationResult.OutputHeight);
        Assert.Equal("background-stretch", lambda.TransformationResult.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 749);
    }

    [Fact]
    public void Transform_OneEdge_Left_ExtendsFreeAxis()
    {
        // Narrow frame (1000x700) with the bbox pinned to the left: the horizontal axis cannot move,
        // and the ideal square side (749) exceeds the original height (700), forcing a vertical
        // extension through Tx_util_BgStretch.
        byte[] jpeg = MakeJpeg(1000, 700);
        BoundingBox bbox = MakeBox(0, 250, 300, 200);  // X=[0,300) touches left only
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, left: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult.TransformerType);
        Assert.Equal(749, lambda.TransformationResult.OutputWidth);
        Assert.Equal(749, lambda.TransformationResult.OutputHeight);
        Assert.Equal("background-stretch", lambda.TransformationResult.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 749);
    }

    //  2 adjacent edges — three untested corners

    [Fact]
    public void Transform_TwoAdjacent_TopRight_CropThenStretchReachesSquare()
    {
        // Height (1000) is capped at a 14% reduction toward width (800): target = 860. Width
        // then stretches 800 -> 860 via Tx_util_BgStretch to reach the final square.
        byte[] jpeg = MakeJpeg(800, 1000);
        BoundingBox bbox = MakeBox(500, 0, 300, 300);  // touches top and right only
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, right: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult.TransformerType);
        Assert.Equal(860, lambda.TransformationResult.OutputWidth);
        Assert.Equal(860, lambda.TransformationResult.OutputHeight);
        Assert.Equal("background-stretch", lambda.TransformationResult.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 860);
    }

    [Fact]
    public void Transform_TwoAdjacent_BottomLeft_CropThenStretchReachesSquare()
    {
        // Height (1000) is capped at a 14% reduction toward width (800): target = 860. Width
        // then stretches 800 -> 860 via Tx_util_BgStretch to reach the final square.
        byte[] jpeg = MakeJpeg(800, 1000);
        BoundingBox bbox = MakeBox(0, 700, 300, 300);  // touches bottom and left only
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, bottom: true, left: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult.TransformerType);
        Assert.Equal(860, lambda.TransformationResult.OutputWidth);
        Assert.Equal(860, lambda.TransformationResult.OutputHeight);
        Assert.Equal("background-stretch", lambda.TransformationResult.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 860);
    }

    [Fact]
    public void Transform_TwoAdjacent_BottomRight_CropThenStretchReachesSquare()
    {
        // Height (1000) is capped at a 14% reduction toward width (800): target = 860. Width
        // then stretches 800 -> 860 via Tx_util_BgStretch to reach the final square.
        byte[] jpeg = MakeJpeg(800, 1000);
        BoundingBox bbox = MakeBox(500, 700, 300, 300); // touches bottom and right only
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, bottom: true, right: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult.TransformerType);
        Assert.Equal(860, lambda.TransformationResult.OutputWidth);
        Assert.Equal(860, lambda.TransformationResult.OutputHeight);
        Assert.Equal("background-stretch", lambda.TransformationResult.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 860);
    }

    [Fact]
    public void Transform_TwoAdjacent_TopRight_AnchorCornerDoesNotMove()
    {
        // Verify the anchor corner (top-right in this case) stays fixed while the opposite corner
        // crops/stretches. Use banded image to confirm pixel positioning. The band is placed in the
        // top-right quadrant to be within the anchor zone after crop.
        byte[] jpeg = MakeBandedJpeg(1000, 700, bandTop: 0, bandBottom: 100);
        BoundingBox bbox = MakeBox(800, 0, 200, 100);  // touches top and right only
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, right: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        // After crop (Step 1): frame goes from 1000x700 to target=860x700, cropped from the left
        // (anchorLeft=false). After stretch (Step 2): stretched to 860x860. The band at rows [0,100)
        // should remain in the top rows of the output, anchored at the top (anchorTop=true).
        using Mat result = Cv2.ImDecode(lambda.ProcessedBytes!, ImreadModes.Color);
        Assert.True(IsWhite(result, row: 50), "Expected the source band to be at the top of the output.");
        Assert.True(IsBlack(result, row: 800), "Expected background near the bottom edge (away from anchor).");
    }

    //  3 edges — three untested open-side rotations, within budget

    [Fact]
    public void Transform_ThreeEdges_OpenTop_WithinBudget_ExtendsOpenSide()
    {
        // Bottom+Left+Right pinned (imgW=1000); the open top side (height=900) needs an 11.1%
        // extension to reach 1000, under the 14% OneSided budget.
        byte[] jpeg = MakeJpeg(1000, 900);
        BoundingBox bbox = MakeBox(0, 500, 1000, 400); // spans full width, open at the top
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, bottom: true, left: true, right: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult.TransformerType);
        Assert.Equal(1000, lambda.TransformationResult.OutputWidth);
        Assert.Equal(1000, lambda.TransformationResult.OutputHeight);
        Assert.Equal("background-stretch", lambda.TransformationResult.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 1000);
    }

    [Fact]
    public void Transform_ThreeEdges_OpenLeft_WithinBudget_ExtendsOpenSide()
    {
        // Top+Bottom+Right pinned (imgW=1000); the open left side (width=900) needs an 11.1%
        // extension to reach 1000, under the 14% OneSided budget.
        byte[] jpeg = MakeJpeg(900, 1000);
        BoundingBox bbox = MakeBox(400, 0, 400, 1000); // spans full height, open at the left
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, bottom: true, right: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult.TransformerType);
        Assert.Equal(1000, lambda.TransformationResult.OutputWidth);
        Assert.Equal(1000, lambda.TransformationResult.OutputHeight);
        Assert.Equal("background-stretch", lambda.TransformationResult.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 1000);
    }

    [Fact]
    public void Transform_ThreeEdges_OpenRight_WithinBudget_ExtendsOpenSide()
    {
        // Top+Bottom+Left pinned (imgW=1000); the open right side (width=900) needs an 11.1%
        // extension to reach 1000, under the 14% OneSided budget.
        byte[] jpeg = MakeJpeg(900, 1000);
        BoundingBox bbox = MakeBox(0, 0, 400, 1000);  // spans full height, open at the right
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, bottom: true, left: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult.TransformerType);
        Assert.Equal(1000, lambda.TransformationResult.OutputWidth);
        Assert.Equal(1000, lambda.TransformationResult.OutputHeight);
        Assert.Equal("background-stretch", lambda.TransformationResult.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 1000);
    }

    //  3 edges — exceeds OneSided budget, other open-side rotation

    [Fact]
    public void Transform_ThreeEdges_OpenLeft_ExceedsBudget_FallsBackToLocalSquareCrop()
    {
        // Top+Bottom+Right pinned (imgW=1000); the open left side (width=500) would need a 100%
        // extension — far beyond the 14% OneSided budget — so this falls back to a local crop.
        byte[] jpeg = MakeJpeg(500, 1000);
        BoundingBox bbox = MakeBox(100, 0, 400, 1000);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, bottom: true, right: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult.TransformerType);
        Assert.Equal(500, lambda.TransformationResult.OutputWidth);
        Assert.Equal(500, lambda.TransformationResult.OutputHeight);
        Assert.Equal(string.Empty, lambda.TransformationResult.BackgroundFillMethod);
        Assert.Contains("exceeds", lambda.TransformationResult.Warnings[0], StringComparison.OrdinalIgnoreCase);
        AssertSquareJpeg(lambda.ProcessedBytes!, 500);
    }

    //  Boundary-exact extension ratios (at the boundary of OneSided and BiDirectional budgets)

    [Fact]
    public void Transform_ThreeEdges_ExtensionRatioJustUnderOneSidedBoundary_Extends()
    {
        // Test that extension works when the ratio is just under (not at) the OneSided boundary (0.14).
        // pinnedSide (width) = 1000; currentOpenSide (height) = 890; delta = 110;
        // ratio = 110/890 ≈ 0.1236, which is < 0.14. This should extend.
        byte[] jpeg = MakeJpeg(1000, 890);
        BoundingBox bbox = MakeBox(0, 100, 1000, 400); // open at the top, ideal side = 1000 (tight)
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, bottom: true, left: true, right: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(1000, lambda.TransformationResult.OutputWidth);
        Assert.Equal(1000, lambda.TransformationResult.OutputHeight);
        Assert.Equal("background-stretch", lambda.TransformationResult.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 1000);
    }

    [Fact]
    public void Transform_TwoOpposing_ExtensionRatioExactlyAtBiDirectionalBoundary_Extends()
    {
        // Construct a case where delta/currentFreeSide = exactly 0.25 (the BiDirectional threshold).
        // pinnedSide (width) = 1000; currentFreeSide (height) = 800; delta = 1000 - 800 = 200.
        // ratio = 200/800 = 0.25. This should extend (the <= comparison is inclusive), not fall back.
        byte[] jpeg = MakeJpeg(1000, 800);
        BoundingBox bbox = MakeBox(0, 300, 1000, 400); // spans full width, touches top+bottom
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox, top: true, bottom: true);

        Tx_DetailCropper cropper = new(Coverage, OneSided, BiDirectional, headcut: false, colorMat: null);
        cropper.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.TransformationResult!.Status);
        Assert.Equal(1000, lambda.TransformationResult.OutputWidth);
        Assert.Equal(1000, lambda.TransformationResult.OutputHeight);
        Assert.Equal("background-stretch", lambda.TransformationResult.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 1000);
    }

    //  Helpers

    /// <summary>Builds a solid-gray JPEG of the given size with a white rectangle drawn in a corner (stand-in for a salient object) and returns its encoded bytes.</summary>
    private static byte[] MakeJpeg(int width, int height)
    {
        using Mat mat = new(height, width, MatType.CV_8UC3, new Scalar(120, 120, 120));
        Cv2.Rectangle(mat, new Rect(0, 0, Math.Min(50, width), Math.Min(50, height)), Scalar.White, thickness: -1);
        Cv2.ImEncode(".jpg", mat, out byte[] jpeg);
        return jpeg;
    }

    private static BoundingBox MakeBox(int x, int y, int w, int h) => new()
    {
        X = x, Y = y, Width = w, Height = h,
        Left = x, Top = y, Right = x + w, Bottom = y + h
    };

    private static ImageRecord_LAMBDA MakeLambda(
        byte[] processedBytes, BoundingBox bbox,
        bool top = false, bool bottom = false, bool left = false, bool right = false)
    {
        using Mat decoded = Cv2.ImDecode(processedBytes, ImreadModes.Color);
        ImageRecord_LAMBDA lambda = new()
        {
            InitialFullName = "img.jpg",
            Width           = decoded.Cols,
            Height          = decoded.Rows,
            ProcessedBytes  = processedBytes,
            BoundingBox     = bbox
        };

        if (top)    lambda.Features.Set("intersects-top", "true", 1.0, "test");
        if (bottom) lambda.Features.Set("intersects-bottom", "true", 1.0, "test");
        if (left)   lambda.Features.Set("intersects-left", "true", 1.0, "test");
        if (right)  lambda.Features.Set("intersects-right", "true", 1.0, "test");

        return lambda;
    }

    /// <summary>Decodes <paramref name="jpeg"/> and asserts it is a valid square image of the expected side.</summary>
    private static void AssertSquareJpeg(byte[] jpeg, int expectedSide)
    {
        using Mat decoded = Cv2.ImDecode(jpeg, ImreadModes.Color);
        Assert.False(decoded.Empty());
        Assert.Equal(expectedSide, decoded.Cols);
        Assert.Equal(expectedSide, decoded.Rows);
    }
}
