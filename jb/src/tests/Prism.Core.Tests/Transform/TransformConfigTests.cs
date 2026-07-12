using Xunit;

namespace PrismCoreTests.Transform;

/// <summary>
/// Load/validation tests for <see cref="TransformConfig"/> — the transform_Config.json root loaded
/// by <see cref="TransformService"/>. Confirms the shipped config parses to the documented values,
/// that every leaf value is <c>required</c> (a missing key fails loud rather than silently
/// defaulting), and that <see cref="TransformConfig"/>'s range checks reject bad values.
/// </summary>
public class TransformConfigTests
{
    private static string ShippedConfigPath()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "jb", "src", "core", "config", "transform_Config.json");
            if (File.Exists(candidate)) return candidate;
        }
        throw new InvalidOperationException("transform_Config.json not found in source tree.");
    }

    [Fact]
    public void Load_ShippedConfig_MatchesDocumentedValues()
    {
        TransformConfig config = TransformConfig.Load(ShippedConfigPath());

        Assert.Equal(570, config.ProblemImageProcessor.MinInputPx);
        Assert.Equal(800, config.ProblemImageProcessor.MinOutputPx);
        Assert.Equal(1.42, config.ProblemImageProcessor.MaxUpscale);

        Assert.Equal(1.25f, config.BgStretch.Tier1MaxRatio);
        Assert.Equal(1.42f, config.BgStretch.Tier2MaxRatio);
        Assert.Equal(2.50f, config.BgStretch.Tier4MinRatio);
        Assert.Equal(16, config.BgStretch.FeatherPx);

        Assert.Equal(0.14, config.DetailCropper.AdjacentCropCap);

        Assert.Equal(2.0, config.LowContrastEnhancement.ClipLimit);
        Assert.Equal(8, config.LowContrastEnhancement.TileSize);

        Assert.Equal(0.75, config.HeadCutter.FaceHeightCutFactor);
    }

    [Fact]
    public void Load_MissingFile_ThrowsFailLoud()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), $"transform_Config_missing_{Guid.NewGuid():N}.json");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => TransformConfig.Load(missingPath));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_JsonMissingRequiredKey_ThrowsFailLoud()
    {
        // BgStretch is missing entirely, and ProblemImageProcessor is missing MinOutputPx —
        // required members with no in-code default, so deserialization itself must fail loud.
        string path = WriteTempConfig("""
        {
            "ProblemImageProcessor": { "MinInputPx": 570, "MaxUpscale": 1.42 },
            "DetailCropper": { "AdjacentCropCap": 0.14 },
            "LowContrastEnhancement": { "ClipLimit": 2.0, "TileSize": 8 },
            "HeadCutter": { "FaceHeightCutFactor": 0.75 }
        }
        """);

        try
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => TransformConfig.Load(path));
            Assert.Contains("not valid JSON", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_OutOfRangeAdjacentCropCap_ThrowsWithFieldName()
    {
        string path = WriteTempConfig("""
        {
            "ProblemImageProcessor": { "MinInputPx": 570, "MinOutputPx": 800, "MaxUpscale": 1.42 },
            "BgStretch": { "Tier1MaxRatio": 1.25, "Tier2MaxRatio": 1.42, "Tier4MinRatio": 2.50, "FeatherPx": 16 },
            "DetailCropper": { "AdjacentCropCap": 1.5 },
            "LowContrastEnhancement": { "ClipLimit": 2.0, "TileSize": 8 },
            "HeadCutter": { "FaceHeightCutFactor": 0.75 }
        }
        """);

        try
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => TransformConfig.Load(path));
            Assert.Contains("DetailCropper.AdjacentCropCap", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempConfig(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), $"transform_Config_test_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
