using Prism.Config;

namespace Prism.Services.Matching;

/// <summary>
/// Thresholds for the yolo26s detector and the analyzers built on its detections,
/// bound from the "Yolo" section of analyzer_Config.json. No defaults — every value must be present
/// in the JSON or deserialization fails loud.
/// </summary>
public sealed class YoloAnalyzerConfig : IValidatableConfig
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

    public void Validate()
    {
        List<string> problems = [];

        if (ConfidenceThreshold is <= 0f or >= 1f) problems.Add("Yolo.ConfidenceThreshold must be in (0,1)");
        if (MaxDetections < 1) problems.Add("Yolo.MaxDetections must be >= 1");
        if (HumanMinConfidence is <= 0f or >= 1f) problems.Add("Yolo.HumanMinConfidence must be in (0,1)");
        if (AbsenceConfidence is <= 0f or > 1f) problems.Add("Yolo.AbsenceConfidence must be in (0,1]");
        if (HeroPersonMinArea is <= 0f or >= 1f) problems.Add("Yolo.HeroPersonMinArea must be in (0,1)");

        if (problems.Count > 0) throw new PrismConfigurationException(string.Join("; ", problems));
    }
}
