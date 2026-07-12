namespace Prism.Services.Matching;

/// <summary>
/// Thresholds for Analyzer_IsIllustration, bound from the "IsIllustration" section of
/// analyzer_Config.json. Defaults mirror the previously hard-coded constants.
/// </summary>
public sealed class IllustrationAnalyzerConfig
{
    /// <summary>Minimum fraction of pixels that must be strong edges.</summary>
    public required float MinEdgeDensity { get; init; }

    /// <summary>Edge strength threshold on the [0,1] gradient scale (60/255 by default).</summary>
    public required float EdgeStrengthThreshold { get; init; }

    /// <summary>Per-channel minimum on the [0,1] scale for a pixel to count as near-white (230/255).</summary>
    public required float WhiteChannelMin { get; init; }

    /// <summary>Minimum fraction of border pixels that must be near-white or transparent.</summary>
    public required float BackgroundFlatnessMin { get; init; }

    /// <summary>Border strip depth as a fraction of the short image side.</summary>
    public required float BorderSampleDepth { get; init; }

    /// <summary>RGB quantization bins per channel for color-cluster counting.</summary>
    public required int ColorBinsPerChannel { get; init; }

    /// <summary>Maximum populated color clusters for an image to qualify as an illustration.</summary>
    public required int MaxColorClusters { get; init; }

    /// <summary>Minimum population (fraction of sampled pixels) for a bucket to count as a cluster.</summary>
    public required float MinClusterPopulation { get; init; }
}
