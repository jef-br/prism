using Prism.Config;
using Xunit;

namespace PrismCoreTests.Transform;

/// <summary>
/// Load/validation tests for the transform_Config.json sections consumed by <see cref="ImageTransformer"/>.
/// Confirms the shipped config parses to the documented values, that every leaf value is <c>required</c>
/// (a missing key fails loud rather than silently defaulting), and that each section class's own
/// <c>Validate()</c> rejects out-of-range values. Probe configs are written into the test binary's
/// config/ folder — ConfigLoader's first discovery candidate — following ConfigLoaderTests.
/// </summary>
public class TransformConfigTests : IDisposable {
    private const string ConfigFile = "transform_Config.json";

    private readonly string configDir = Path.Combine(AppContext.BaseDirectory, "config");
    private readonly List<string> writtenFiles = [];

    public void Dispose() {
        foreach (string path in writtenFiles) File.Delete(path);
    }

    private string WriteConfig(string json, [System.Runtime.CompilerServices.CallerMemberName] string testName = "") {
        Directory.CreateDirectory(configDir);
        string fileName = $"probe_transform_{testName}_{Guid.NewGuid():N}.json";
        File.WriteAllText(Path.Combine(configDir, fileName), json);
        writtenFiles.Add(Path.Combine(configDir, fileName));
        return fileName;
    }

    [Fact]
    public void ShippedConfig_SectionsMatchDocumentedValues() {
        CropTransformSettings crop = ConfigLoader.Section<CropTransformSettings>(ConfigFile, "Crop");
        Assert.Equal(0.042, crop.WhiteSpaceMargin);
        Assert.Equal(0.8, crop.CropCoverage);
        Assert.Equal(0.14, crop.CropExtensionOneSided);
        Assert.Equal(0.25, crop.CropExtensionBiDirectional);

        ProblemImageProcessorConfig problem = ConfigLoader.Section<ProblemImageProcessorConfig>(ConfigFile, "ProblemImageProcessor");
        Assert.Equal(570, problem.MinInputPx);
        Assert.Equal(800, problem.MinOutputPx);
        Assert.Equal(1.42, problem.MaxUpscale);

        BgStretchConfig bgStretch = ConfigLoader.Section<BgStretchConfig>(ConfigFile, "BgStretch");
        Assert.Equal(1.25f, bgStretch.Tier1MaxRatio);
        Assert.Equal(1.42f, bgStretch.Tier2MaxRatio);
        Assert.Equal(2.50f, bgStretch.Tier4MinRatio);
        Assert.Equal(16, bgStretch.FeatherPx);

        Assert.Equal(0.14, ConfigLoader.Section<DetailCropperConfig>(ConfigFile, "DetailCropper").AdjacentCropCap);

        LowContrastEnhancementConfig lowContrast = ConfigLoader.Section<LowContrastEnhancementConfig>(ConfigFile, "LowContrastEnhancement");
        Assert.Equal(2.0, lowContrast.ClipLimit);
        Assert.Equal(8, lowContrast.TileSize);

        Assert.Equal(0.75, ConfigLoader.Section<HeadCutterConfig>(ConfigFile, "HeadCutter").FaceHeightCutFactor);
    }

    [Fact]
    public void FromConfig_ShippedConfig_ComposesEverySection() {
        // The startup gate: PrismApiConfiguration.Load() and TransformService both go through this.
        TransformParameters parameters = TransformParameters.FromConfig();

        Assert.Equal(0.042, parameters.Crop.WhiteSpaceMargin);
        Assert.Equal(570, parameters.ProblemImageProcessor.MinInputPx);
        Assert.Equal(1.25f, parameters.BgStretch.Tier1MaxRatio);
        Assert.Equal(0.14, parameters.DetailCropper.AdjacentCropCap);
        Assert.Equal(2.0, parameters.LowContrastEnhancement.ClipLimit);
        Assert.Equal(0.75, parameters.HeadCutter.FaceHeightCutFactor);
    }

    [Fact]
    public void Section_MissingRequiredKey_ThrowsFailLoud() {
        // MinOutputPx is required with no in-code default — deserialization itself must fail loud.
        string fileName = WriteConfig("""{ "ProblemImageProcessor": { "MinInputPx": 570, "MaxUpscale": 1.42 } }""");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => ConfigLoader.Section<ProblemImageProcessorConfig>(fileName, "ProblemImageProcessor"));
        Assert.Contains("MinOutputPx", ex.Message);
    }

    [Fact]
    public void Section_OutOfRangeAdjacentCropCap_ThrowsWithFieldName() {
        string fileName = WriteConfig("""{ "DetailCropper": { "AdjacentCropCap": 1.5 } }""");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => ConfigLoader.Section<DetailCropperConfig>(fileName, "DetailCropper"));
        Assert.Contains("DetailCropper.AdjacentCropCap", ex.Message);
    }

    [Fact]
    public void Section_OutOfRangeWhiteSpaceMargin_ThrowsWithFieldName() {
        // 0.5 collapses Tx_CenterAndStretch's (1 - 2*margin) divisor to zero — the reason for the 0.49 cap.
        string fileName = WriteConfig("""
        {
            "Crop": { "WhiteSpaceMargin": 0.5, "CropCoverage": 0.8, "CropExtensionOneSided": 0.14, "CropExtensionBiDirectional": 0.25 }
        }
        """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => ConfigLoader.Section<CropTransformSettings>(fileName, "Crop"));
        Assert.Contains("Crop.WhiteSpaceMargin", ex.Message);
    }

    [Fact]
    public void Section_MissingFile_ThrowsFailLoud() {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => ConfigLoader.Section<BgStretchConfig>($"transform_Config_missing_{Guid.NewGuid():N}.json", "BgStretch"));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
