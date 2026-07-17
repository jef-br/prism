using OpenCvSharp;
using Xunit;

namespace PrismCoreTests.Transform;

/// <summary>
/// Coverage for the fixed-signature webservice entry points of Tx_util_BgStretch and
/// Tx_LowContrastEnhancement. Both now load their own section from transform_Config.json on use, so
/// Process() works with no prior setup call from any caller — no static push-in, no ordering
/// dependency on TransformService having run first.
/// </summary>
public class TxWebserviceEntryPointTests {

    [Fact]
    public void BgStretch_Process_LoadsOwnConfig_AndExpandsCanvas() {
        byte[] result = Tx_util_BgStretch.Process(TestJpeg(), 0, 1.2f);

        Assert.NotEmpty(result);
        using Mat decoded = Cv2.ImDecode(result, ImreadModes.Color);
        // 40 px * 1.2 = 48 px per side; Process expands the canvas, never crops.
        Assert.Equal(48, decoded.Cols);
        Assert.Equal(48, decoded.Rows);
    }

    [Fact]
    public void LowContrastEnhancement_Process_LoadsOwnConfig_AndPreservesDimensions() {
        byte[] result = Tx_LowContrastEnhancement.Process(TestJpeg(), 0, 1f);

        Assert.NotEmpty(result);
        using Mat decoded = Cv2.ImDecode(result, ImreadModes.Color);
        // CLAHE alters contrast only — dimensions must be untouched.
        Assert.Equal(40, decoded.Cols);
        Assert.Equal(40, decoded.Rows);
    }

    private static byte[] TestJpeg() {
        // BGR 40x40 flat tone — enough for both entry points; content is irrelevant here.
        using Mat mat = new(40, 40, MatType.CV_8UC3, new Scalar(180, 200, 220));
        Cv2.ImEncode(".jpg", mat, out byte[] jpeg);
        return jpeg;
    }
}
