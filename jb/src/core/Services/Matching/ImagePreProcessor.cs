using System.Globalization;
using System.Runtime.InteropServices;
using OpenCvSharp;
using CvSize = OpenCvSharp.Size;

namespace Prism.Services.Matching;

/// <summary>
/// Normalizes each image (EXIF orient → flat JPG → upscale decision), detects its salient
/// bounding box, and runs image analyzers before transform routing.
/// Returns preprocessed JPEG bytes and a BGR Mat for downstream transform use.
/// </summary>
public static class ImagePreProcessor {
    private const string DefaultBboxCoords = "0.0000,0.0000,0.0000,0.0000"; // internal sentinel only

    /// <summary>
    /// Tuning values for ImagePreProcessor, bound from the "ImagePreProcessor" section of
    /// ClassifyConfig.json. No defaults — every value must be present in the JSON or deserialization
    /// fails loud. Loaded via a literal file/section-name pair rather than through ClassifyParameters:
    /// this class compiles into Prism.Core, which does not reference the Classify project that owns
    /// ClassifyParameters.
    /// </summary>
    public sealed class Config : IValidatableConfig {
        /// <summary>Longest side (px) of the grayscale analysis image used for salient-bbox detection.</summary>
        public required int MaxAnalysisSize { get; init; }

        /// <summary>Canny edge detector's first (lower) hysteresis threshold.</summary>
        public required float CannyThreshold1 { get; init; }

        /// <summary>Canny edge detector's second (upper) hysteresis threshold.</summary>
        public required float CannyThreshold2 { get; init; }

        /// <summary>Sigmoid midpoint applied to the normalized spatial-saliency mask.</summary>
        public required float SigmoidCenter { get; init; }

        /// <summary>Sigmoid slope applied to the normalized spatial-saliency mask.</summary>
        public required float SigmoidSlope { get; init; }

        /// <summary>Refined-mask value above which a pixel counts toward the salient bounding box.</summary>
        public required float EdgeThreshold { get; init; }

        /// <summary>Minimum bounding-box area, as a fraction of the analysis image, to accept the detected box.</summary>
        public required float MinBboxAreaRatio { get; init; }

        /// <summary>Maximum raw 8-bit channel value, used to normalize Canny/grayscale output into [0,1].</summary>
        public required double MaxChannelValueD { get; init; }

        /// <summary>Sobel aperture size used by the Canny edge detector.</summary>
        public required int CannySobelApertureSize { get; init; }

        /// <summary>Side length (px) of the square structuring element used to dilate detected edges.</summary>
        public required int DilationKernelSize { get; init; }

        public void Validate() {
            List<string> problems = [];

            if (this.MaxAnalysisSize < 1) problems.Add("ImagePreProcessor.MaxAnalysisSize must be >= 1");
            if (this.CannyThreshold1 <= 0f) problems.Add("ImagePreProcessor.CannyThreshold1 must be > 0");
            if (this.CannyThreshold2 <= this.CannyThreshold1) problems.Add("ImagePreProcessor.CannyThreshold2 must be > CannyThreshold1");
            if (this.SigmoidSlope <= 0f) problems.Add("ImagePreProcessor.SigmoidSlope must be > 0");
            if (this.EdgeThreshold is <= 0f or >= 1f) problems.Add("ImagePreProcessor.EdgeThreshold must be in (0,1)");
            if (this.MinBboxAreaRatio is <= 0f or >= 1f) problems.Add("ImagePreProcessor.MinBboxAreaRatio must be in (0,1)");
            if (this.MaxChannelValueD <= 0.0) problems.Add("ImagePreProcessor.MaxChannelValueD must be > 0");
            if (this.CannySobelApertureSize < 1) problems.Add("ImagePreProcessor.CannySobelApertureSize must be >= 1");
            if (this.DilationKernelSize < 1) problems.Add("ImagePreProcessor.DilationKernelSize must be >= 1");

            if (problems.Count > 0) throw new PrismConfigurationException(string.Join("; ", problems));
        }
    }

