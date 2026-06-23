using System.Globalization;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace Prism.Core;

/*
Here is a script that shows you how to get the bounding box using opencv/python
I want this. In dotnet if needed.

``` python
import cv2
import requests
import numpy as np
import matplotlib.pyplot as plt

def refined_edges(url):
    response = requests.get(url)
    image = np.asarray(bytearray(response.content), dtype=np.uint8)
    image = cv2.imdecode(image, cv2.IMREAD_COLOR)

    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)

    g = gray.astype(np.float32)

    local_mean = cv2.blur(g, (31, 31))
    local_sqmean = cv2.blur(g * g, (31, 31))

    local_contrast = np.sqrt(np.maximum(local_sqmean - local_mean * local_mean, 0))
    local_contrast = local_contrast - local_contrast.min()
    local_contrast = local_contrast / (local_contrast.max() + 1e-6)

    edges = cv2.Canny(gray, 80, 160).astype(np.float32) / 255.0

    edges_u8 = (edges * 255).astype(np.uint8)
    kernel = np.ones((7, 7), np.uint8)
    edges_dilated = cv2.dilate(edges_u8, kernel, iterations=1).astype(np.float32) / 255.0

    spatial = edges_dilated * local_contrast
    spatial = spatial - spatial.min()
    spatial = spatial / (spatial.max() + 1e-6)

    mask = 1.0 / (1.0 + np.exp(- (spatial - 0.5) / 0.15))

    refined_edges = edges * mask
    binary = (refined_edges > 0.2).astype(np.uint8) * 255

    return binary


url = "https://images.unsplash.com/photo-1444464666168-49d633b86797?w=800&auto=format&fit=crop&q=60"
result = refined_edges(url)

plt.imshow(result, cmap="gray")
plt.axis("off")
plt.show()
```*/

/// <summary>
/// Computes the salient-object bounding box for each image before transform routing.
/// Direct C# port of the Python reference above using OpenCvSharp4.
/// Writes "salient-bbox" as normalized [0–1] ratios (left,top,right,bottom) to the image's
/// feature snapshot. Leaves the feature at UNKNOWN on any failure.
/// </summary>
public static class ImagePreProcessor {
    private const int   MaxAnalysisSize  = 512;
    private const float CannyThreshold1  = 80f;
    private const float CannyThreshold2  = 160f;
    private const float SigmoidCenter    = 0.5f;
    private const float SigmoidSlope     = 0.15f;
    private const float EdgeThreshold    = 0.2f;
    private const float MinBboxAreaRatio = 0.01f;

