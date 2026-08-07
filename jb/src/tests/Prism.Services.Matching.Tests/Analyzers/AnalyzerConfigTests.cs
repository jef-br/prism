using System.Text.Json;
using Prism.Config;
using Xunit;

namespace PrismCoreTests.Analyzers;

/// <summary>
/// Load/validation tests for the analyzer_Config.json sections composed into
/// <see cref="AnalyzerParameters"/> by <see cref="FeatureAnalysisService"/>. Confirms the shipped
/// config parses to the documented values, that every leaf value is <c>required</c> (a missing key
/// fails loud rather than silently defaulting), and that each section class's own <c>Validate()</c>
/// rejects out-of-range values. Probe configs are written into the test binary's config/ folder —
/// ConfigLoader's first discovery candidate — following ConfigLoaderTests.
/// </summary>
public class AnalyzerConfigTests : IDisposable {
    private const string ConfigFile = "analyzer_Config.json";

    private readonly string configDir = Path.Combine(AppContext.BaseDirectory, "config");
    private readonly List<string> writtenFiles = [];

    public void Dispose() {
        foreach (string path in writtenFiles) File.Delete(path);
    }

    private string WriteConfig(string json, [System.Runtime.CompilerServices.CallerMemberName] string testName = "") {
        Directory.CreateDirectory(configDir);
        string fileName = $"probe_analyzer_{testName}_{Guid.NewGuid():N}.json";
        File.WriteAllText(Path.Combine(configDir, fileName), json);
        writtenFiles.Add(Path.Combine(configDir, fileName));
        return fileName;
    }

    [Fact]
    public void FromConfig_ShippedConfig_ComposesEverySection() {
        // The startup gate: PrismApiConfiguration.Load() and FeatureAnalysisService both go through this.
        // Every expected value is read from the same JSON the loader reads. Mirroring them as literals
        // here breaks the suite whenever a threshold is retuned, for a reason unrelated to the behaviour
        // under test — it has bitten twice (T-5000's 0.75/0.60, and Yolo.ConfidenceThreshold 0.40/0.33).
        // Reading from the file is not tautological: this pins the mapping, so a section wired to the
        // wrong JSON key still fails.
        AnalyzerParameters parameters = AnalyzerParameters.FromConfig();
        using JsonDocument shipped = JsonDocument.Parse(File.ReadAllText(ConfigLoader.RequireFile(ConfigFile)));
        JsonElement root = shipped.RootElement;

        Assert.Equal(Num(root, "Interior", "MinAreaFraction"), parameters.Interior.MinAreaFraction);
        Assert.Equal(Int(root, "IsIllustration", "ColorBinsPerChannel"), parameters.IsIllustration.ColorBinsPerChannel);
        Assert.Equal(Num(root, "Yolo", "ConfidenceThreshold"), parameters.Yolo.ConfidenceThreshold);
        Assert.Equal(Num(root, "Filename", "OrientationConfidence"), parameters.Filename.OrientationConfidence);
        Assert.Equal(Str(root, "Filename", "OrientationTokens", "front"), parameters.Filename.OrientationTokens["front"]);
        Assert.Equal(Num(root, "SubjectGeometry", "ForegroundColorDistance"), parameters.SubjectGeometry.ForegroundColorDistance);
        Assert.Equal(Int(root, "Colors", "BucketCount"), parameters.Colors.BucketCount);
        Assert.Equal(root.GetProperty("Colors").GetProperty("Palette").EnumerateObject().Count(), parameters.Colors.Palette.Count);
        Assert.Equal(Str(root, "Colors", "Palette", "red"), parameters.Colors.Palette["red"]);
        Assert.Equal(Num(root, "Exposure", "HighLuminance"), parameters.Exposure.HighLuminance);
        Assert.Equal(Num(root, "MultipleProducts", "OverlapIou"), parameters.MultipleProducts.OverlapIou);
    }

    private static float Num(JsonElement root, string section, string key) {
        return root.GetProperty(section).GetProperty(key).GetSingle();
    }

    private static int Int(JsonElement root, string section, string key) {
        return root.GetProperty(section).GetProperty(key).GetInt32();
    }

    private static string Str(JsonElement root, string section, string map, string key) {
        return root.GetProperty(section).GetProperty(map).GetProperty(key).GetString()!;
    }

    [Fact]
    public void Section_MissingRequiredKey_ThrowsFailLoud() {
        // TextureDiffMin is required with no in-code default — deserialization itself must fail loud.
        string fileName = WriteConfig("""{ "Interior": { "MinAreaFraction": 0.04, "MinEdgeStrength": 0.1176 } }""");

        PrismConfigurationException ex = Assert.Throws<PrismConfigurationException>(
            () => ConfigLoader.Section<Analyzer_Interior.Config>(fileName, "Interior"));
        Assert.Contains("TextureDiffMin", ex.Message);
    }

    [Fact]
    public void Section_MissingSection_ThrowsNamingIt() {
        string fileName = WriteConfig("""{ "Interior": { "MinAreaFraction": 0.04, "MinEdgeStrength": 0.1176, "TextureDiffMin": 0.015 } }""");

        PrismConfigurationException ex = Assert.Throws<PrismConfigurationException>(
            () => ConfigLoader.Section<YoloAnalyzerConfig>(fileName, "Yolo"));
        Assert.Contains("Yolo", ex.Message);
    }

    [Fact]
    public void Section_OutOfRangeOverlapIou_ThrowsWithFieldName() {
        string fileName = WriteConfig("""{ "MultipleProducts": { "OverlapIou": 1.5, "Confidence": 0.70 } }""");

        PrismConfigurationException ex = Assert.Throws<PrismConfigurationException>(
            () => ConfigLoader.Section<Analyzer_MultipleProducts.Config>(fileName, "MultipleProducts"));
        Assert.Contains("MultipleProducts.OverlapIou", ex.Message);
    }

    [Fact]
    public void Section_OutOfRangeExposureLowLuminance_ThrowsWithFieldName() {
        string fileName = WriteConfig("""
        {
            "Exposure": { "HighLuminance": 0.98, "LowLuminance": 1.0, "FlaggedFraction": 0.25, "Confidence": 0.70 }
        }
        """);

        PrismConfigurationException ex = Assert.Throws<PrismConfigurationException>(
            () => ConfigLoader.Section<Analyzer_Exposure.Config>(fileName, "Exposure"));
        Assert.Contains("Exposure.LowLuminance", ex.Message);
    }

    [Fact]
    public void Section_EmptyPalette_ThrowsWithFieldName() {
        string fileName = WriteConfig("""
        {
            "Colors": { "BucketCount": 4, "BinsPerChannel": 8, "MinBucketShare": 0.02, "BackgroundDistance": 0.12, "MinSampleFraction": 0.02, "DominantColorsConfidence": 0.70, "ProductColorConfidence": 0.80, "BackgroundColorConfidence": 0.85, "Palette": {} }
        }
        """);

        PrismConfigurationException ex = Assert.Throws<PrismConfigurationException>(
            () => ConfigLoader.Section<ColorAnalyzerConfig>(fileName, "Colors"));
        Assert.Contains("Colors.Palette", ex.Message);
    }

    [Fact]
    public void Section_MissingFile_ThrowsFailLoud() {
        PrismConfigurationException ex = Assert.Throws<PrismConfigurationException>(
            () => ConfigLoader.Section<Analyzer_Interior.Config>($"analyzer_Config_missing_{Guid.NewGuid():N}.json", "Interior"));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