    /// <summary>
    /// Normalizes the image, detects the salient bounding box, and runs analyzers.
    /// Returns preprocessed JPEG bytes and a BGR Mat (caller owns the Mat and must dispose it).
    /// Sets <see cref="ImageRecord_LAMBDA.BoundingBox"/> and feature values on <paramref name="lambda"/>.
    /// Returns (null, null) and sets <see cref="ImageRecord_LAMBDA.IsKo"/> when the image fails thresholds.
    /// </summary>
    public static async Task<(byte[]? bytes, Mat? colorMat)> PreprocessAsync(
        ImageRecord_LAMBDA lambda, string? imagePath, PrismConfiguration config, IUpscaleService? remoteUpscale = null, CancellationToken cancellationToken = default) {
        byte[]? flatJpg = ReadNormalizedJpg(imagePath);
        if (flatJpg is null) return (null, null);

        // Decode to BGR Mat once — reused for bbox detection, analyzers, and downstream transforms.
        Mat colorMat = Cv2.ImDecode(flatJpg, ImreadModes.Color);
        if (colorMat.Empty()) { colorMat.Dispose(); return (null, null); }

        (string coords, int origW, int origH) bbox = DetectSalientBoundingBox(colorMat);

        lambda.BoundingBox = ParseSalientBox(bbox.coords, bbox.origW, bbox.origH);

        byte[]? processedBytes = await UpscaleAsync(flatJpg, bbox, config, lambda, remoteUpscale, cancellationToken);
        if (lambda.IsKo) { colorMat.Dispose(); return (null, null); }

        return (processedBytes, colorMat);
    }

    /// <summary>
    /// Detects the salient bounding box from pre-decoded BGR Mat and parses it into pixel coordinates.
    /// Exposed internal for use by stateless webservice paths that decode their own Mat.
    /// </summary>
    internal static BoundingBox? DetectAndParseSalientBox(byte[] imageBytes) {
        using Mat colorMat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        if (colorMat.Empty()) return null;
        (string coords, int origW, int origH) bbox = DetectSalientBoundingBox(colorMat);
        return ParseSalientBox(bbox.coords, bbox.origW, bbox.origH);
    }

