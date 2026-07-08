namespace Prism.Services.Matching;

/// <summary>
/// Thresholds for Analyzer_SubjectGeometry, bound from the "SubjectGeometry" section of
/// analyzer_Config.json.
/// </summary>
public sealed class SubjectGeometryAnalyzerConfig
{
    /// <summary>Euclidean RGB distance ([0,1] channels) from the background estimate above which a pixel counts as foreground.</summary>
    public float ForegroundColorDistance { get; init; } = 0.15f;

    /// <summary>Minimum foreground pixel fraction for the fallback box to be trusted.</summary>
    public float MinForegroundFraction { get; init; } = 0.005f;

    /// <summary>Confidence recorded on features measured from the color-distance fallback box (YOLO boxes carry the detection confidence).</summary>
    public float FallbackConfidence { get; init; } = 0.60f;
}
