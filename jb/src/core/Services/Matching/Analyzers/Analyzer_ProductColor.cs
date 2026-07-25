namespace Prism.Services.Matching;

/*
 Proposed workings
 -----------------
 The largest surviving foreground color bucket (background and skin already excluded by
 Analyzer_DominantColors) is the product color; it maps to the nearest named color in the
 configured palette. Confidence is configurable (default high, 0.80); a later CLIP
 product-color tag with a stronger score may overwrite it — intended, CLIP is higher-trust
 for named colors.
*/

/// <summary>
/// Names the product color from the dominant foreground bucket via the configured palette.
/// </summary>
internal static class Analyzer_ProductColor {
    public static void Analyze(IReadOnlyList<ColorBucket> buckets, ImageFeatureSnapshot snapshot, ColorAnalyzerConfig cfg) {
        if (buckets.Count == 0) return;

        ColorBucket top = buckets[0];
        string name = AnalyzerMath.NearestPaletteName(top.R, top.G, top.B, cfg.Palette);
        snapshot.Set("product-color", name, cfg.ProductColorConfidence, "imagesharp");
    }
}
