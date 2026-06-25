using OpenCvSharp;

namespace Prism.Core;

// GPU path: Real-ESRGAN x2plus (DirectML, fixed ×2) → Lanczos4 top-up to exact target.
// Requires: Microsoft.ML.OnnxRuntime.DirectML NuGet + real-esrgan-x2plus.onnx model path in config.
public static class Upscaler_g_p_u {
    private const double SrScale = 2.0;

    // TODO: initialize session once OnnxRuntime.DirectML package is added:
    // private static readonly InferenceSession _session = CreateSession(modelPath);
    // private static InferenceSession CreateSession(string modelPath) {
    //     var opts = new SessionOptions();
    //     opts.AppendExecutionProvider_DML(adapterIndex: 0);
    //     return new InferenceSession(modelPath, opts);
    // }

    public static byte[] Upscale( byte[] imageBytes, double scaleFactor ) {
        byte[] sr       = RunRealEsrgan(imageBytes);    // fixed ×2
        double remaining = scaleFactor / SrScale;        // 0.9–1.25 for scaleFactor 1.8–2.5
        return ApplyLanczos4(sr, remaining);
    }

    // TODO: replace with ONNX DirectML inference:
    //   1. Decode to BGR float [0,1] → reshape to NCHW [1, 3, H, W]
    //   2. _session.Run(inputs) → output [1, 3, H×2, W×2]
    //   3. Clamp [0,1] → BGR uint8 → encode JPG
    private static byte[] RunRealEsrgan( byte[] imageBytes ) =>
        throw new NotImplementedException(
            "Add Microsoft.ML.OnnxRuntime.DirectML NuGet and real-esrgan-x2plus.onnx before enabling GPU path.");

    private static byte[] ApplyLanczos4( byte[] imageBytes, double scaleFactor ) {
        using Mat src = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        int newW = (int)Math.Round(src.Cols * scaleFactor);
        int newH = (int)Math.Round(src.Rows * scaleFactor);
        using Mat dst = new();
        Cv2.Resize(src, dst, new Size(newW, newH), interpolation: InterpolationFlags.Lanczos4);
        Cv2.ImEncode(".jpg", dst, out byte[] result);
        return result;
    }
}
