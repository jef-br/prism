using OpenCvSharp;
using Xunit;

namespace PrismCoreTests.Transform;

/// <summary>
/// T-4805: the standalone Process entry must honor the IImageTransformation contract — when a lambda
/// with a detected bbox is supplied it uses that bbox (matching the pipeline Transform), and only a
/// lambda-less caller falls back to the whole frame. Regression guard against the prior divergence
/// where Process always cropped to FullImageBounds and ignored the lambda.
/// </summary>
public class Tx_CenterAndStretchProcessTests {
    private static readonly BgStretchConfig BgStretch = new() { Tier1MaxRatio = 1.25f, Tier2MaxRatio = 1.42f, Tier4MinRatio = 2.50f, FeatherPx = 16 };
    private static readonly HeadCutterConfig HeadCutter = new() { FaceHeightCutFactor = 0.75 };
    private const double Margin = 0.042;

    [Fact]
    public void Process_WithLambdaBbox_CanvasDerivedFromBbox_NotFullFrame() {
        Tx_CenterAndStretch tx = new(Margin, headcut: false, colorMat: null, BgStretch, HeadCutter);
        byte[] jpeg = FlatJpeg(100, 100);

        ImageRecord_LAMBDA lambda = new() {
            InitialFullName = "img.jpg",
            Width = 100,
            Height = 100,
            BoundingBox = new BoundingBox { X = 25, Y = 25, Width = 50, Height = 50, Left = 25, Top = 25, Right = 75, Bottom = 75 }
        };

        byte[] withBbox = tx.Process(jpeg, 0, 1f, lambda);
        byte[] fullFrame = tx.Process(jpeg, 0, 1f, null);

        using Mat withBboxMat = Cv2.ImDecode(withBbox, ImreadModes.Color);
        using Mat fullFrameMat = Cv2.ImDecode(fullFrame, ImreadModes.Color);

        // canvasSize = (floor(longestSide*(1+2*margin)) rounded down to even) - 2.
        // bbox longest side 50 → 52; full frame longest side 100 → 106.
        Assert.Equal(52, withBboxMat.Cols);
        Assert.Equal(106, fullFrameMat.Cols);
    }

    private static byte[] FlatJpeg(int w, int h) {
        using Mat mat = new(h, w, MatType.CV_8UC3, new Scalar(180, 200, 220));
        Cv2.ImEncode(".jpg", mat, out byte[] jpeg);
        return jpeg;
    }
}
