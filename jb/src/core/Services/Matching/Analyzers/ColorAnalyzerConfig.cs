using Prism.Config;

namespace Prism.Services.Matching;

/// <summary>
/// Thresholds and the named palette for Analyzer_DominantColors, Analyzer_ProductColor, and
/// Analyzer_BackgroundColor, bound from the "Colors" section of analyzer_Config.json. No defaults —
/// every value must be present in the JSON or deserialization fails loud.
/// </summary>
public sealed class ColorAnalyzerConfig : IValidatableConfig
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

    public void Validate()
    {
        List<string> problems = [];

        if (BucketCount < 1) problems.Add("Colors.BucketCount must be >= 1");
        if (BinsPerChannel < 2) problems.Add("Colors.BinsPerChannel must be >= 2");
        if (Palette.Count == 0) problems.Add("Colors.Palette must define at least one named color");

        if (problems.Count > 0) throw new PrismConfigurationException(string.Join("; ", problems));
    }
}
