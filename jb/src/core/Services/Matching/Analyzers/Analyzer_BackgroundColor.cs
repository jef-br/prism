using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Services.Matching;

/*
 Proposed workings
 -----------------
 Only meaningful on a uniform background: when background-type == SOLIDCOLOR, the mean color
 of the 5% border strips (the same estimate the edge detector uses) names the background via
 the configured palette. On REALLIFE/UNKNOWN backgrounds the feature stays UNKNOWN — a mean
 over a lifestyle scene names nothing real.
*/

/// <summary>
/// Names the background color of solid-background images via the configured palette.
/// </summary>
internal static class Analyzer_BackgroundColor {
    public static void Analyze( Image<Rgba32> image, ImageFeatureSnapshot snapshot, ColorAnalyzerConfig cfg ) {
        if (!string.Equals(snapshot.GetValue("background-type"), "SOLIDCOLOR", StringComparison.OrdinalIgnoreCase)) return;

        (float r, float g, float b) = AnalyzerMath.EstimateBackgroundColor(image);
        string name = AnalyzerMath.NearestPaletteName(r, g, b, cfg.Palette);
        snapshot.Set("background-color", name, cfg.BackgroundColorConfidence, "imagesharp");
    }
}
