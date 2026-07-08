using System.Text.Json;

namespace Prism.Core;

/// <summary>
/// Root configuration for the analyzer chain, loaded from analyzer_Config.json in the core config
/// directory. One section per analyzer; sections deserialize into their own typed config classes.
/// Missing file or malformed JSON fails loud — never silently.
/// </summary>
public sealed class AnalyzerConfig
{
    public InteriorAnalyzerConfig Interior { get; init; } = new();
    public IllustrationAnalyzerConfig IsIllustration { get; init; } = new();
    public YoloAnalyzerConfig Yolo { get; init; } = new();
    public FilenameAnalyzerConfig Filename { get; init; } = new();
    public SubjectGeometryAnalyzerConfig SubjectGeometry { get; init; } = new();
    public ColorAnalyzerConfig Colors { get; init; } = new();
    public ExposureAnalyzerConfig Exposure { get; init; } = new();
    public MultipleProductsAnalyzerConfig MultipleProducts { get; init; } = new();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>Loads and validates analyzer_Config.json from the given path.</summary>
    public static AnalyzerConfig Load(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new InvalidOperationException($"analyzer_Config.json not found at: {jsonPath}");

        AnalyzerConfig config;
        try
        {
            config = JsonSerializer.Deserialize<AnalyzerConfig>(File.ReadAllText(jsonPath), SerializerOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize analyzer_Config.json at: {jsonPath}");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"analyzer_Config.json at {jsonPath} is not valid JSON: {ex.Message}", ex);
        }

        config.Validate(jsonPath);
        return config;
    }

    private void Validate(string jsonPath)
    {
        List<string> problems = [];

        if (Interior.MinAreaFraction is <= 0f or >= 1f) problems.Add("Interior.MinAreaFraction must be in (0,1)");
        if (Interior.MinEdgeStrength <= 0f) problems.Add("Interior.MinEdgeStrength must be > 0");
        if (Interior.TextureDiffMin <= 0f) problems.Add("Interior.TextureDiffMin must be > 0");
        if (IsIllustration.MinEdgeDensity is <= 0f or >= 1f) problems.Add("IsIllustration.MinEdgeDensity must be in (0,1)");
        if (IsIllustration.EdgeStrengthThreshold <= 0f) problems.Add("IsIllustration.EdgeStrengthThreshold must be > 0");
        if (IsIllustration.BackgroundFlatnessMin is <= 0f or > 1f) problems.Add("IsIllustration.BackgroundFlatnessMin must be in (0,1]");
        if (IsIllustration.BorderSampleDepth is <= 0f or >= 0.5f) problems.Add("IsIllustration.BorderSampleDepth must be in (0,0.5)");
        if (IsIllustration.ColorBinsPerChannel < 2) problems.Add("IsIllustration.ColorBinsPerChannel must be >= 2");
        if (IsIllustration.MaxColorClusters < 1) problems.Add("IsIllustration.MaxColorClusters must be >= 1");
        if (Yolo.ConfidenceThreshold is <= 0f or >= 1f) problems.Add("Yolo.ConfidenceThreshold must be in (0,1)");
        if (Yolo.NmsIouThreshold is <= 0f or >= 1f) problems.Add("Yolo.NmsIouThreshold must be in (0,1)");
        if (Yolo.MaxDetections < 1) problems.Add("Yolo.MaxDetections must be >= 1");
        if (Yolo.HumanMinConfidence is <= 0f or >= 1f) problems.Add("Yolo.HumanMinConfidence must be in (0,1)");
        if (Yolo.AbsenceConfidence is <= 0f or > 1f) problems.Add("Yolo.AbsenceConfidence must be in (0,1]");
        if (Yolo.HeroPersonMinArea is <= 0f or >= 1f) problems.Add("Yolo.HeroPersonMinArea must be in (0,1)");
        if (Filename.OrientationConfidence is <= 0f or > 1f) problems.Add("Filename.OrientationConfidence must be in (0,1]");
        if (SubjectGeometry.ForegroundColorDistance is <= 0f or >= 1f) problems.Add("SubjectGeometry.ForegroundColorDistance must be in (0,1)");
        if (Colors.BucketCount < 1) problems.Add("Colors.BucketCount must be >= 1");
        if (Colors.BinsPerChannel < 2) problems.Add("Colors.BinsPerChannel must be >= 2");
        if (Colors.Palette.Count == 0) problems.Add("Colors.Palette must define at least one named color");
        if (Exposure.HighLuminance is <= 0f or > 1f) problems.Add("Exposure.HighLuminance must be in (0,1]");
        if (Exposure.LowLuminance is < 0f or >= 1f) problems.Add("Exposure.LowLuminance must be in [0,1)");
        if (Exposure.FlaggedFraction is <= 0f or > 1f) problems.Add("Exposure.FlaggedFraction must be in (0,1]");
        if (MultipleProducts.OverlapIou is <= 0f or >= 1f) problems.Add("MultipleProducts.OverlapIou must be in (0,1)");

        if (problems.Count > 0)
            throw new InvalidOperationException(
                $"analyzer_Config.json at {jsonPath} is invalid: {string.Join("; ", problems)}");
    }
}
