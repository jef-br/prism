using OpenCvSharp;
using Xunit;

namespace PrismCoreTests.Transform;

/// <summary>
/// Failure-path and happy-path coverage for the Configure() gate on the fixed-signature webservice
/// entry points of Tx_util_BgStretch and Tx_LowContrastEnhancement: Process() must throw before
/// Configure() and work after. Each fact resets the static config first so the assertions hold
/// regardless of test ordering (TransformService configures these classes during pipeline
/// integration runs in the same test process).
/// </summary>
// Same collection as PipelineIntegrationTests — its fixture Configure()s the same static
// fields; sharing the collection serializes the two classes and kills the reset/assert race.
[Collection("TxStaticConfig")]
public class TxConfigureGateTests {

    [Fact]
    public void BgStretch_Process_ThrowsBeforeConfigure_WorksAfter() {
        Tx_util_BgStretch.ResetConfigureForTests();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Tx_util_BgStretch.Process(TestJpeg(), 0, 1.2f));
        Assert.Contains("Configure", ex.Message);

        Tx_util_BgStretch.Configure(new BgStretchConfig {
            Tier1MaxRatio = 1.25f,
            Tier2MaxRatio = 1.42f,
            Tier4MinRatio = 2.50f,
            FeatherPx = 16
        });

        byte[] result = Tx_util_BgStretch.Process(TestJpeg(), 0, 1.2f);
        Assert.NotEmpty(result);
        using Mat decoded = Cv2.ImDecode(result, ImreadModes.Color);
        // 40 px * 1.2 = 48 px per side; Process expands the canvas, never crops.
        Assert.Equal(48, decoded.Cols);
        Assert.Equal(48, decoded.Rows);
    }

    [Fact]
    public void LowContrastEnhancement_Process_ThrowsBeforeConfigure_WorksAfter() {
        Tx_LowContrastEnhancement.ResetConfigureForTests();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Tx_LowContrastEnhancement.Process(TestJpeg(), 0, 1f));
        Assert.Contains("Configure", ex.Message);

        Tx_LowContrastEnhancement.Configure(new LowContrastEnhancementConfig {
            ClipLimit = 2.0,
            TileSize = 8
        });

        byte[] result = Tx_LowContrastEnhancement.Process(TestJpeg(), 0, 1f);
        Assert.NotEmpty(result);
        using Mat decoded = Cv2.ImDecode(result, ImreadModes.Color);
        // CLAHE alters contrast only — dimensions must be untouched.
        Assert.Equal(40, decoded.Cols);
        Assert.Equal(40, decoded.Rows);
    }

    private static byte[] TestJpeg() {
        // BGR 40x40 flat tone — enough for both entry points; content is irrelevant to the gate.
        using Mat mat = new(40, 40, MatType.CV_8UC3, new Scalar(180, 200, 220));
        Cv2.ImEncode(".jpg", mat, out byte[] jpeg);
        return jpeg;
    }
}
