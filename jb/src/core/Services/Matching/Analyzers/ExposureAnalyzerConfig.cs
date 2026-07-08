namespace Prism.Services.Matching;

/// <summary>
/// Thresholds for Analyzer_Exposure, bound from the "Exposure" section of analyzer_Config.json.
/// </summary>
public sealed class ExposureAnalyzerConfig
{
    /// <summary>Luminance at or above which a pixel counts as blown out.</summary>
    public float HighLuminance { get; init; } = 0.98f;

    /// <summary>Luminance at or below which a pixel counts as crushed.</summary>
    public float LowLuminance { get; init; } = 0.02f;

    /// <summary>Fraction of counted pixels beyond a luminance bound that flips the corresponding flag.</summary>
    public float FlaggedFraction { get; init; } = 0.25f;

    /// <summary>Confidence written on overexposed/underexposed.</summary>
    public float Confidence { get; init; } = 0.70f;
}
