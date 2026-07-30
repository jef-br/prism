using Prism.Config;
using Xunit;

namespace PrismCoreTests.Classify;

/// <summary>
/// Load/validation tests for the ClassifyConfig.json sections composed into
/// <see cref="ClassifyParameters"/>. Confirms the shipped config parses to the documented values,
/// that every leaf value is <c>required</c> (a missing key fails loud rather than silently
/// defaulting), and that each section class's own <c>Validate()</c> rejects out-of-range values.
/// Probe configs are written into the test binary's config/ folder — ConfigLoader's first
/// discovery candidate — following ConfigLoaderTests.
/// </summary>
public class ClassifyConfigTests : IDisposable {
    private readonly string configDir = Path.Combine(AppContext.BaseDirectory, "config");
    private readonly List<string> writtenFiles = [];

    public void Dispose() {
        foreach (string path in writtenFiles) File.Delete(path);
    }

    private string WriteConfig(string json, [System.Runtime.CompilerServices.CallerMemberName] string testName = "") {
        Directory.CreateDirectory(configDir);
        string fileName = $"probe_classify_{testName}_{Guid.NewGuid():N}.json";
        File.WriteAllText(Path.Combine(configDir, fileName), json);
        writtenFiles.Add(Path.Combine(configDir, fileName));
        return fileName;
    }

    [Fact]
    public void FromConfig_ShippedConfig_ComposesEverySection() {
        // The startup gate: PrismApiConfiguration.Load() and FeatureAnalysisService both go through this.
        ClassifyParameters parameters = ClassifyParameters.FromConfig();

        Assert.Equal(0.012f, parameters.ImageFeatureAnalyzer.BackgroundVarianceSolidColorMax);
        Assert.Equal(512, parameters.SubjectEdgeDetector.MaxAnalysisSize);
        Assert.Equal(17, parameters.VisualHasher.HashWidth);
        Assert.Equal(8, parameters.VisualHasher.HashHeight);
    }

    [Fact]
    public void Section_MissingRequiredKey_ThrowsFailLoud() {
        // MaxChannelValueF is required with no in-code default — deserialization itself must fail loud.
        string fileName = WriteConfig("""
        {
            "ImageFeatureAnalyzer": {
                "BackgroundVarianceSolidColorMax": 0.012, "BackgroundVarianceLifestyleMin": 0.040,
                "NearWhiteChannelMin": 0.90, "AlphaOpaqueThreshold": 128,
                "PixelSampleStride": 2, "ChannelCount": 3, "ClippingPathConfidence": 0.90,
                "WhiteBackgroundConfidence": 0.92, "LifestyleBackgroundAlphaConfidence": 0.95,
                "LifestyleBackgroundSolidConfidence": 0.85, "LifestyleBackgroundRealLifeConfidence": 0.72,
                "BackgroundTypeConfidence": 0.82, "EdgeIntersectionConfidence": 0.85,
                "SkinToneAreaConfidence": 0.75
            }
        }
        """);

        PrismConfigurationException ex = Assert.Throws<PrismConfigurationException>(
            () => ConfigLoader.Section<ImageFeatureAnalyzer.Config>(fileName, "ImageFeatureAnalyzer"));
        Assert.Contains("MaxChannelValueF", ex.Message);
    }

    [Fact]
    public void Section_MissingSection_ThrowsNamingIt() {
        string fileName = WriteConfig("""{ "VisualHasher": { "HashWidth": 17, "HashHeight": 8 } }""");

        PrismConfigurationException ex = Assert.Throws<PrismConfigurationException>(
            () => ConfigLoader.Section<SubjectEdgeDetector.Config>(fileName, "SubjectEdgeDetector"));
        Assert.Contains("SubjectEdgeDetector", ex.Message);
    }

    [Fact]
    public void Section_OutOfRangeAlphaOpaqueThreshold_ThrowsWithFieldName() {
        string fileName = WriteConfig("""
        {
            "ImageFeatureAnalyzer": {
                "BackgroundVarianceSolidColorMax": 0.012, "BackgroundVarianceLifestyleMin": 0.040,
                "NearWhiteChannelMin": 0.90, "AlphaOpaqueThreshold": 256, "MaxChannelValueF": 255,
                "PixelSampleStride": 2, "ChannelCount": 3, "ClippingPathConfidence": 0.90,
                "WhiteBackgroundConfidence": 0.92, "LifestyleBackgroundAlphaConfidence": 0.95,
                "LifestyleBackgroundSolidConfidence": 0.85, "LifestyleBackgroundRealLifeConfidence": 0.72,
                "BackgroundTypeConfidence": 0.82, "EdgeIntersectionConfidence": 0.85,
                "SkinToneAreaConfidence": 0.75
            }
        }
        """);

        PrismConfigurationException ex = Assert.Throws<PrismConfigurationException>(
            () => ConfigLoader.Section<ImageFeatureAnalyzer.Config>(fileName, "ImageFeatureAnalyzer"));
        Assert.Contains("ImageFeatureAnalyzer.AlphaOpaqueThreshold", ex.Message);
    }

    [Fact]
    public void Section_OutOfRangeHashWidth_ThrowsWithFieldName() {
        string fileName = WriteConfig("""{ "VisualHasher": { "HashWidth": 1, "HashHeight": 8 } }""");

        PrismConfigurationException ex = Assert.Throws<PrismConfigurationException>(
            () => ConfigLoader.Section<VisualHasher.Config>(fileName, "VisualHasher"));
        Assert.Contains("VisualHasher.HashWidth", ex.Message);
    }

    [Fact]
    public void Section_MissingFile_ThrowsFailLoud() {
        PrismConfigurationException ex = Assert.Throws<PrismConfigurationException>(
            () => ConfigLoader.Section<VisualHasher.Config>($"ClassifyConfig_missing_{Guid.NewGuid():N}.json", "VisualHasher"));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
