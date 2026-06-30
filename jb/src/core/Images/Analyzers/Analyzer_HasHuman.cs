using System.Runtime.InteropServices;
using OpenCvSharp;

namespace Prism.Core;

/// <summary>
/// Detects human presence in an image by measuring skin-colored pixel ratio within the salient bounding box.
/// Uses the BGR Mat produced by ImagePreProcessor — no additional image decode.
/// Sets the <c>has-human</c> ImageFeature.
/// </summary>
internal static class Analyzer_HasHuman
{
    /// <summary>
    /// Returns true when the fraction of skin-colored pixels within <paramref name="bbox"/>
    /// exceeds the configured threshold. BoundingBox is always set when this is called.
    /// </summary>
    public static bool Analyze(Mat colorMat, BoundingBox bbox, ImageAnalyzerConfig cfg)
    {
        // Crop to the bounding box region.
        using Mat roi = colorMat.SubMat(new Rect(bbox.X, bbox.Y, bbox.Width, bbox.Height));
        using Mat hsv = new Mat();
        Cv2.CvtColor(roi, hsv, ColorConversionCodes.BGR2HSV);

        int totalPixels = hsv.Rows * hsv.Cols;
        if (totalPixels == 0) return false;

        int stride = (int)hsv.Step();
        byte[] data = new byte[hsv.Rows * stride];
        Marshal.Copy(hsv.Data, data, 0, data.Length);

        int skinPixels = 0;
        for (int y = 0; y < hsv.Rows; y++)
        {
            for (int x = 0; x < hsv.Cols; x++)
            {
                int idx = y * stride + x * 3;
                // OpenCV HSV: H in [0,180], S in [0,255], V in [0,255]
                float h = data[idx]     * 2f;         // convert to degrees [0,360]
                float s = data[idx + 1] / 255f;
                float v = data[idx + 2] / 255f;

                bool hueInRange = (h >= cfg.SkinHueMin1 && h <= cfg.SkinHueMax1)
                               || (h >= cfg.SkinHueMin2 && h <= cfg.SkinHueMax2);
                bool satInRange = s >= cfg.SkinSatMin && s <= cfg.SkinSatMax;
                bool valInRange = v >= cfg.SkinValMin && v <= cfg.SkinValMax;

                if (hueInRange && satInRange && valInRange) skinPixels++;
            }
        }

        return (float)skinPixels / totalPixels >= cfg.MinSkinPixelRatio;
    }
}
