namespace Prism.Services.Matching;

/// <summary>
/// Thresholds for Analyzer_SubjectGeometry, bound from the "SubjectGeometry" section of
/// analyzer_Config.json.
/// </summary>
public sealed class SubjectGeometryAnalyzerConfig
{
    /// <summary>Euclidean RGB distance ([0,1] channels) from the background estimate above which a pixel counts as foreground.</summary>
    public required float ForegroundColorDistance { get; init; }

    /// <summary>Minimum foreground pixel fraction for the fallback box to be trusted.</summary>
    public required float MinForegroundFraction { get; init; }

    /// <summary>Confidence recorded on features measured from the color-distance fallback box (YOLO boxes carry the detection confidence).</summary>
    public required float FallbackConfidence { get; init; }
}
