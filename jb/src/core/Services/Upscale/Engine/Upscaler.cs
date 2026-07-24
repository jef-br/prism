using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace Prism.Services.Upscale;

/// <summary>
/// The one PRISM upscaler: Real-ESRGAN x2plus (fixed ×2) → Lanczos4 top-up to exact target. The
/// session comes from OnnxSessionFactory — DirectML when a hardware adapter is present, CPU otherwise
/// (T-4110; there is no separate CPU algorithm, the same model runs everywhere).
/// Requires: real-esrgan-x2plus.onnx model asset — startup validation fails loud without it.
/// The committed model export has a fixed [1, 3, 64, 64] input, so images are processed in
/// overlapping tiles (<see cref="RunTiled"/>) whose outputs are combined with a weighted blend across
/// the overlap band (see <see cref="AccumulateTile"/>) rather than a hard cut, so no seam is visible at
/// tile boundaries; a model exported with a dynamic input shape instead runs as a single tile covering
/// the whole image. Call <see cref="Initialize"/> once at startup before invoking <see cref="Upscale"/>.
/// </summary>
public static class Upscaler {
    private const double SrScale = 2.0;
    private const int SrScaleInt = 2;
    private const float MaxChannelValueF = 255f;

    private static readonly bool GpuAvailable = GpuProbe.HasHardwareDirectMLAdapter();

    // Fallback tiling parameters used when cfg_Upscale.json is missing or unreadable — see LoadTilingConfig.
    private const int DefaultTileOverlapPixels = 16;
    private const int DefaultDiscardBandPixels = 3;

    // ONNX tensor names — standard Real-ESRGAN x2plus export.
    private const string TensorInput  = "input";
    private const string TensorOutput = "output";

    private static readonly object _sessionLock = new();
    private static InferenceSession? _session;
    private static int _tileHeight;  // model's fixed input height; 0 when the model's shape is dynamic
    private static int _tileWidth;   // model's fixed input width; 0 when the model's shape is dynamic

    // Tile overlap (source pixels) reserved for the discard band + blend ramp at internal tile seams —
    // see LoadTilingConfig / cfg_Upscale.json.
    private static int _tileOverlapPixels = DefaultTileOverlapPixels;

    // Source pixels nearest each internal seam that are fully discarded before blending starts — Real-
    // ESRGAN's convolutional receptive field makes edge pixels of a tile less accurate than interior
    // pixels, so this band never contributes to the stitched output.
    private static int _discardBandPixels = DefaultDiscardBandPixels;

    /// <summary>True when a hardware DirectML adapter was detected at startup.</summary>
    public static bool IsGpuAvailable => GpuAvailable;

    /// <summary>True when the Real-ESRGAN session is loaded and ready for <see cref="Upscale"/>.</summary>
    public static bool IsReady => _session is not null;

    /// <summary>
    /// Loads the Real-ESRGAN ONNX model via OnnxSessionFactory and the tiling parameters
    /// from <paramref name="configPath"/> (cfg_Upscale.json). Call once at startup before
    /// <see cref="Upscale"/>. Idempotent and thread-safe — a call once a session is already loaded is a
    /// no-op. A missing model file returns silently (existence is validated loud upstream by
    /// PrismConfiguration.ValidateModelAssets / UpscaleService.Create); a file that is present but
    /// fails to load throws <see cref="PrismConfigurationException"/> — corrupt models never degrade
    /// silently (T-4110). A missing or unreadable tiling config falls back to the hardcoded defaults
    /// rather than blocking session load — it is a tuning knob, not a correctness gate.
    /// </summary>
    public static void Initialize( string modelPath, string configPath ) {
        lock (_sessionLock) {
            if (_session is not null) return;
            if (!File.Exists(modelPath)) return;

            try {
                InferenceSession session = OnnxSessionFactory.Create(modelPath);

                int[] inputDims = session.InputMetadata[TensorInput].Dimensions;
#pragma warning disable S109 // NCHW dims: index 2 = height, 3 = width — fixed ONNX tensor layout, never changes.
                _tileHeight = inputDims[2] > 0 ? inputDims[2] : 0;
                _tileWidth  = inputDims[3] > 0 ? inputDims[3] : 0;
#pragma warning restore S109

                LoadTilingConfig(configPath);

                _session = session;
            }
            catch (Exception loadException) {
                _session?.Dispose();
                _session = null;
                throw new PrismConfigurationException(
                    $"Real-ESRGAN ONNX model at '{modelPath}' is present but failed to load — the file is " +
                    $"corrupt, truncated, or an incompatible export: {loadException.Message}", loadException);
            }
        }
    }

