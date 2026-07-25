using OpenCvSharp;
using Xunit;

namespace PrismCoreTests.Transform;

/// <summary>
/// Pixel-level tests for <see cref="Tx_CenterAndStretch"/>'s crop-resize-and-stretch algorithm.
/// The worked example in <see cref="Transform_WorkedExample_MatchesExactCanvasSize"/> matches a
/// known-good real-world reference (a production Photoshop script) exactly, including the
/// floor/round-to-even/minus-2 canvas-size formula.
/// </summary>
public class Tx_CenterAndStretchTests {
    private const double Margin = 0.042;

    // Mirrors the shipped transform_Config.json BgStretch/HeadCutter sections.
    private static readonly BgStretchConfig BgStretchCfg = new() { Tier1MaxRatio = 1.25f, Tier2MaxRatio = 1.42f, Tier4MinRatio = 2.50f, FeatherPx = 16 };
    private static readonly HeadCutterConfig HeadCutterCfg = new() { FaceHeightCutFactor = 0.75 };

    [Fact]
    public void Transform_WorkedExample_MatchesExactCanvasSize() {
        // 1500x2000 original, bbox x:400,y:100,w:400,h:1800, margin 0.042.
        // longestSide=1800 -> raw=1800*1.084=1951.2 -> floor=1951 -> round down to even=1950 -> -2 = 1948.
        byte[] jpeg = MakeJpeg(1500, 2000);
        BoundingBox bbox = MakeBox(400, 100, 400, 1800);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox);

        Tx_CenterAndStretch tx = new(Margin, headcut: false, colorMat: null, bgStretch: BgStretchCfg, headCutter: HeadCutterCfg);
        tx.Transform(lambda);

        Assert.Equal(TransformationStatus.Ok, lambda.OutputRecord!.TransformStatus);
        Assert.Equal(1948, lambda.OutputRecord.OutputWidth);
        Assert.Equal(1948, lambda.OutputRecord.OutputHeight);
        Assert.Equal("background-stretch", lambda.OutputRecord.BackgroundFillMethod);
        AssertSquareJpeg(lambda.ProcessedBytes!, 1948);
    }

    [Fact]
    public void Transform_NeverProducesNegativeStretchOffset_EvenWhenBboxIsFarOffCenter() {
        // Same bbox size/margin as the worked example above, but positioned hard against the
        // right/bottom of a much larger frame — the exact shape of bug that crashed the real
        // TinyTest pipeline (Tx_util_BgStretch's CopyMakeBorder throwing on a negative offset).
        // The crop-then-resize-then-center approach can never produce a negative offset because
        // the resized product's longer side always equals finalBboxSize, which is always strictly
        // smaller than canvasSize (margin > 0) — regardless of where the bbox sat in the original.
        byte[] jpeg = MakeJpeg(3000, 4000);
        BoundingBox bbox = MakeBox(2600, 2200, 400, 1800);   // far from the frame's own center
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox);

        Tx_CenterAndStretch tx = new(Margin, headcut: false, colorMat: null, bgStretch: BgStretchCfg, headCutter: HeadCutterCfg);
        var exception = Record.Exception(() => tx.Transform(lambda));

        Assert.Null(exception);
        Assert.Equal(TransformationStatus.Ok, lambda.OutputRecord!.TransformStatus);
        Assert.Equal(1948, lambda.OutputRecord.OutputWidth);
        AssertSquareJpeg(lambda.ProcessedBytes!, 1948);
    }

    [Fact]
    public void Transform_ResizesProductToFitMarginAdjustedSize() {
        // scaleFactor = finalBboxSize/longestSide = (1948*(1-0.084))/1800 = 1784.368/1800 = 0.991316...
        // The reported ScaleFactor must reflect the real resize applied to the cropped product,
        // and ResizeMode must reflect that this was a (very slight) downscale.
        byte[] jpeg = MakeJpeg(1500, 2000);
        BoundingBox bbox = MakeBox(400, 100, 400, 1800);
        ImageRecord_LAMBDA lambda = MakeLambda(jpeg, bbox);

        Tx_CenterAndStretch tx = new(Margin, headcut: false, colorMat: null, bgStretch: BgStretchCfg, headCutter: HeadCutterCfg);
        tx.Transform(lambda);

        Assert.Equal("downscale", lambda.OutputRecord!.ResizeMode);
        Assert.InRange(lambda.OutputRecord.ScaleFactor, 0.99, 0.992);
    }

    //  Helpers

    private static byte[] MakeJpeg(int width, int height) {
        using Mat mat = new(height, width, MatType.CV_8UC3, new Scalar(120, 120, 120));
        Cv2.Rectangle(mat, new Rect(0, 0, Math.Min(50, width), Math.Min(50, height)), Scalar.White, thickness: -1);
        Cv2.ImEncode(".jpg", mat, out byte[] jpeg);
        return jpeg;
    }

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

    private static ImageRecord_LAMBDA MakeLambda(byte[] processedBytes, BoundingBox bbox) {
        using Mat decoded = Cv2.ImDecode(processedBytes, ImreadModes.Color);
        return new ImageRecord_LAMBDA {
            InitialFullName = "img.jpg",
            Width = decoded.Cols,
            Height = decoded.Rows,
            ProcessedBytes = processedBytes,
            BoundingBox = bbox
        };
    }

    private static void AssertSquareJpeg(byte[] jpeg, int expectedSide) {
        using Mat decoded = Cv2.ImDecode(jpeg, ImreadModes.Color);
        Assert.False(decoded.Empty());
        Assert.Equal(expectedSide, decoded.Cols);
        Assert.Equal(expectedSide, decoded.Rows);
    }
}
