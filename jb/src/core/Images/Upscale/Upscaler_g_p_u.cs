using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace Prism.Core;

/// <summary>
/// GPU path: Real-ESRGAN x2plus (DirectML, fixed ×2) → Lanczos4 top-up to exact target.
/// Requires: Microsoft.ML.OnnxRuntime.DirectML NuGet + real-esrgan-x2plus.onnx model asset.
/// The committed model export has a fixed [1, 3, 64, 64] input, so images are processed in
/// overlapping tiles (<see cref="RunTiled"/>) and stitched back together; a model exported with a
/// dynamic input shape instead runs as a single tile covering the whole image.
/// Call <see cref="Initialize"/> once at startup before invoking <see cref="Upscale"/>.
/// </summary>
public static class Upscaler_g_p_u {
    private const double SrScale = 2.0;
    private const int SrScaleInt = 2;

    // Border (in source pixels) discarded from each tile's output at internal tile seams — Real-ESRGAN's
    // convolutional receptive field makes edge pixels of a tile less accurate than interior pixels, so
    // tiles overlap by this much and only their trusted center region is kept.
    private const int TileOverlap = 8;

    // ONNX tensor names — standard Real-ESRGAN x2plus export.
    private const string TensorInput  = "input";
    private const string TensorOutput = "output";

    private static readonly object _sessionLock = new();
    private static InferenceSession? _session;
    private static int _tileHeight;  // model's fixed input height; 0 when the model's shape is dynamic
    private static int _tileWidth;   // model's fixed input width; 0 when the model's shape is dynamic

    /// <summary>True when the Real-ESRGAN GPU session is loaded and ready for <see cref="Upscale"/>.</summary>
    public static bool IsReady => _session is not null;

