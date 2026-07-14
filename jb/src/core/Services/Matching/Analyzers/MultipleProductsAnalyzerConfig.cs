using Prism.Config;

namespace Prism.Services.Matching;

/// <summary>
/// Thresholds for Analyzer_MultipleProducts, bound from the "MultipleProducts" section of
/// analyzer_Config.json. No defaults — every value must be present in the JSON or deserialization
/// fails loud.
/// </summary>
public sealed class MultipleProductsAnalyzerConfig : IValidatableConfig
{
    /// <summary>IoU above which two non-person detections count as overlapping.</summary>
    public required float OverlapIou { get; init; }

    /// <summary>Confidence written on multiple-products/overlap-count.</summary>
    public required float Confidence { get; init; }

    public void Validate()
    {
        if (OverlapIou is <= 0f or >= 1f)
            throw new PrismConfigurationException("MultipleProducts.OverlapIou must be in (0,1)");
    }
}
