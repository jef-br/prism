namespace Prism.Services.Matching;

/// <summary>
/// Thresholds for Analyzer_Exposure, bound from the "Exposure" section of analyzer_Config.json.
/// </summary>
public sealed class ExposureAnalyzerConfig
{
    /// <summary>Luminance at or above which a pixel counts as blown out.</summary>
    public required float HighLuminance { get; init; }

    /// <summary>Luminance at or below which a pixel counts as crushed.</summary>
    public required float LowLuminance { get; init; }

    /// <summary>Fraction of counted pixels beyond a luminance bound that flips the corresponding flag.</summary>
    public required float FlaggedFraction { get; init; }

    /// <summary>Confidence written on overexposed/underexposed.</summary>
    public required float Confidence { get; init; }
}
