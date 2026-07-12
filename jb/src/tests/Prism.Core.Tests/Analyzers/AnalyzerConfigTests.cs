using Xunit;

namespace PrismCoreTests.Analyzers;

/// <summary>
/// Load/validation tests for <see cref="AnalyzerConfig"/> — the analyzer_Config.json root loaded
/// by <see cref="FeatureAnalysisService"/>. Confirms the shipped config parses to the documented
/// values, that every leaf value is <c>required</c> (a missing key fails loud rather than silently
/// defaulting), and that <see cref="AnalyzerConfig"/>'s range checks reject bad values.
/// </summary>
public class AnalyzerConfigTests
{
    private static string ShippedConfigPath()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "jb", "src", "core", "config", "analyzer_Config.json");
            if (File.Exists(candidate)) return candidate;
        }
        throw new InvalidOperationException("analyzer_Config.json not found in source tree.");
    }

    [Fact]
    public void Load_ShippedConfig_MatchesDocumentedValues()
    {
        AnalyzerConfig config = AnalyzerConfig.Load(ShippedConfigPath());

        Assert.Equal(0.04f, config.Interior.MinAreaFraction);
        Assert.Equal(8, config.IsIllustration.ColorBinsPerChannel);
        Assert.Equal(0.40f, config.Yolo.ConfidenceThreshold);
        Assert.Equal(0.75f, config.Filename.OrientationConfidence);
        Assert.Equal(0.15f, config.SubjectGeometry.ForegroundColorDistance);
        Assert.Equal(4, config.Colors.BucketCount);
        Assert.Equal(12, config.Colors.Palette.Count);
        Assert.Equal("#cc0000", config.Colors.Palette["red"]);
        Assert.Equal(0.98f, config.Exposure.HighLuminance);
        Assert.Equal(0.10f, config.MultipleProducts.OverlapIou);
    }

    [Fact]
    public void Load_MissingFile_ThrowsFailLoud()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), $"analyzer_Config_missing_{Guid.NewGuid():N}.json");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => AnalyzerConfig.Load(missingPath));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_JsonMissingRequiredKey_ThrowsFailLoud()
    {
        // Yolo section is missing entirely, and Interior is missing TextureDiffMin — required
        // members with no in-code default, so deserialization itself must fail loud.
        string path = WriteTempConfig("""
        {
            "Interior": { "MinAreaFraction": 0.04, "MinEdgeStrength": 0.1176 },
            "IsIllustration": { "MinEdgeDensity": 0.12, "EdgeStrengthThreshold": 0.2353, "WhiteChannelMin": 0.9020, "BackgroundFlatnessMin": 0.80, "BorderSampleDepth": 0.05, "ColorBinsPerChannel": 8, "MaxColorClusters": 8, "MinClusterPopulation": 0.01 },
            "Filename": { "OrientationConfidence": 0.75 },
            "SubjectGeometry": { "ForegroundColorDistance": 0.15, "MinForegroundFraction": 0.005, "FallbackConfidence": 0.60 },
            "Colors": { "BucketCount": 4, "BinsPerChannel": 8, "MinBucketShare": 0.02, "BackgroundDistance": 0.12, "MinSampleFraction": 0.02, "DominantColorsConfidence": 0.70, "ProductColorConfidence": 0.80, "BackgroundColorConfidence": 0.85, "Palette": { "black": "#000000" } },
            "Exposure": { "HighLuminance": 0.98, "LowLuminance": 0.02, "FlaggedFraction": 0.25, "Confidence": 0.70 },
            "MultipleProducts": { "OverlapIou": 0.10, "Confidence": 0.70 }
        }
        """);

        try
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => AnalyzerConfig.Load(path));
            Assert.Contains("not valid JSON", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_OutOfRangeOverlapIou_ThrowsWithFieldName()
    {
        string path = WriteTempConfig("""
        {
            "Interior": { "MinAreaFraction": 0.04, "MinEdgeStrength": 0.1176, "TextureDiffMin": 0.015 },
            "IsIllustration": { "MinEdgeDensity": 0.12, "EdgeStrengthThreshold": 0.2353, "WhiteChannelMin": 0.9020, "BackgroundFlatnessMin": 0.80, "BorderSampleDepth": 0.05, "ColorBinsPerChannel": 8, "MaxColorClusters": 8, "MinClusterPopulation": 0.01 },
            "Yolo": { "ConfidenceThreshold": 0.40, "MaxDetections": 32, "HumanMinConfidence": 0.50, "AbsenceConfidence": 0.60, "HeroPersonMinArea": 0.15 },
            "Filename": { "OrientationConfidence": 0.75 },
            "SubjectGeometry": { "ForegroundColorDistance": 0.15, "MinForegroundFraction": 0.005, "FallbackConfidence": 0.60 },
            "Colors": { "BucketCount": 4, "BinsPerChannel": 8, "MinBucketShare": 0.02, "BackgroundDistance": 0.12, "MinSampleFraction": 0.02, "DominantColorsConfidence": 0.70, "ProductColorConfidence": 0.80, "BackgroundColorConfidence": 0.85, "Palette": { "black": "#000000" } },
            "Exposure": { "HighLuminance": 0.98, "LowLuminance": 0.02, "FlaggedFraction": 0.25, "Confidence": 0.70 },
            "MultipleProducts": { "OverlapIou": 1.5, "Confidence": 0.70 }
        }
        """);

        try
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => AnalyzerConfig.Load(path));
            Assert.Contains("MultipleProducts.OverlapIou", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempConfig(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), $"analyzer_Config_test_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
