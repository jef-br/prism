using Prism.Config;

namespace Prism.Services.Matching;

/// <summary>
/// Thresholds for Analyzer_Exposure, bound from the "Exposure" section of analyzer_Config.json.
/// No defaults — every value must be present in the JSON or deserialization fails loud.
/// </summary>
public sealed class ExposureAnalyzerConfig : IValidatableConfig
{
    /// <summary>Luminance at or above which a pixel counts as blown out.</summary>
    public required float HighLuminance { get; init; }

    /// <summary>Luminance at or below which a pixel counts as crushed.</summary>
    public required float LowLuminance { get; init; }

    /// <summary>Fraction of counted pixels beyond a luminance bound that flips the corresponding flag.</summary>
    public required float FlaggedFraction { get; init; }

    /// <summary>Confidence written on overexposed/underexposed.</summary>
    public required float Confidence { get; init; }

    public void Validate()
    {
        List<string> problems = [];

        if (HighLuminance is <= 0f or > 1f) problems.Add("Exposure.HighLuminance must be in (0,1]");
        if (LowLuminance is < 0f or >= 1f) problems.Add("Exposure.LowLuminance must be in [0,1)");
        if (FlaggedFraction is <= 0f or > 1f) problems.Add("Exposure.FlaggedFraction must be in (0,1]");

        if (problems.Count > 0) throw new PrismConfigurationException(string.Join("; ", problems));
    }
}
