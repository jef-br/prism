using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Services.Matching;

/// <summary>
/// Detects technical drawings and illustrations using CPU-only topological analysis.
/// Three signals must all pass: high-frequency edge density (many hard, defined lines),
/// near-white border region (flat background), and a low color-cluster count (few colors).
/// Sets the <c>is-illustration</c> ImageFeature.
/// </summary>
public static class Analyzer_IsIllustration
{
    private const int AlphaOpaqueThreshold = 128;
    private const float MaxChannelValueF = 255f;

    /// <summary>
    /// Thresholds for Analyzer_IsIllustration, bound from the "IsIllustration" section of
    /// analyzer_Config.json. No defaults — every value must be present in the JSON or deserialization
    /// fails loud.
    /// </summary>
    public sealed class Config : IValidatableConfig
    {
        // Validation bounds, not tunable: BorderSampleDepth is a fraction of the short image side, so it
        // must stay below half; ColorBinsPerChannel needs at least 2 bins to quantize anything.
        private const float BorderSampleDepthUpperBound = 0.5f;
        private const int MinColorBinsPerChannel = 2;

        /// <summary>Minimum fraction of pixels that must be strong edges.</summary>
        public required float MinEdgeDensity { get; init; }

        /// <summary>Edge strength threshold on the [0,1] gradient scale (60/255 by default).</summary>
        public required float EdgeStrengthThreshold { get; init; }

        /// <summary>Per-channel minimum on the [0,1] scale for a pixel to count as near-white (230/255).</summary>
        public required float WhiteChannelMin { get; init; }

        /// <summary>Minimum fraction of border pixels that must be near-white or transparent.</summary>
        public required float BackgroundFlatnessMin { get; init; }

        /// <summary>Border strip depth as a fraction of the short image side.</summary>
        public required float BorderSampleDepth { get; init; }

        /// <summary>RGB quantization bins per channel for color-cluster counting.</summary>
        public required int ColorBinsPerChannel { get; init; }

        /// <summary>Maximum populated color clusters for an image to qualify as an illustration.</summary>
        public required int MaxColorClusters { get; init; }

        /// <summary>Minimum population (fraction of sampled pixels) for a bucket to count as a cluster.</summary>
        public required float MinClusterPopulation { get; init; }

        public void Validate()
        {
            List<string> problems = [];

            if (this.MinEdgeDensity is <= 0f or >= 1f) problems.Add("IsIllustration.MinEdgeDensity must be in (0,1)");
            if (this.EdgeStrengthThreshold <= 0f) problems.Add("IsIllustration.EdgeStrengthThreshold must be > 0");
            if (this.BackgroundFlatnessMin is <= 0f or > 1f) problems.Add("IsIllustration.BackgroundFlatnessMin must be in (0,1]");
            if (this.BorderSampleDepth is <= 0f or >= BorderSampleDepthUpperBound) problems.Add("IsIllustration.BorderSampleDepth must be in (0,0.5)");
            if (this.ColorBinsPerChannel < MinColorBinsPerChannel) problems.Add("IsIllustration.ColorBinsPerChannel must be >= 2");
            if (this.MaxColorClusters < 1) problems.Add("IsIllustration.MaxColorClusters must be >= 1");

            if (problems.Count > 0) throw new PrismConfigurationException(string.Join("; ", problems));
        }
    }

    /// <summary>
    /// Returns true when all three topological signals indicate a technical drawing or illustration.
    /// Thresholds come from analyzer_Config.json.
    /// </summary>
    public static bool Analyze(Image<Rgba32> image, Config cfg)
    {
        int w = image.Width;
        int h = image.Height;
        float[,] gray = AnalyzerMath.ToGrayscale(image, w, h);
        float[,] edges = AnalyzerMath.ComputeGradientMagnitude(gray, w, h);

        return HasHighEdgeDensity(edges, w, h, cfg)
            && HasFlatWhiteBackground(image, w, h, cfg)
            && HasFewColorClusters(image, w, h, cfg);
    }

    // EdgeStrengthThreshold is a threshold on AnalyzerMath.ComputeGradientMagnitude's output — a
    // gradient-magnitude value on the normalized [0,1] luminance scale (range up to ~1.4), not a raw
    // pixel value. 0.2353 was chosen to be numerically equivalent to a ~60/255 raw luminance step
    // between adjacent pixels.
    private static bool HasHighEdgeDensity(float[,] edges, int w, int h, Config cfg)
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
    private static bool HasFlatWhiteBackground(Image<Rgba32> image, int w, int h, Config cfg)
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
                    if (px.A < AlphaOpaqueThreshold
                        || (px.R / MaxChannelValueF >= cfg.WhiteChannelMin
                         && px.G / MaxChannelValueF >= cfg.WhiteChannelMin
                         && px.B / MaxChannelValueF >= cfg.WhiteChannelMin))
                        nearWhite++;
                }
            }
        });

        return total > 0 && (float)nearWhite / total >= cfg.BackgroundFlatnessMin;
    }

    // Technical drawings use few colors: black lines on white, or a handful of palette colors.
    // Quantize RGB to a coarse grid and count populated buckets above a minimum population.
    private static bool HasFewColorClusters(Image<Rgba32> image, int w, int h, Config cfg)
    {
        int bins = cfg.ColorBinsPerChannel;
        int totalPixels = 0;
        int[] buckets = new int[bins * bins * bins];

        // Stride 2 subsamples every other pixel/row — perf, not a tunable threshold.
#pragma warning disable S109
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y += 2) {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < w; x += 2) {
                    Rgba32 px = row[x];
                    if (px.A < AlphaOpaqueThreshold) continue;
                    totalPixels++;
                    int rBin = Math.Min(px.R * bins / 256, bins - 1);
                    int gBin = Math.Min(px.G * bins / 256, bins - 1);
                    int bBin = Math.Min(px.B * bins / 256, bins - 1);
                    buckets[rBin * bins * bins + gBin * bins + bBin]++;
                }
            }
        });
#pragma warning restore S109

        if (totalPixels == 0) return false;
        int minPop = Math.Max(1, (int)(totalPixels * cfg.MinClusterPopulation));
        int clusters = 0;
        foreach (int count in buckets)
            if (count >= minPop) clusters++;
        return clusters <= cfg.MaxColorClusters;
    }
}
