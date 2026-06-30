using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Core;

/// <summary>
/// Detects technical drawings and illustrations using CPU-only topological analysis.
/// Three signals must all pass: high-frequency edge density (many hard, defined lines),
/// near-white border region (flat background), and a low color-cluster count (few colors).
/// Sets the <c>is-illustration</c> ImageFeature.
/// </summary>
internal static class Analyzer_IsIllustration
{
    private const float MinEdgeDensity = 0.12f;
    private const float EdgeStrengthThreshold = 60f / 255f;
    private const float WhiteChannelMin = 230f / 255f;
    private const float BackgroundFlatnessMin = 0.80f;
    private const float BorderSampleDepth = 0.05f;
    private const int ColorBinsPerChannel = 8;
    private const int MaxColorClusters = 8;
    private const float MinClusterPopulation = 0.01f;

    /// <summary>
    /// Returns true when all three topological signals indicate a technical drawing or illustration.
    /// </summary>
    public static bool Analyze(Image<Rgba32> image)
    {
        int w = image.Width;
        int h = image.Height;
        float[,] gray = ToGrayscale(image, w, h);
        float[,] edges = ComputeGradientMagnitude(gray, w, h);

        return HasHighEdgeDensity(edges, w, h)
            && HasFlatWhiteBackground(image, w, h)
            && HasFewColorClusters(image, w, h);
    }

    private static float[,] ToGrayscale(Image<Rgba32> image, int w, int h)
    {
        float[,] gray = new float[h, w];
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y++)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < w; x++)
                {
                    Rgba32 p = row[x];
                    gray[y, x] = (0.299f * p.R + 0.587f * p.G + 0.114f * p.B) / 255f;
                }
            }
        });
        return gray;
    }

    // Sobel-style gradient magnitude: gx = right - left, gy = below - above.
    // Input values are [0,1]; output is sqrt(gx^2 + gy^2), also in [0, ~1.4].
    private static float[,] ComputeGradientMagnitude(float[,] gray, int w, int h)
    {
        float[,] mag = new float[h, w];
        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                float gx = gray[y, x + 1] - gray[y, x - 1];
                float gy = gray[y + 1, x] - gray[y - 1, x];
                mag[y, x] = MathF.Sqrt(gx * gx + gy * gy);
            }
        }
        return mag;
    }

    // Technical illustrations have many hard, high-frequency lines.
    // Edge threshold at ~60/255 targets clearly defined strokes over noise.
    private static bool HasHighEdgeDensity(float[,] edges, int w, int h)
    {
        int edgePx = 0;
        int total = (w - 2) * (h - 2);
        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
                if (edges[y, x] >= EdgeStrengthThreshold) edgePx++;
        return total > 0 && (float)edgePx / total >= MinEdgeDensity;
    }

    // Illustrations typically sit on a near-white background.
    // Sample strips along all four borders (5% depth) and check that most pixels are near-white.
    // Transparent pixels count as white (no background).
    private static bool HasFlatWhiteBackground(Image<Rgba32> image, int w, int h)
    {
        int depth = Math.Max(1, (int)(Math.Min(w, h) * BorderSampleDepth));
        int nearWhite = 0;
        int total = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y++)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                bool inTopOrBottomBand = y < depth || y >= h - depth;

                for (int x = 0; x < w; x++)
                {
                    bool inBorder = inTopOrBottomBand || x < depth || x >= w - depth;
                    if (!inBorder) continue;

                    Rgba32 px = row[x];
                    total++;
                    if (px.A < 128
                        || (px.R / 255f >= WhiteChannelMin
                         && px.G / 255f >= WhiteChannelMin
                         && px.B / 255f >= WhiteChannelMin))
                        nearWhite++;
                }
            }
        });

        return total > 0 && (float)nearWhite / total >= BackgroundFlatnessMin;
    }

    // Technical drawings use few colors: black lines on white, or a handful of palette colors.
    // Quantize RGB to a coarse grid and count populated buckets above a minimum population.
    private static bool HasFewColorClusters(Image<Rgba32> image, int w, int h)
    {
        int totalPixels = 0;
        int[] buckets = new int[ColorBinsPerChannel * ColorBinsPerChannel * ColorBinsPerChannel];

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y += 2)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < w; x += 2)
                {
                    Rgba32 px = row[x];
                    if (px.A < 128) continue;
                    totalPixels++;
                    int rBin = Math.Min(px.R * ColorBinsPerChannel / 256, ColorBinsPerChannel - 1);
                    int gBin = Math.Min(px.G * ColorBinsPerChannel / 256, ColorBinsPerChannel - 1);
                    int bBin = Math.Min(px.B * ColorBinsPerChannel / 256, ColorBinsPerChannel - 1);
                    buckets[rBin * ColorBinsPerChannel * ColorBinsPerChannel + gBin * ColorBinsPerChannel + bBin]++;
                }
            }
        });

        if (totalPixels == 0) return false;
        int minPop = Math.Max(1, (int)(totalPixels * MinClusterPopulation));
        int clusters = 0;
        foreach (int count in buckets)
            if (count >= minPop) clusters++;
        return clusters <= MaxColorClusters;
    }
}
