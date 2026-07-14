using Prism.Config;

namespace Prism.Services.Matching;

/// <summary>
/// Thresholds for Analyzer_FilenameEvidence, bound from the "Filename" section of
/// analyzer_Config.json. No defaults — every value must be present in the JSON or deserialization
/// fails loud.
/// </summary>
public sealed class FilenameAnalyzerConfig : IValidatableConfig
{
    /// <summary>
    /// Confidence written on hero-orientation when a filename token names the orientation.
    /// A stronger existing measurement (e.g. CLIP) is never overwritten.
    /// </summary>
    public required float OrientationConfidence { get; init; }

    public void Validate()
    {
        if (OrientationConfidence is <= 0f or > 1f)
            throw new PrismConfigurationException("Filename.OrientationConfidence must be in (0,1]");
    }
}
