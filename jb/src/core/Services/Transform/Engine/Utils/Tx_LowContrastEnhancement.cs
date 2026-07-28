using OpenCvSharp;
using Prism.Config;

namespace Prism.Services.Transform;

/// <summary>
/// Standalone CLAHE (Contrast Limited Adaptive Histogram Equalization) utility webservice.
/// CLAHE's real job is bounding-box detection accuracy, not visual export — the detector applies it
/// to a throwaway copy upstream (see the subject-isolation detector, T-4830). This class survives only
/// as the fixed-signature <see cref="Process"/> webservice entry; there is no in-pipeline sub-step
/// caller (the former <c>Enhance</c> form was dead code, removed in T-4805).
/// <para>
/// CLAHE is applied to the L-channel of the LAB colour space so that colour information is preserved
/// while only luminance contrast is sharpened. Clip limit and tile size come from the
/// "LowContrastEnhancement" section of transform_Config.json, loaded by <see cref="Process"/> itself.
/// </para>
/// </summary>
public static class Tx_LowContrastEnhancement {

    /// <summary>
    /// Webservice form: decodes <paramref name="arr"/> (JPEG, BGR), applies CLAHE,
    /// re-encodes to JPEG. <paramref name="upscale_factor"/> is accepted for interface
    /// conformance; image dimensions are not altered here.
    /// </summary>
    public static byte[] Process(byte[] arr, int stride, float upscale_factor) {
        LowContrastEnhancementConfig cfg = ConfigLoader.Section<LowContrastEnhancementConfig>(TransformParameters.ConfigFile, "LowContrastEnhancement");

        // Input: JPEG bytes, colour space BGR (OpenCVSharp default decode).
        using Mat bgrSrc = Cv2.ImDecode(arr, ImreadModes.Color);
        if (bgrSrc.Empty()) return arr;

        using Mat bgrEnhanced = ApplyClahe(bgrSrc, cfg);

        // Output: JPEG bytes, colour space BGR.
        Cv2.ImEncode(".jpg", bgrEnhanced, out byte[] encoded);
        return encoded;
    }

    // Converts BGR → LAB, applies CLAHE to the L-channel, merges, converts back to BGR.
    private static Mat ApplyClahe(Mat bgrSrc, LowContrastEnhancementConfig cfg) {
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
        using CLAHE clahe = Cv2.CreateCLAHE(cfg.ClipLimit, new Size(cfg.TileSize, cfg.TileSize));
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
