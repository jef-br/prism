using OpenCvSharp;

namespace Prism.Core;

/// <summary>CPU fallback — Lanczos4, capped at ×1.42.</summary>
public static class Upscaler_c_p_u {
    private const double MaxScaleFactor = 1.42;

    public static byte[] Upscale( byte[] imageBytes, double scaleFactor ) {
        double scale = Math.Min(scaleFactor, MaxScaleFactor);
        using Mat src = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        int newW = (int)Math.Round(src.Cols * scale);
        int newH = (int)Math.Round(src.Rows * scale);
        using Mat dst = new();
        Cv2.Resize(src, dst, new Size(newW, newH), interpolation: InterpolationFlags.Lanczos4);
        Cv2.ImEncode(".jpg", dst, out byte[] result);
        return result;
    }
}
