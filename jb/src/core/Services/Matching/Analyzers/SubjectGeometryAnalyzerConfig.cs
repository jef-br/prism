using Prism.Config;

namespace Prism.Services.Matching;

/// <summary>
/// Thresholds for Analyzer_SubjectGeometry, bound from the "SubjectGeometry" section of
/// analyzer_Config.json. No defaults — every value must be present in the JSON or deserialization
/// fails loud.
/// </summary>
public sealed class SubjectGeometryAnalyzerConfig : IValidatableConfig
{
    /// <summary>Euclidean RGB distance ([0,1] channels) from the background estimate above which a pixel counts as foreground.</summary>
    public required float ForegroundColorDistance { get; init; }

    /// <summary>Minimum foreground pixel fraction for the fallback box to be trusted.</summary>
    public required float MinForegroundFraction { get; init; }

    /// <summary>Confidence recorded on features measured from the color-distance fallback box (YOLO boxes carry the detection confidence).</summary>
    public required float FallbackConfidence { get; init; }

    public void Validate()
    {
        if (ForegroundColorDistance is <= 0f or >= 1f)
            throw new InvalidOperationException("SubjectGeometry.ForegroundColorDistance must be in (0,1)");
    }
}
