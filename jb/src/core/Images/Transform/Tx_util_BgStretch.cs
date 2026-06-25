using OpenCvSharp;

namespace Prism.Core;

/// <summary>
/// Tiered background-fill helper. Not an <see cref="IImageTransformation"/> implementor.
/// Called as a sub-step from <see cref="Tx_CenterAndStretch"/> and <see cref="Tx_DetailCropper"/>.
/// All work is in-memory — no disk writes.
/// </summary>
public static class Tx_util_BgStretch {

    private const float Tier1MaxRatio = 1.25f;
    private const float Tier2MaxRatio = 1.42f;
    private const float Tier4MinRatio = 2.50f;
    private const int   FeatherPx     = 16;      // linear-blend seam width (tiers 1 and 2 only)

    /// <summary>
    /// Webservice form: uniformly expands the source JPEG by <paramref name="upscale_factor"/>
    /// in both dimensions, centering the source, and fills the new border.
    /// Extension ratio = upscale_factor².
    /// </summary>
    public static byte[] Process( byte[] arr, int stride, float upscale_factor ) {
        using Mat src = Cv2.ImDecode(arr, ImreadModes.Color);
        if (src.Empty()) return arr;

        int tW = Math.Max(1, (int)Math.Round(src.Cols * upscale_factor));
        int tH = Math.Max(1, (int)Math.Round(src.Rows * upscale_factor));
        using Mat result = StretchMat(src, tW, tH, (tW - src.Cols) / 2, (tH - src.Rows) / 2);
        Cv2.ImEncode(".jpg", result, out byte[] encoded);
        return encoded;
    }

    /// <summary>
    /// Sub-step form: places <paramref name="sourceJpeg"/> at (<paramref name="srcX"/>,
    /// <paramref name="srcY"/>) on a (<paramref name="canvasW"/>×<paramref name="canvasH"/>) canvas
    /// and fills uncovered edges using the appropriate tier.
    /// </summary>
    internal static byte[] Stretch( byte[] sourceJpeg, int canvasW, int canvasH, int srcX, int srcY ) {
        using Mat src = Cv2.ImDecode(sourceJpeg, ImreadModes.Color);
        if (src.Empty()) return sourceJpeg;
        using Mat result = StretchMat(src, canvasW, canvasH, srcX, srcY);
        Cv2.ImEncode(".jpg", result, out byte[] encoded);
        return encoded;
    }

    private static Mat StretchMat( Mat src, int canvasW, int canvasH, int srcX, int srcY ) {
        float ratio = (long)canvasW * canvasH / (float)((long)src.Cols * src.Rows);

        if (ratio > Tier4MinRatio) return WhiteFill(src, canvasW, canvasH, srcX, srcY);
        if (ratio > Tier2MaxRatio) return InpaintFill(src, canvasW, canvasH, srcX, srcY);
        if (ratio > Tier1MaxRatio) return ContentAwareFill(src, canvasW, canvasH, srcX, srcY);
        return EdgeExtendFill(src, canvasW, canvasH, srcX, srcY);
    }

    // Tier 1 (≤125%) — reflect-101 border pixels outward; feather the seam
    private static Mat EdgeExtendFill( Mat src, int canvasW, int canvasH, int srcX, int srcY ) {
        Mat canvas = new Mat();
        Cv2.CopyMakeBorder(src, canvas,
            srcY, canvasH - src.Rows - srcY,
            srcX, canvasW - src.Cols - srcX,
            BorderTypes.Reflect101);
        FeatherSeam(canvas, srcX, srcY, src.Cols, src.Rows);
        return canvas;
    }

    // Tier 2 (≤142%) — reflect border (slightly wider pattern than 101); feather the seam
    private static Mat ContentAwareFill( Mat src, int canvasW, int canvasH, int srcX, int srcY ) {
        Mat canvas = new Mat();
        Cv2.CopyMakeBorder(src, canvas,
            srcY, canvasH - src.Rows - srcY,
            srcX, canvasW - src.Cols - srcX,
            BorderTypes.Reflect);
        FeatherSeam(canvas, srcX, srcY, src.Cols, src.Rows);
        return canvas;
    }