    /// <summary>Loads tile overlap / discard-band sizing from cfg_Upscale.json, falling back to the hardcoded defaults on any failure.</summary>
    private static void LoadTilingConfig( string configPath ) {
        try {
            UpscaleConfig config = UpscaleConfig.Load(configPath);
            _tileOverlapPixels = config.TileOverlapPixels;
            _discardBandPixels = config.DiscardBandPixels;
        }
        catch {
            _tileOverlapPixels = DefaultTileOverlapPixels;
            _discardBandPixels = DefaultDiscardBandPixels;
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
                "Upscaler.Initialize() must be called before RunRealEsrgan.");

        using Mat bgrUint8 = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        using Mat srBgrUint8 = RunTiled(session, bgrUint8);

        Cv2.ImEncode(".jpg", srBgrUint8, out byte[] result);
        return result;
    }

    /// <summary>
    /// Splits <paramref name="src"/> into overlapping tiles sized to the model's fixed input shape
    /// (the whole image, in one tile, when the shape is dynamic), runs each tile through
    /// <paramref name="session"/>, and combines every tile's output into the full-resolution result with
    /// a weighted blend across the overlap band (<see cref="AccumulateTile"/>) so no hard seam remains
    /// at internal tile boundaries.
    /// </summary>
    private static Mat RunTiled( InferenceSession session, Mat src ) {
        bool tiling = _tileHeight > 0 && _tileWidth > 0;
        int tileH = tiling ? _tileHeight : src.Rows;
        int tileW = tiling ? _tileWidth  : src.Cols;
        int overlap = tiling ? _tileOverlapPixels : 0;
        int discard = tiling ? _discardBandPixels : 0;

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

        int outH = src.Rows * SrScaleInt;
        int outW = src.Cols * SrScaleInt;
        int overlapOut = overlap * SrScaleInt;
        int discardOut = discard * SrScaleInt;

        using Mat colorSum  = new(outH, outW, MatType.CV_32FC3, Scalar.All(0));
        using Mat weightSum = new(outH, outW, MatType.CV_32FC1, Scalar.All(0));

        for (int ty = 0; ty < tilesY; ty++) {
            bool topOutward    = ty == 0;
            bool bottomOutward = ty == tilesY - 1;
            int originY = (ty * stepH - overlap) * SrScaleInt;

            for (int tx = 0; tx < tilesX; tx++) {
                bool leftOutward  = tx == 0;
                bool rightOutward = tx == tilesX - 1;
                int originX = (tx * stepW - overlap) * SrScaleInt;

                using Mat tile = padded.SubMat(new Rect(tx * stepW, ty * stepH, tileW, tileH));
                using Mat tileOutput = RunSingleTile(session, tile);

                AccumulateTile(tileOutput, colorSum, weightSum, originY, originX, outH, outW,
                    overlapOut, discardOut, topOutward, bottomOutward, leftOutward, rightOutward);
            }
        }

        return NormalizeAccumulator(colorSum, weightSum, outH, outW);
    }

    /// <summary>
    /// Adds one tile's output into the running <paramref name="colorSum"/>/<paramref name="weightSum"/>
    /// accumulators at offset (<paramref name="originY"/>, <paramref name="originX"/>), weighted per
    /// pixel by <see cref="AxisWeight"/> along each axis. A tile's edges that face a real neighboring
    /// tile taper from 0 (discard band) to 1 (trusted interior) across the overlap band; edges that face
    /// the true image border (<paramref name="topOutward"/> etc.) carry full weight throughout, since
    /// there is no neighbor there to blend against.
    /// </summary>
    private static void AccumulateTile(
        Mat tileOutput, Mat colorSum, Mat weightSum, int originY, int originX, int outH, int outW,
        int overlapOut, int discardOut, bool topOutward, bool bottomOutward, bool leftOutward, bool rightOutward ) {
        int tileH = tileOutput.Rows;
        int tileW = tileOutput.Cols;

        for (int ly = 0; ly < tileH; ly++) {
            int oy = originY + ly;
            if (oy < 0 || oy >= outH) continue;

            float wy = AxisWeight(ly, tileH, overlapOut, discardOut, topOutward, bottomOutward);
            if (wy <= 0f) continue;

            for (int lx = 0; lx < tileW; lx++) {
                int ox = originX + lx;
                if (ox < 0 || ox >= outW) continue;

                float w = wy * AxisWeight(lx, tileW, overlapOut, discardOut, leftOutward, rightOutward);
                if (w <= 0f) continue;

                Vec3b tilePixel = tileOutput.Get<Vec3b>(ly, lx);
                Vec3f accumulated = colorSum.Get<Vec3f>(oy, ox);
                colorSum.Set<Vec3f>(oy, ox, new Vec3f(
                    accumulated.Item0 + tilePixel.Item0 * w,
                    accumulated.Item1 + tilePixel.Item1 * w,
                    accumulated.Item2 + tilePixel.Item2 * w));
                weightSum.Set<float>(oy, ox, weightSum.Get<float>(oy, ox) + w);
            }
        }
    }

