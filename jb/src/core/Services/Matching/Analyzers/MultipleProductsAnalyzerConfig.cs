namespace Prism.Services.Matching;

/// <summary>
/// Thresholds for Analyzer_MultipleProducts, bound from the "MultipleProducts" section of
/// analyzer_Config.json.
/// </summary>
public sealed class MultipleProductsAnalyzerConfig
{
    /// <summary>IoU above which two non-person detections count as overlapping.</summary>
    public required float OverlapIou { get; init; }

    /// <summary>Confidence written on multiple-products/overlap-count.</summary>
    public required float Confidence { get; init; }
}
