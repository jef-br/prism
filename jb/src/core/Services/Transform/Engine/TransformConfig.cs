using System.Text.Json;

namespace Prism.Services.Transform;

/// <summary>
/// Root configuration for the Transform engine, loaded from transform_Config.json in the core
/// config directory. One section per Tx_ class; every section and every leaf value is <c>required</c>
/// — there are no in-code defaults, so a missing or misspelled key fails loud at deserialization
/// instead of silently falling back to a stale constant.
/// </summary>
public sealed class TransformConfig
{
    public required ProblemImageProcessorConfig ProblemImageProcessor { get; init; }
    public required BgStretchConfig BgStretch { get; init; }
    public required DetailCropperConfig DetailCropper { get; init; }
    public required LowContrastEnhancementConfig LowContrastEnhancement { get; init; }
    public required HeadCutterConfig HeadCutter { get; init; }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>Loads and validates transform_Config.json from the given path.</summary>
    public static TransformConfig Load(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new InvalidOperationException($"transform_Config.json not found at: {jsonPath}");

        TransformConfig config;
        try
        {
            config = JsonSerializer.Deserialize<TransformConfig>(File.ReadAllText(jsonPath), SerializerOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize transform_Config.json at: {jsonPath}");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"transform_Config.json at {jsonPath} is not valid JSON: {ex.Message}", ex);
        }

        config.Validate(jsonPath);
        return config;
    }

    private void Validate(string jsonPath)
    {
        List<string> problems = [];

        if (ProblemImageProcessor.MinInputPx <= 0) problems.Add("ProblemImageProcessor.MinInputPx must be > 0");
        if (ProblemImageProcessor.MinOutputPx <= 0) problems.Add("ProblemImageProcessor.MinOutputPx must be > 0");
        if (ProblemImageProcessor.MinOutputPx < ProblemImageProcessor.MinInputPx) problems.Add("ProblemImageProcessor.MinOutputPx must be >= MinInputPx");
        if (ProblemImageProcessor.MaxUpscale <= 1.0) problems.Add("ProblemImageProcessor.MaxUpscale must be > 1.0");

        if (BgStretch.Tier1MaxRatio <= 1f) problems.Add("BgStretch.Tier1MaxRatio must be > 1");
        if (BgStretch.Tier2MaxRatio <= BgStretch.Tier1MaxRatio) problems.Add("BgStretch.Tier2MaxRatio must be > Tier1MaxRatio");
        if (BgStretch.Tier4MinRatio <= BgStretch.Tier2MaxRatio) problems.Add("BgStretch.Tier4MinRatio must be > Tier2MaxRatio");
        if (BgStretch.FeatherPx < 0) problems.Add("BgStretch.FeatherPx must be >= 0");

        if (DetailCropper.AdjacentCropCap is <= 0.0 or >= 1.0) problems.Add("DetailCropper.AdjacentCropCap must be in (0,1)");

        if (LowContrastEnhancement.ClipLimit <= 0.0) problems.Add("LowContrastEnhancement.ClipLimit must be > 0");
        if (LowContrastEnhancement.TileSize < 1) problems.Add("LowContrastEnhancement.TileSize must be >= 1");

        if (HeadCutter.FaceHeightCutFactor is <= 0.0 or >= 1.0) problems.Add("HeadCutter.FaceHeightCutFactor must be in (0,1)");

        if (problems.Count > 0)
            throw new InvalidOperationException(
                $"transform_Config.json at {jsonPath} is invalid: {string.Join("; ", problems)}");
    }
}