    /// <summary>
    /// Blend weight at <paramref name="pos"/> along one axis of a tile of length <paramref name="length"/>.
    /// Combines the ramp measured from each end (<see cref="RampFromEdge"/>) with <c>Min</c> so a pixel
    /// only reaches full weight once it clears both a start-edge and an end-edge ramp; an end that faces
    /// the true image border (<paramref name="startOutward"/>/<paramref name="endOutward"/>) always
    /// contributes full weight, since there is no neighboring tile there to blend against.
    /// </summary>
    internal static float AxisWeight( int pos, int length, int overlapOut, int discardOut, bool startOutward, bool endOutward ) {
        float wStart = startOutward ? 1f : RampFromEdge(pos, overlapOut, discardOut);
        float wEnd   = endOutward   ? 1f : RampFromEdge(length - 1 - pos, overlapOut, discardOut);
        return Math.Min(wStart, wEnd);
    }

    /// <summary>
    /// 0 within the discard band nearest a seam-facing edge, a raised-cosine taper from 0 to 1 across the
    /// remaining overlap band, and 1 in the trusted interior beyond it.
    /// </summary>
    internal static float RampFromEdge( int distFromEdge, int overlapOut, int discardOut ) {
        if (distFromEdge < discardOut) return 0f;

        int rampWidth = overlapOut - discardOut;
        if (rampWidth <= 0) return 1f;

        int rel = distFromEdge - discardOut;
        if (rel >= rampWidth) return 1f;

#pragma warning disable S109 // 0.5 is the raised-cosine half-amplitude — the curve's own midline, not a tunable.
        double t = (rel + 0.5) / rampWidth;
        return (float)(0.5 - 0.5 * Math.Cos(Math.PI * t));
#pragma warning restore S109
    }

    /// <summary>Divides the weighted color accumulator by the accumulated weight to produce the final stitched BGR uint8 image.</summary>
    private static Mat NormalizeAccumulator( Mat colorSum, Mat weightSum, int outH, int outW ) {
        Mat output = new(outH, outW, MatType.CV_8UC3);

        for (int y = 0; y < outH; y++) {
            for (int x = 0; x < outW; x++) {
                float w = weightSum.Get<float>(y, x);
                Vec3f sum = colorSum.Get<Vec3f>(y, x);
                output.Set<Vec3b>(y, x, new Vec3b(
                    (byte)Math.Clamp(sum.Item0 / w, 0f, MaxChannelValueF),
                    (byte)Math.Clamp(sum.Item1 / w, 0f, MaxChannelValueF),
                    (byte)Math.Clamp(sum.Item2 / w, 0f, MaxChannelValueF)));
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
                tensor[0, 0, y, x] = px.Item0 / MaxChannelValueF;  // B
                tensor[0, 1, y, x] = px.Item1 / MaxChannelValueF;  // G
#pragma warning disable S109 // channel index 2 = R in BGR order — fixed layout, never changes.
                tensor[0, 2, y, x] = px.Item2 / MaxChannelValueF;  // R
#pragma warning restore S109
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
                byte b = (byte)(Math.Clamp(outputTensor[0, 0, y, x], 0f, 1f) * MaxChannelValueF);
                byte g = (byte)(Math.Clamp(outputTensor[0, 1, y, x], 0f, 1f) * MaxChannelValueF);
#pragma warning disable S109 // channel index 2 = R in BGR order — fixed layout, never changes.
                byte r = (byte)(Math.Clamp(outputTensor[0, 2, y, x], 0f, 1f) * MaxChannelValueF);
#pragma warning restore S109
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