    /// <summary>
    /// Computes the salient-object bounding box from the image at <paramref name="imagePath"/>
    /// and writes it to <paramref name="lambda"/>.Features as "salient-bbox".
    /// When detection fails or the path is unavailable, the feature is left unchanged.
    /// </summary>
    public static void Preprocess( ImageRecord_LAMBDA lambda, string? imagePath ) {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            return;

        try {
            using Mat gray8 = Cv2.ImRead(imagePath, ImreadModes.Grayscale);
            if (gray8.Empty()) return;

            using Mat img = ScaleDown(gray8);
            int w = img.Cols, h = img.Rows;

            // g = gray.astype(float32) / 255
            using Mat grayF = new Mat();
            img.ConvertTo(grayF, MatType.CV_32F, 1.0 / 255.0);

            // local_mean = blur(g, 31); local_sqmean = blur(g*g, 31)
            using Mat localMean   = new Mat();
            using Mat graySquared = new Mat();
            using Mat localSqMean = new Mat();
            Cv2.Blur(grayF, localMean, new Size(31, 31));
            Cv2.Multiply(grayF, grayF, graySquared);
            Cv2.Blur(graySquared, localSqMean, new Size(31, 31));

            // local_contrast = sqrt(max(sqmean - mean², 0)), normalize [0,1]
            using Mat mean2         = new Mat();
            using Mat localContrast = new Mat();
            Cv2.Multiply(localMean, localMean, mean2);
            Cv2.Subtract(localSqMean, mean2, localContrast);
            Cv2.Threshold(localContrast, localContrast, 0, 0, ThresholdTypes.Tozero);
            Cv2.Sqrt(localContrast, localContrast);
            Cv2.Normalize(localContrast, localContrast, 0.0, 1.0, NormTypes.MinMax);

            // edges = Canny(gray8, 80, 160) / 255
            using Mat edges8 = new Mat();
            Cv2.Canny(img, edges8, CannyThreshold1, CannyThreshold2, 3);
            using Mat edgesF = new Mat();
            edges8.ConvertTo(edgesF, MatType.CV_32F, 1.0 / 255.0);

            // edges_dilated = dilate(edges8, 7×7) / 255
            using Mat kernel7x7     = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(7, 7));
            using Mat edges8Dilated = new Mat();
            Cv2.Dilate(edges8, edges8Dilated, kernel7x7, iterations: 1);
            using Mat edgesDilatedF = new Mat();
            edges8Dilated.ConvertTo(edgesDilatedF, MatType.CV_32F, 1.0 / 255.0);

            // spatial = edges_dilated * local_contrast, normalize [0,1]
            using Mat spatial = new Mat();
            Cv2.Multiply(edgesDilatedF, localContrast, spatial);
            Cv2.Normalize(spatial, spatial, 0.0, 1.0, NormTypes.MinMax);

            // mask = sigmoid(spatial); refined = edges * mask
            using Mat mask    = new Mat(h, w, MatType.CV_32F);
            using Mat refined = new Mat();
            ApplySigmoid(spatial, mask, w, h);
            Cv2.Multiply(edgesF, mask, refined);

            // binary = (refined > 0.2) → bounding rect of nonzero pixels
            (int x1, int y1, int x2, int y2)? bbox = FindBbox(refined, w, h, EdgeThreshold);
            if (bbox is null) return;

            float bboxArea = (float)(bbox.Value.x2 - bbox.Value.x1) * (bbox.Value.y2 - bbox.Value.y1);
            if (bboxArea / (w * h) < MinBboxAreaRatio) return;

            string value = string.Format(CultureInfo.InvariantCulture, "{0:F4},{1:F4},{2:F4},{3:F4}",
                (float)bbox.Value.x1 / w,
                (float)bbox.Value.y1 / h,
                (float)bbox.Value.x2 / w,
                (float)bbox.Value.y2 / h);

            lambda.Features.Set("salient-bbox", value, 0.85, "opencv-canny");
        } catch { /* Leave salient-bbox at UNKNOWN on any failure. */ }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Mat ScaleDown( Mat src ) {
        int maxDim = Math.Max(src.Cols, src.Rows);
        if (maxDim <= MaxAnalysisSize) return src.Clone();
        float scale = (float)MaxAnalysisSize / maxDim;
        int nw = Math.Max(1, (int)(src.Cols * scale));
        int nh = Math.Max(1, (int)(src.Rows * scale));
        Mat dst = new Mat();
        Cv2.Resize(src, dst, new Size(nw, nh), interpolation: InterpolationFlags.Area);
        return dst;
    }

    private static void ApplySigmoid( Mat src, Mat dst, int w, int h ) {
        int srcStride = (int)src.Step() / sizeof(float);
        int dstStride = (int)dst.Step() / sizeof(float);
        float[] srcData = new float[h * srcStride];
        float[] dstData = new float[h * dstStride];
        Marshal.Copy(src.Data, srcData, 0, srcData.Length);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) {
                float v = srcData[y * srcStride + x];
                dstData[y * dstStride + x] = 1f / (1f + MathF.Exp(-((v - SigmoidCenter) / SigmoidSlope)));
            }
        Marshal.Copy(dstData, 0, dst.Data, dstData.Length);
    }

    private static (int x1, int y1, int x2, int y2)? FindBbox( Mat img, int w, int h, float threshold ) {
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
