namespace Prism.Core;

/// <summary>
/// Thresholds for the YOLOv8n detector and the analyzers built on its detections,
/// bound from the "Yolo" section of analyzer_Config.json.
/// </summary>
public sealed class YoloAnalyzerConfig
{
    /// <summary>Minimum class score for a raw detection to survive.</summary>
    public float ConfidenceThreshold { get; init; } = 0.40f;

    /// <summary>IoU above which two same-class boxes are considered duplicates during NMS.</summary>
    public float NmsIouThreshold { get; init; } = 0.60f;

    /// <summary>Maximum detections kept per image after NMS.</summary>
    public int MaxDetections { get; init; } = 32;

    /// <summary>Minimum person-class confidence for has-human to flip true.</summary>
    public float HumanMinConfidence { get; init; } = 0.50f;

    /// <summary>Confidence recorded on has-human=false when no person is detected (absence of evidence is weaker than presence).</summary>
    public float AbsenceConfidence { get; init; } = 0.60f;

    /// <summary>Minimum person-box area (fraction of the frame) for the person to count as the hero (hero-is-human=TRUE).</summary>
    public float HeroPersonMinArea { get; init; } = 0.15f;
}