    // Steps 1 + 2 are already done by Import: the file at imagePath is an oriented, alpha-flattened
    // JPEG. Re-normalizing here would decode it, apply two no-op mutations, and re-encode a second
    // lossy JPEG generation before upscale/crop — so the bytes are read as-is instead.
    private static byte[]? ReadNormalizedJpg(string? imagePath) {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath)) return null;
        try { return File.ReadAllBytes(imagePath); } catch { return null; }
    }

    // Step 3: salient bounding box detection from BGR Mat
    private static (string coords, int origW, int origH) DetectSalientBoundingBox(Mat colorMat) {
        Config cfg = ConfigLoader.Section<Config>("ClassifyConfig.json", "ImagePreProcessor");
        try {
            int origW = colorMat.Cols, origH = colorMat.Rows;
            if (origW == 0 || origH == 0) return (DefaultBboxCoords, 0, 0);

            using Mat gray8 = new Mat();
            Cv2.CvtColor(colorMat, gray8, ColorConversionCodes.BGR2GRAY);

            using Mat img = ScaleDown(gray8, cfg.MaxAnalysisSize);
            int w = img.Cols, h = img.Rows;

            // g = gray.astype(float32) / 255
            using Mat grayF = new Mat();
            img.ConvertTo(grayF, MatType.CV_32F, 1.0 / cfg.MaxChannelValueD);

            // local_mean = blur(g, 31); local_sqmean = blur(g*g, 31)
            using Mat localMean = new Mat();
            using Mat graySquared = new Mat();
            using Mat localSqMean = new Mat();
            Cv2.Blur(grayF, localMean, new CvSize(31, 31));
            Cv2.Multiply(grayF, grayF, graySquared);
            Cv2.Blur(graySquared, localSqMean, new CvSize(31, 31));

            // local_contrast = sqrt(max(sqmean - mean², 0)), normalize [0,1]
            using Mat mean2 = new Mat();
            using Mat localContrast = new Mat();
            Cv2.Multiply(localMean, localMean, mean2);
            Cv2.Subtract(localSqMean, mean2, localContrast);
            Cv2.Threshold(localContrast, localContrast, 0, 0, ThresholdTypes.Tozero);
            Cv2.Sqrt(localContrast, localContrast);
            Cv2.Normalize(localContrast, localContrast, 0.0, 1.0, NormTypes.MinMax);

            // edges = Canny(gray8, 80, 160) / 255
            using Mat edges8 = new Mat();
            Cv2.Canny(img, edges8, cfg.CannyThreshold1, cfg.CannyThreshold2, cfg.CannySobelApertureSize);
            using Mat edgesF = new Mat();
            edges8.ConvertTo(edgesF, MatType.CV_32F, 1.0 / cfg.MaxChannelValueD);

            // edges_dilated = dilate(edges8, DilationKernelSize²) / 255
            using Mat dilationKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new CvSize(cfg.DilationKernelSize, cfg.DilationKernelSize));
            using Mat edges8Dilated = new Mat();
            Cv2.Dilate(edges8, edges8Dilated, dilationKernel, iterations: 1);
            using Mat edgesDilatedF = new Mat();
            edges8Dilated.ConvertTo(edgesDilatedF, MatType.CV_32F, 1.0 / cfg.MaxChannelValueD);

            // spatial = edges_dilated * local_contrast, normalize [0,1]
            using Mat spatial = new Mat();
            Cv2.Multiply(edgesDilatedF, localContrast, spatial);
            Cv2.Normalize(spatial, spatial, 0.0, 1.0, NormTypes.MinMax);

            // mask = sigmoid(spatial); refined = edges * mask
            using Mat mask = new Mat(h, w, MatType.CV_32F);
            using Mat refined = new Mat();
            ApplySigmoid(spatial, mask, w, h, cfg.SigmoidCenter, cfg.SigmoidSlope);
            Cv2.Multiply(edgesF, mask, refined);

            // bounding rect of pixels > threshold; default to 0,0,0,0 when not found
            float x1 = 0, y1 = 0, x2 = 0, y2 = 0;
            (int bx1, int by1, int bx2, int by2)? found = FindBbox(refined, w, h, cfg.EdgeThreshold);
            if (found is not null) {
                float bboxArea = (float)(found.Value.bx2 - found.Value.bx1) * (found.Value.by2 - found.Value.by1);
                if (bboxArea / (w * h) >= cfg.MinBboxAreaRatio) {
                    x1 = (float)found.Value.bx1 / w;
                    y1 = (float)found.Value.by1 / h;
                    x2 = (float)found.Value.bx2 / w;
                    y2 = (float)found.Value.by2 / h;
                }
            }

            return (string.Format(CultureInfo.InvariantCulture, "{0:F4},{1:F4},{2:F4},{3:F4}", x1, y1, x2, y2), origW, origH);
        }
        catch { return (DefaultBboxCoords, 0, 0); }
    }

    // Step 4: upscale decision based on the salient bbox's largest pixel dimension
    private static async Task<byte[]?> UpscaleAsync(byte[] flatJpg, (string coords, int origW, int origH) bbox,
                                     PrismConfiguration config, ImageRecord_LAMBDA lambda, IUpscaleService? remoteUpscale, CancellationToken cancellationToken) {
        if (bbox.origW == 0) return flatJpg;

        string[] parts = bbox.coords.Split(',');
        float bboxPixelW = (float.Parse(parts[2], CultureInfo.InvariantCulture) - float.Parse(parts[0], CultureInfo.InvariantCulture)) * bbox.origW;
        float bboxPixelH = (float.Parse(parts[3], CultureInfo.InvariantCulture) - float.Parse(parts[1], CultureInfo.InvariantCulture)) * bbox.origH;
        float largest = Math.Max(bboxPixelW, bboxPixelH);

        if (largest < config.MinInputSizeInPixels)
            return Ko(lambda, "PREPROCESS_TOO_SMALL", $"Salient object {largest:F0}px < minimum {config.MinInputSizeInPixels}px.");

        if (largest >= config.MinOutputWidth)
            return flatJpg;

        double scale = config.MinOutputWidth / (double)largest;
        if (scale > config.MaxUpScaleFactor)
            return Ko(lambda, "PREPROCESS_UPSCALE_EXCEEDED", $"Required scale {scale:F2}× exceeds maximum {config.MaxUpScaleFactor:F2}×.");

        // Remote host when PRISM_UPSCALE_URL routed one in (distributed deployment), local static
        // session otherwise. The remote leg is a real await — no thread held during the round-trip.
        // The local leg stays synchronous: it's GPU/CPU compute, not I/O, so there's nothing to yield on.
        return remoteUpscale is null
            ? Upscaler.Upscale(flatJpg, scale)
            : await remoteUpscale.UpscaleAsync(flatJpg, scale, cancellationToken);
    }

    private static byte[]? Ko(ImageRecord_LAMBDA lambda, string code, string message) {
        lambda.IsKo = true;
        lambda.KoReasonCode = code;
        lambda.KoSafeMessage = message;
        return null;
    }

    internal static BoundingBox? ParseSalientBox(string coords, int origW, int origH) {
        if (origW == 0 || origH == 0) return null;
        string[] p = coords.Split(',');
        float x1 = float.Parse(p[0], CultureInfo.InvariantCulture);
        float y1 = float.Parse(p[1], CultureInfo.InvariantCulture);
        float x2 = float.Parse(p[2], CultureInfo.InvariantCulture);
        float y2 = float.Parse(p[3], CultureInfo.InvariantCulture);
        if (x2 <= x1 || y2 <= y1) return null;
        int bx = (int)(x1 * origW);
        int by = (int)(y1 * origH);
        int bw = (int)((x2 - x1) * origW);
        int bh = (int)((y2 - y1) * origH);
        return new BoundingBox {
            X = bx,
            Y = by,
            Width = bw,
            Height = bh,
            Left = bx,
            Top = by,
            Right = bx + bw,
            Bottom = by + bh
        };
    }

    //  Helpers for DetectSalientBoundingBox
    private static Mat ScaleDown(Mat src, int maxAnalysisSize) {
        int maxDim = Math.Max(src.Cols, src.Rows);
        if (maxDim <= maxAnalysisSize) return src.Clone();
        float scale = (float)maxAnalysisSize / maxDim;
        int nw = Math.Max(1, (int)(src.Cols * scale));
        int nh = Math.Max(1, (int)(src.Rows * scale));
        Mat dst = new Mat();
        Cv2.Resize(src, dst, new CvSize(nw, nh), interpolation: InterpolationFlags.Area);
        return dst;
    }

    private static void ApplySigmoid(Mat src, Mat dst, int w, int h, float sigmoidCenter, float sigmoidSlope) {
        int srcStride = (int)src.Step() / sizeof(float);
        int dstStride = (int)dst.Step() / sizeof(float);
        float[] srcData = new float[h * srcStride];
        float[] dstData = new float[h * dstStride];
        Marshal.Copy(src.Data, srcData, 0, srcData.Length);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) {
                float v = srcData[y * srcStride + x];
                dstData[y * dstStride + x] = 1f / (1f + MathF.Exp(-((v - sigmoidCenter) / sigmoidSlope)));
            }
        Marshal.Copy(dstData, 0, dst.Data, dstData.Length);
    }

    private static (int x1, int y1, int x2, int y2)? FindBbox(Mat img, int w, int h, float threshold) {
        int stride = (int)img.Step() / sizeof(float);
        float[] data = new float[h * stride];
        Marshal.Copy(img.Data, data, 0, data.Length);
        int x1 = w, y1 = h, x2 = -1, y2 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (data[y * stride + x] > threshold) {
                    if (x < x1) x1 = x;
                    if (x > x2) x2 = x;
                    if (y < y1) y1 = y;
                    if (y > y2) y2 = y;
                }
        return x2 >= x1 && y2 >= y1 ? (x1, y1, x2 + 1, y2 + 1) : null;
    }
}
