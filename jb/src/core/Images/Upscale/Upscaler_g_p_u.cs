using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace Prism.Core;

/// <summary>
/// GPU path: Real-ESRGAN x2plus (DirectML, fixed ×2) → Lanczos4 top-up to exact target.
/// Requires: Microsoft.ML.OnnxRuntime.DirectML NuGet + real-esrgan-x2plus.onnx model asset.
/// Call <see cref="Initialize"/> once at startup before invoking <see cref="Upscale"/>.
/// </summary>
public static class Upscaler_g_p_u {
    private const double SrScale = 2.0;

    // ONNX tensor names — standard Real-ESRGAN x2plus export.
    private const string TensorInput  = "input";
    private const string TensorOutput = "output";

    private static InferenceSession? _session;

    /// <summary>
    /// Loads the Real-ESRGAN ONNX model with the DirectML execution provider.
    /// Must be called once at startup before <see cref="Upscale"/>.
    /// Throws <see cref="InvalidOperationException"/> if the model file does not exist.
    /// </summary>
    public static void Initialize( string modelPath ) {
        if (!File.Exists(modelPath))
            throw new InvalidOperationException(
                $"Real-ESRGAN model not found at: {modelPath}. " +
                "Deploy Real-ESRGAN_x2plus.onnx to Images/Upscale/ONNX/ or set PRISM_ONNX_MODEL_DIR.");

        var opts = new SessionOptions();
        opts.AppendExecutionProvider_DML(0);
        _session = new InferenceSession(modelPath, opts);
    }

    /// <summary>
    /// Upscales JPEG image bytes by <paramref name="scaleFactor"/> using Real-ESRGAN ×2 followed
    /// by Lanczos4 to reach the requested scale.
    /// </summary>
    public static byte[] Upscale( byte[] imageBytes, double scaleFactor ) {
        byte[] sr = RunRealEsrgan(imageBytes);    // fixed ×2
        double remaining = scaleFactor / SrScale;  // 0.9–1.25 for scaleFactor 1.8–2.5
        return ApplyLanczos4(sr, remaining);
    }

    /// <summary>
    /// Runs Real-ESRGAN inference: JPEG → BGR float NCHW → session → clamp → BGR uint8 → JPEG.
    /// </summary>
    private static byte[] RunRealEsrgan( byte[] imageBytes ) {
        if (_session is null)
            throw new InvalidOperationException(
                "Upscaler_g_p_u.Initialize() must be called before RunRealEsrgan.");

        // Decode input JPEG → BGR uint8 Mat (OpenCV native format).
        using Mat bgrUint8 = Cv2.ImDecode(imageBytes, ImreadModes.Color);

        DenseTensor<float> inputTensor = BuildInputTensor(bgrUint8);

        var inputs = new List<NamedOnnxValue> {
            NamedOnnxValue.CreateFromTensor(TensorInput, inputTensor)
        };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs =
            _session.Run(inputs, [TensorOutput]);

        Tensor<float> outputTensor = outputs.First(o => o.Name == TensorOutput).AsTensor<float>();

        return BuildOutputJpeg(outputTensor);
    }

    /// <summary>
    /// Decodes BGR uint8 Mat to an NCHW float32 tensor normalized to [0, 1].
    /// Input shape: [1, 3, H, W] — CHW layout, BGR channel order.
    /// </summary>
    private static DenseTensor<float> BuildInputTensor( Mat bgrUint8 ) {
        int h = bgrUint8.Rows;
        int w = bgrUint8.Cols;
        // Input shape: [1, 3, H, W] — NCHW, BGR channel order, normalized to [0, 1].
        var tensor = new DenseTensor<float>([1, 3, h, w]);

        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                Vec3b px = bgrUint8.At<Vec3b>(y, x);
                tensor[0, 0, y, x] = px.Item0 / 255f;  // B
                tensor[0, 1, y, x] = px.Item1 / 255f;  // G
                tensor[0, 2, y, x] = px.Item2 / 255f;  // R
            }
        }

        return tensor;
    }

    /// <summary>
    /// Converts the NCHW float32 output tensor to a BGR uint8 Mat and encodes it as JPEG.
    /// Output tensor shape: [1, 3, H×2, W×2] — BGR channel order, [0, 1] float.
    /// </summary>
    private static byte[] BuildOutputJpeg( Tensor<float> outputTensor ) {
        int h = outputTensor.Dimensions[2];
        int w = outputTensor.Dimensions[3];
        using Mat bgrUint8Out = new(h, w, MatType.CV_8UC3);

        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                // Clamp [0, 1] → scale to [0, 255] → cast to byte.
                byte b = (byte)(Math.Clamp(outputTensor[0, 0, y, x], 0f, 1f) * 255f);
                byte g = (byte)(Math.Clamp(outputTensor[0, 1, y, x], 0f, 1f) * 255f);
                byte r = (byte)(Math.Clamp(outputTensor[0, 2, y, x], 0f, 1f) * 255f);
                bgrUint8Out.At<Vec3b>(y, x) = new Vec3b(b, g, r);
            }
        }

        Cv2.ImEncode(".jpg", bgrUint8Out, out byte[] result);
        return result;
    }

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
