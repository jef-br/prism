namespace Prism.Services.Matching;

/// <summary>
/// Thresholds for Analyzer_Interior, bound from the "Interior" section of analyzer_Config.json.
/// Defaults mirror the previously hard-coded constants.
/// </summary>
public sealed class InteriorAnalyzerConfig
{
    /// <summary>Minimum fraction of image area an interior region must cover.</summary>
    public required float MinAreaFraction { get; init; }

    /// <summary>Edge strength threshold on the [0,1] gradient scale (30/255 by default).</summary>
    public required float MinEdgeStrength { get; init; }

    /// <summary>Interior texture must be at least this much smoother than its surroundings.</summary>
    public required float TextureDiffMin { get; init; }
}
