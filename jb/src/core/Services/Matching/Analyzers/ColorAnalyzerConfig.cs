namespace Prism.Services.Matching;

/// <summary>
/// Thresholds and the named palette for Analyzer_DominantColors, Analyzer_ProductColor, and
/// Analyzer_BackgroundColor, bound from the "Colors" section of analyzer_Config.json.
/// </summary>
public sealed class ColorAnalyzerConfig
{
    /// <summary>Number of dominant color buckets reported (user decision: 4).</summary>
    public required int BucketCount { get; init; }

    /// <summary>RGB quantization bins per channel.</summary>
    public required int BinsPerChannel { get; init; }

    /// <summary>Minimum share of sampled subject pixels for a bucket to count.</summary>
    public required float MinBucketShare { get; init; }

    /// <summary>Euclidean RGB distance below which a pixel is treated as background and excluded.</summary>
    public required float BackgroundDistance { get; init; }

    /// <summary>Minimum fraction of subject pixels that must survive background/skin exclusion; below this the colors stay UNKNOWN (white product on white background).</summary>
    public required float MinSampleFraction { get; init; }

    /// <summary>Confidence written on dominant-colors.</summary>
    public required float DominantColorsConfidence { get; init; }

    /// <summary>Confidence written on product-color (user decision: high, configurable).</summary>
    public required float ProductColorConfidence { get; init; }

    /// <summary>Confidence written on background-color.</summary>
    public required float BackgroundColorConfidence { get; init; }

    /// <summary>Named palette (name → #rrggbb); measured colors map to the nearest entry.</summary>
    public required Dictionary<string, string> Palette { get; init; }
}
