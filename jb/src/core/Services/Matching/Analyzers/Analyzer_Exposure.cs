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
internal static class Analyzer_Exposure
{
    public static void Analyze(Image<Rgba32> image, ImageFeatureSnapshot snapshot, ExposureAnalyzerConfig cfg, ColorAnalyzerConfig colorCfg)
    {
        bool excludeBackground = string.Equals(snapshot.GetValue("background-type"), "SOLIDCOLOR", StringComparison.OrdinalIgnoreCase);
        (float bgR, float bgG, float bgB) = excludeBackground ? AnalyzerMath.EstimateBackgroundColor(image) : (0f, 0f, 0f);

        int counted = 0, high = 0, low = 0;

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

        if (counted == 0) return;

        snapshot.Set("overexposed", (float)high / counted >= cfg.FlaggedFraction ? "true" : "false", cfg.Confidence, "imagesharp");
        snapshot.Set("underexposed", (float)low / counted >= cfg.FlaggedFraction ? "true" : "false", cfg.Confidence, "imagesharp");
    }
}
