namespace Prism.Services.Matching;

/// <summary>
/// Thresholds for the yolo26s detector and the analyzers built on its detections,
/// bound from the "Yolo" section of analyzer_Config.json.
/// </summary>
public sealed class YoloAnalyzerConfig
{
    /// <summary>Minimum class score for a raw detection to survive.</summary>
    public required float ConfidenceThreshold { get; init; }

    /// <summary>Maximum detections kept per image after NMS.</summary>
    public required int MaxDetections { get; init; }

    /// <summary>Minimum person-class confidence for has-human to flip true.</summary>
    public required float HumanMinConfidence { get; init; }

    /// <summary>Confidence recorded on has-human=false when no person is detected (absence of evidence is weaker than presence).</summary>
    public required float AbsenceConfidence { get; init; }

    /// <summary>Minimum person-box area (fraction of the frame) for the person to count as the hero (hero-is-human=TRUE).</summary>
    public required float HeroPersonMinArea { get; init; }
}
