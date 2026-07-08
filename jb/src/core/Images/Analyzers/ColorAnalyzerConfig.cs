namespace Prism.Core;

/// <summary>
/// Thresholds and the named palette for Analyzer_DominantColors, Analyzer_ProductColor, and
/// Analyzer_BackgroundColor, bound from the "Colors" section of analyzer_Config.json.
/// </summary>
public sealed class ColorAnalyzerConfig
{
    /// <summary>Number of dominant color buckets reported (user decision: 4).</summary>
    public int BucketCount { get; init; } = 4;

    /// <summary>RGB quantization bins per channel.</summary>
    public int BinsPerChannel { get; init; } = 8;

    /// <summary>Minimum share of sampled subject pixels for a bucket to count.</summary>
    public float MinBucketShare { get; init; } = 0.02f;

    /// <summary>Euclidean RGB distance below which a pixel is treated as background and excluded.</summary>
    public float BackgroundDistance { get; init; } = 0.12f;

    /// <summary>Minimum fraction of subject pixels that must survive background/skin exclusion; below this the colors stay UNKNOWN (white product on white background).</summary>
    public float MinSampleFraction { get; init; } = 0.02f;

    /// <summary>Confidence written on dominant-colors.</summary>
    public float DominantColorsConfidence { get; init; } = 0.70f;

    /// <summary>Confidence written on product-color (user decision: high, configurable).</summary>
    public float ProductColorConfidence { get; init; } = 0.80f;

    /// <summary>Confidence written on background-color.</summary>
    public float BackgroundColorConfidence { get; init; } = 0.85f;

    /// <summary>Named palette (name → #rrggbb); measured colors map to the nearest entry.</summary>
    public Dictionary<string, string> Palette { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "#000000", ["white"] = "#ffffff", ["grey"] = "#808080",
        ["red"] = "#cc0000", ["blue"] = "#0044cc", ["green"] = "#00aa44",
        ["yellow"] = "#ffdd00", ["orange"] = "#ff8800", ["pink"] = "#ff66aa",
        ["purple"] = "#7733aa", ["brown"] = "#8b5a2b", ["beige"] = "#d9c7a7"
    };
}
