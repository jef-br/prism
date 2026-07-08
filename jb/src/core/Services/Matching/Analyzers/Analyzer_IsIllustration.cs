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
    /// <summary>
    /// Returns true when all three topological signals indicate a technical drawing or illustration.
    /// Thresholds come from analyzer_Config.json.
    /// </summary>
    public static bool Analyze(Image<Rgba32> image, IllustrationAnalyzerConfig cfg)
    {
        int w = image.Width;
        int h = image.Height;
        float[,] gray = AnalyzerMath.ToGrayscale(image, w, h);
        float[,] edges = AnalyzerMath.ComputeGradientMagnitude(gray, w, h);

        return HasHighEdgeDensity(edges, w, h, cfg)
            && HasFlatWhiteBackground(image, w, h, cfg)
            && HasFewColorClusters(image, w, h, cfg);
    }

    // Technical illustrations have many hard, high-frequency lines.
    // Edge threshold at ~60/255 targets clearly defined strokes over noise.
    private static bool HasHighEdgeDensity(float[,] edges, int w, int h, IllustrationAnalyzerConfig cfg)
    {
        int edgePx = 0;
        int total = (w - 2) * (h - 2);
        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
                if (edges[y, x] >= cfg.EdgeStrengthThreshold) edgePx++;
        return total > 0 && (float)edgePx / total >= cfg.MinEdgeDensity;
    }

    // Illustrations typically sit on a near-white background.
    // Sample strips along all four borders (5% depth) and check that most pixels are near-white.
    // Transparent pixels count as white (no background).
    private static bool HasFlatWhiteBackground(Image<Rgba32> image, int w, int h, IllustrationAnalyzerConfig cfg)
    {
        int depth = Math.Max(1, (int)(Math.Min(w, h) * cfg.BorderSampleDepth));
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
                        || (px.R / 255f >= cfg.WhiteChannelMin
                         && px.G / 255f >= cfg.WhiteChannelMin
                         && px.B / 255f >= cfg.WhiteChannelMin))
                        nearWhite++;
                }
            }
        });

        return total > 0 && (float)nearWhite / total >= cfg.BackgroundFlatnessMin;
    }

    // Technical drawings use few colors: black lines on white, or a handful of palette colors.
    // Quantize RGB to a coarse grid and count populated buckets above a minimum population.
    private static bool HasFewColorClusters(Image<Rgba32> image, int w, int h, IllustrationAnalyzerConfig cfg)
    {
        int bins = cfg.ColorBinsPerChannel;
        int totalPixels = 0;
        int[] buckets = new int[bins * bins * bins];

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
                    int rBin = Math.Min(px.R * bins / 256, bins - 1);
                    int gBin = Math.Min(px.G * bins / 256, bins - 1);
                    int bBin = Math.Min(px.B * bins / 256, bins - 1);
                    buckets[rBin * bins * bins + gBin * bins + bBin]++;
                }
            }
        });

        if (totalPixels == 0) return false;
        int minPop = Math.Max(1, (int)(totalPixels * cfg.MinClusterPopulation));
        int clusters = 0;
        foreach (int count in buckets)
            if (count >= minPop) clusters++;
        return clusters <= cfg.MaxColorClusters;
    }
}
