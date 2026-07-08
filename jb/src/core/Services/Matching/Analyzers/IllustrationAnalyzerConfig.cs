namespace Prism.Services.Matching;

/// <summary>
/// Thresholds for Analyzer_IsIllustration, bound from the "IsIllustration" section of
/// analyzer_Config.json. Defaults mirror the previously hard-coded constants.
/// </summary>
public sealed class IllustrationAnalyzerConfig
{
    /// <summary>Minimum fraction of pixels that must be strong edges.</summary>
    public float MinEdgeDensity { get; init; } = 0.12f;

    /// <summary>Edge strength threshold on the [0,1] gradient scale (60/255 by default).</summary>
    public float EdgeStrengthThreshold { get; init; } = 60f / 255f;

    /// <summary>Per-channel minimum on the [0,1] scale for a pixel to count as near-white (230/255).</summary>
    public float WhiteChannelMin { get; init; } = 230f / 255f;

    /// <summary>Minimum fraction of border pixels that must be near-white or transparent.</summary>
    public float BackgroundFlatnessMin { get; init; } = 0.80f;

    /// <summary>Border strip depth as a fraction of the short image side.</summary>
    public float BorderSampleDepth { get; init; } = 0.05f;

    /// <summary>RGB quantization bins per channel for color-cluster counting.</summary>
    public int ColorBinsPerChannel { get; init; } = 8;

    /// <summary>Maximum populated color clusters for an image to qualify as an illustration.</summary>
    public int MaxColorClusters { get; init; } = 8;

    /// <summary>Minimum population (fraction of sampled pixels) for a bucket to count as a cluster.</summary>
    public float MinClusterPopulation { get; init; } = 0.01f;
}