    // Tier 3 (>142%) — OpenCV INPAINT_TELEA; seam handled implicitly by inpainting
    private static Mat InpaintFill( Mat src, int canvasW, int canvasH, int srcX, int srcY ) {
        Mat canvas = new Mat(canvasH, canvasW, src.Type(), Scalar.White);
        using Mat mask = new Mat(canvasH, canvasW, MatType.CV_8UC1, Scalar.All(255));
        src.CopyTo(canvas[new Rect(srcX, srcY, src.Cols, src.Rows)]);
        mask[new Rect(srcX, srcY, src.Cols, src.Rows)].SetTo(Scalar.All(0));
        Mat result = new Mat();
        Cv2.Inpaint(canvas, mask, result, inpaintRadius: 5, InpaintMethod.Telea);
        canvas.Dispose();
        return result;
    }

    // Tier 4 (>250%) — solid white canvas with source placed at offset
    private static Mat WhiteFill( Mat src, int canvasW, int canvasH, int srcX, int srcY ) {
        Mat canvas = new Mat(canvasH, canvasW, src.Type(), Scalar.White);
        src.CopyTo(canvas[new Rect(srcX, srcY, src.Cols, src.Rows)]);
        return canvas;
    }

    // Linear-gradient blend at the seam between source content and filled region.
    // For each seam side: alpha = 1 adjacent to source → 0 at outermost filled pixel.
    // Row-based for top/bottom (contiguous memory); pixel-loop for left/right (non-contiguous columns).
    // No Gaussian blur used anywhere.
    private static void FeatherSeam( Mat canvas, int sx, int sy, int sw, int sh ) {
        int canvasW = canvas.Cols, canvasH = canvas.Rows;
        int padL = sx, padT = sy;
        int padR = canvasW - sx - sw, padB = canvasH - sy - sh;

        // Top/bottom seams — rows are contiguous, use AddWeighted on row submatrices
        int fwT = Math.Min(FeatherPx, padT);
        for (int d = 0; d < fwT; d++) {
            float alpha = (float)(fwT - d) / fwT;
            using Mat srcRow    = canvas[new Rect(sx, sy,          sw, 1)];
            using Mat filledRow = canvas[new Rect(sx, sy - 1 - d,  sw, 1)];
            using Mat blended   = new Mat();
            Cv2.AddWeighted(srcRow, alpha, filledRow, 1.0 - alpha, 0, blended);
            blended.CopyTo(filledRow);
        }

        int fwB = Math.Min(FeatherPx, padB);
        for (int d = 0; d < fwB; d++) {
            float alpha = (float)(fwB - d) / fwB;
            using Mat srcRow    = canvas[new Rect(sx, sy + sh - 1,  sw, 1)];
            using Mat filledRow = canvas[new Rect(sx, sy + sh + d,   sw, 1)];
            using Mat blended   = new Mat();
            Cv2.AddWeighted(srcRow, alpha, filledRow, 1.0 - alpha, 0, blended);
            blended.CopyTo(filledRow);
        }

        // Left/right seams — columns are non-contiguous; blend pixel by pixel
        int fwL = Math.Min(FeatherPx, padL);
        int fwR = Math.Min(FeatherPx, padR);

        for (int row = sy; row < sy + sh; row++) {
            for (int d = 0; d < fwL; d++) {
                float alpha = (float)(fwL - d) / fwL;
                canvas.Set<Vec3b>(row, sx - 1 - d,
                    Blend(canvas.Get<Vec3b>(row, sx), canvas.Get<Vec3b>(row, sx - 1 - d), alpha));
            }
            for (int d = 0; d < fwR; d++) {
                float alpha = (float)(fwR - d) / fwR;
                canvas.Set<Vec3b>(row, sx + sw + d,
                    Blend(canvas.Get<Vec3b>(row, sx + sw - 1), canvas.Get<Vec3b>(row, sx + sw + d), alpha));
            }
        }
    }

    private static Vec3b Blend( Vec3b src, Vec3b dst, float srcAlpha ) => new(
        (byte)Math.Round(src.Item0 * srcAlpha + dst.Item0 * (1f - srcAlpha)),
        (byte)Math.Round(src.Item1 * srcAlpha + dst.Item1 * (1f - srcAlpha)),
        (byte)Math.Round(src.Item2 * srcAlpha + dst.Item2 * (1f - srcAlpha)));
}
