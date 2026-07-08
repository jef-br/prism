namespace Prism.Core;

/// <summary>
/// Thresholds for Analyzer_FilenameEvidence, bound from the "Filename" section of
/// analyzer_Config.json.
/// </summary>
public sealed class FilenameAnalyzerConfig
{
    /// <summary>
    /// Confidence written on hero-orientation when a filename token names the orientation.
    /// A stronger existing measurement (e.g. CLIP) is never overwritten.
    /// </summary>
    public float OrientationConfidence { get; init; } = 0.75f;
}
