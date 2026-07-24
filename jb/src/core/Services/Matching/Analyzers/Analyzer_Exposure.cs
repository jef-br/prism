using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Services.Matching;

/*
 Proposed workings
 -----------------
 Sampled luminance histogram over the image. Pixels near the solid background color are
 excluded first, so a packshot on pure white is not flagged overexposed — only the product
 itself counts. overexposed flips true when the blown-out (>= HighLuminance) fraction of
 counted pixels exceeds FlaggedFraction; underexposed symmetrically at <= LowLuminance.
*/

/// <summary>
/// Sets the <c>overexposed</c> and <c>underexposed</c> ImageFeatures from the subject's
/// luminance distribution.
/// </summary>
public static class Analyzer_Exposure
{
    /// <summary>
    /// Thresholds for Analyzer_Exposure, bound from the "Exposure" section of analyzer_Config.json.
    /// No defaults — every value must be present in the JSON or deserialization fails loud.
    /// </summary>
    public sealed class Config : IValidatableConfig
    {
        /// <summary>Luminance at or above which a pixel counts as blown out.</summary>
        public required float HighLuminance { get; init; }

        /// <summary>Luminance at or below which a pixel counts as crushed.</summary>
        public required float LowLuminance { get; init; }

        /// <summary>Fraction of counted pixels beyond a luminance bound that flips the corresponding flag.</summary>
        public required float FlaggedFraction { get; init; }

        /// <summary>Confidence written on overexposed/underexposed.</summary>
        public required float Confidence { get; init; }

        public void Validate()
        {
            List<string> problems = [];

            if (this.HighLuminance is <= 0f or > 1f) problems.Add("Exposure.HighLuminance must be in (0,1]");
            if (this.LowLuminance is < 0f or >= 1f) problems.Add("Exposure.LowLuminance must be in [0,1)");
            if (this.FlaggedFraction is <= 0f or > 1f) problems.Add("Exposure.FlaggedFraction must be in (0,1]");

            if (problems.Count > 0) throw new PrismConfigurationException(string.Join("; ", problems));
        }
    }

    public static void Analyze(Image<Rgba32> image, ImageFeatureSnapshot snapshot, Config cfg, ColorAnalyzerConfig colorCfg)
    {
        bool excludeBackground = string.Equals(snapshot.GetValue("background-type"), "SOLIDCOLOR", StringComparison.OrdinalIgnoreCase);
        (float bgR, float bgG, float bgB) = excludeBackground ? AnalyzerMath.EstimateBackgroundColor(image) : (0f, 0f, 0f);

        int counted = 0, high = 0, low = 0;

        // Stride 2 subsamples every other pixel/row (perf, not a tunable threshold); 128 is the
        // alpha-opaque cutoff on the [0,255] channel scale — both structural, never tuned.
#pragma warning disable S109
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y += 2)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x += 2)
                {
                    Rgba32 p = row[x];
                    if (p.A < 128) continue;

                    float r = p.R / 255f, g = p.G / 255f, b = p.B / 255f;
                    if (excludeBackground && AnalyzerMath.ColorDistance(r, g, b, bgR, bgG, bgB) < colorCfg.BackgroundDistance) continue;

                    counted++;
                    float luminance = 0.299f * r + 0.587f * g + 0.114f * b;
                    if (luminance >= cfg.HighLuminance) high++;
                    else if (luminance <= cfg.LowLuminance) low++;
                }
            }
        });
#pragma warning restore S109

        if (counted == 0) return;

        snapshot.Set("overexposed", (float)high / counted >= cfg.FlaggedFraction ? "true" : "false", cfg.Confidence, "imagesharp");
        snapshot.Set("underexposed", (float)low / counted >= cfg.FlaggedFraction ? "true" : "false", cfg.Confidence, "imagesharp");
    }
}