    /// <summary>
    /// Loads the Real-ESRGAN ONNX model with the DirectML execution provider. Call once at startup
    /// before <see cref="Upscale"/>. Idempotent and thread-safe — a call once a session is already
    /// loaded is a no-op. Does not throw: when the model file is missing or the session fails to load,
    /// this leaves <see cref="IsReady"/> false so <see cref="ImageUpscaler"/> falls back to the CPU
    /// path instead of the caller crashing.
    /// </summary>
    public static void Initialize( string modelPath ) {
        lock (_sessionLock) {
            if (_session is not null) return;
            if (!File.Exists(modelPath)) return;

            try {
                var opts = new SessionOptions();
                opts.AppendExecutionProvider_DML(0);
                InferenceSession session = new(modelPath, opts);

                int[] inputDims = session.InputMetadata[TensorInput].Dimensions;
                _tileHeight = inputDims[2] > 0 ? inputDims[2] : 0;
                _tileWidth  = inputDims[3] > 0 ? inputDims[3] : 0;

                _session = session;
            }
            catch {
                // Graceful degradation — IsReady reports false; ImageUpscaler falls back to CPU.
                _session?.Dispose();
                _session = null;
            }
        }
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

    /// <summary>Runs Real-ESRGAN inference: JPEG → tiled session passes → stitched BGR uint8 → JPEG.</summary>
    private static byte[] RunRealEsrgan( byte[] imageBytes ) {
        InferenceSession? session = _session;
        if (session is null)
            throw new InvalidOperationException(
                "Upscaler_g_p_u.Initialize() must be called before RunRealEsrgan.");

        using Mat bgrUint8 = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        using Mat srBgrUint8 = RunTiled(session, bgrUint8);

        Cv2.ImEncode(".jpg", srBgrUint8, out byte[] result);
        return result;
    }

    /// <summary>
    /// Splits <paramref name="src"/> into overlapping tiles sized to the model's fixed input shape
    /// (the whole image, in one tile, when the shape is dynamic), runs each tile through
    /// <paramref name="session"/>, and stitches the trusted center region of each tile's output into
    /// the full-resolution result.
    /// </summary>
    private static Mat RunTiled( InferenceSession session, Mat src ) {
        bool tiling = _tileHeight > 0 && _tileWidth > 0;
        int tileH = tiling ? _tileHeight : src.Rows;
        int tileW = tiling ? _tileWidth  : src.Cols;
        int overlap = tiling ? TileOverlap : 0;

        int stepH = Math.Max(1, tileH - 2 * overlap);
        int stepW = Math.Max(1, tileW - 2 * overlap);

        int tilesY = Math.Max(1, (int)Math.Ceiling(src.Rows / (double)stepH));
        int tilesX = Math.Max(1, (int)Math.Ceiling(src.Cols / (double)stepW));

        int paddedH = tilesY * stepH + 2 * overlap;
        int paddedW = tilesX * stepW + 2 * overlap;

        using Mat padded = new();
        Cv2.CopyMakeBorder(src, padded,
            overlap, paddedH - src.Rows - overlap,
            overlap, paddedW - src.Cols - overlap,
            BorderTypes.Replicate);

        Mat output = new(src.Rows * SrScaleInt, src.Cols * SrScaleInt, MatType.CV_8UC3);

        for (int ty = 0; ty < tilesY; ty++) {
            int coreY0 = ty * stepH;
            int coreH  = Math.Min(stepH, src.Rows - coreY0);

            for (int tx = 0; tx < tilesX; tx++) {
                int coreX0 = tx * stepW;
                int coreW  = Math.Min(stepW, src.Cols - coreX0);

                using Mat tile = padded.SubMat(new Rect(tx * stepW, ty * stepH, tileW, tileH));
                using Mat tileOutput = RunSingleTile(session, tile);

                using Mat core = tileOutput.SubMat(new Rect(
                    overlap * SrScaleInt, overlap * SrScaleInt, coreW * SrScaleInt, coreH * SrScaleInt));
                core.CopyTo(output[new Rect(
                    coreX0 * SrScaleInt, coreY0 * SrScaleInt, coreW * SrScaleInt, coreH * SrScaleInt)]);
            }
        }

        return output;
    }

    /// <summary>Runs one fixed-size tile through the ONNX session and returns the BGR uint8 result.</summary>
    private static Mat RunSingleTile( InferenceSession session, Mat tileBgrUint8 ) {
        DenseTensor<float> inputTensor = BuildInputTensor(tileBgrUint8);

        var inputs = new List<NamedOnnxValue> {
            NamedOnnxValue.CreateFromTensor(TensorInput, inputTensor)
        };

        // _sessionLock serializes inference across all tiles and images (intra-job) and all concurrent
        // jobs — required because the DML execution provider does not support concurrent
        // InferenceSession.Run calls (same constraint as MatchingService._clipLock). Locks the whole
        // Run + tensor-extraction tail, not just Run(), because the Tensor<float> view over `outputs`
        // must stay alive until TensorToMat finishes reading it.
        lock (_sessionLock) {
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs =
                session.Run(inputs, [TensorOutput]);

            Tensor<float> outputTensor = outputs.First(o => o.Name == TensorOutput).AsTensor<float>();

            return TensorToMat(outputTensor);
        }
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
    /// Converts the NCHW float32 output tensor to a BGR uint8 Mat.
    /// Output tensor shape: [1, 3, H×2, W×2] — BGR channel order, [0, 1] float.
    /// </summary>
    private static Mat TensorToMat( Tensor<float> outputTensor ) {
        int h = outputTensor.Dimensions[2];
        int w = outputTensor.Dimensions[3];
        Mat bgrUint8Out = new(h, w, MatType.CV_8UC3);

        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                // Clamp [0, 1] → scale to [0, 255] → cast to byte.
                byte b = (byte)(Math.Clamp(outputTensor[0, 0, y, x], 0f, 1f) * 255f);
                byte g = (byte)(Math.Clamp(outputTensor[0, 1, y, x], 0f, 1f) * 255f);
                byte r = (byte)(Math.Clamp(outputTensor[0, 2, y, x], 0f, 1f) * 255f);
                bgrUint8Out.At<Vec3b>(y, x) = new Vec3b(b, g, r);
            }
        }

        return bgrUint8Out;
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
