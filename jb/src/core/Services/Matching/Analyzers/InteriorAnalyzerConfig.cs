namespace Prism.Core;

/// <summary>
/// Thresholds for Analyzer_Interior, bound from the "Interior" section of analyzer_Config.json.
/// Defaults mirror the previously hard-coded constants.
/// </summary>
public sealed class InteriorAnalyzerConfig
{
    /// <summary>Minimum fraction of image area an interior region must cover.</summary>
    public float MinAreaFraction { get; init; } = 0.04f;

    /// <summary>Edge strength threshold on the [0,1] gradient scale (30/255 by default).</summary>
    public float MinEdgeStrength { get; init; } = 30f / 255f;

    /// <summary>Interior texture must be at least this much smoother than its surroundings.</summary>
    public float TextureDiffMin { get; init; } = 0.015f;
}
