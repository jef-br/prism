using OpenCvSharp;

namespace Prism.Services.Transform;

/// <summary>
/// Applies CLAHE (Contrast Limited Adaptive Histogram Equalization) to the full image to
/// improve foreground/background separation before bounding-box detection.
/// This is a pre-processing step for bbox accuracy — not a visual export enhancement.
/// <para>
/// CLAHE is applied to the L-channel of the LAB colour space so that colour information
/// is preserved while only luminance contrast is sharpened.
/// Tile size: 8×8 pixels. Clip limit: 2.0 (mild; avoids noise amplification).
/// </para>
/// </summary>
public static class Tx_LowContrastEnhancement {

    // CLAHE parameters — tuned for product-photography pre-processing.
    // Tile size 8×8 gives region-local contrast adaptation without grid artefacts.
    // Clip limit 2.0 suppresses noise amplification while meaningfully lifting low-contrast regions.
    private const double ClipLimit  = 2.0;
    private const int    TileSize   = 8;

    /// <summary>
    /// Webservice form: decodes <paramref name="arr"/> (JPEG, BGR), applies CLAHE,
    /// re-encodes to JPEG. <paramref name="upscale_factor"/> is accepted for interface
    /// conformance; image dimensions are not altered here.
    /// </summary>
    public static byte[] Process(byte[] arr, int stride, float upscale_factor) {
        // Input: JPEG bytes, colour space BGR (OpenCVSharp default decode).
        using Mat bgrSrc = Cv2.ImDecode(arr, ImreadModes.Color);
        if (bgrSrc.Empty()) return arr;

        using Mat bgrEnhanced = ApplyClahe(bgrSrc);

        // Output: JPEG bytes, colour space BGR.
        Cv2.ImEncode(".jpg", bgrEnhanced, out byte[] encoded);
        return encoded;
    }

    /// <summary>
    /// Sub-step form: accepts and returns JPEG <c>byte[]</c> for use as a named
    /// pipeline step inside <c>Tx_CenterAndStretch</c> and similar tools.
    /// </summary>
    internal static byte[] Enhance(byte[] sourceJpeg) {
        // Input: JPEG bytes, colour space BGR.
        using Mat bgrSrc = Cv2.ImDecode(sourceJpeg, ImreadModes.Color);
        if (bgrSrc.Empty()) return sourceJpeg;

        using Mat bgrEnhanced = ApplyClahe(bgrSrc);

        // Output: JPEG bytes, colour space BGR.
        Cv2.ImEncode(".jpg", bgrEnhanced, out byte[] encoded);
        return encoded;
    }

    // Converts BGR → LAB, applies CLAHE to the L-channel, merges, converts back to BGR.
    private static Mat ApplyClahe(Mat bgrSrc) {
        // Convert to LAB so CLAHE operates on luminance only; colour channels are unchanged.
        using Mat labSrc = new Mat();
        Cv2.CvtColor(bgrSrc, labSrc, ColorConversionCodes.BGR2Lab);

        // Split into L, a, b channels.
        Mat[] labChannels = Cv2.Split(labSrc);
        Mat channelL = labChannels[0];  // L: 0–255 in CV_8U Lab encoding
        Mat channelA = labChannels[1];
        Mat channelB = labChannels[2];

        // Apply CLAHE to the L-channel.
        // Expected input: CV_8UC1, values 0–255.
        using CLAHE clahe = Cv2.CreateCLAHE(ClipLimit, new Size(TileSize, TileSize));
        Mat channelLEnhanced = new Mat();
        clahe.Apply(channelL, channelLEnhanced);
        channelL.Dispose();

        // Merge enhanced L with original a, b channels.
        Mat labEnhanced = new Mat();
        Cv2.Merge(new Mat[] { channelLEnhanced, channelA, channelB }, labEnhanced);
        channelLEnhanced.Dispose();
        channelA.Dispose();
        channelB.Dispose();

        // Convert LAB → BGR for return.
        // Output colour space: BGR.
        Mat bgrEnhanced = new Mat();
        Cv2.CvtColor(labEnhanced, bgrEnhanced, ColorConversionCodes.Lab2BGR);
        labEnhanced.Dispose();

        return bgrEnhanced;
    }
}
