using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace PrismCoreTests.Services;

/// <summary>
/// The per-model AI toggles (Models.&lt;section&gt;.UseIt). Two contracts are pinned here: a missing UseIt
/// key fails loud at load time (no shadow default picks a side for you), and a model whose toggle is off
/// is not asset-validated — otherwise disabling a model precisely because its file is missing or bad
/// would still fail startup.
/// <para>
/// Each test runs against a private copy of the whole config directory so the shipped
/// <c>Prism_Config.json</c> is never mutated and the cross-file ImageNGP validator still finds its
/// companion files.
/// </para>
/// </summary>
public class PrismConfigurationTests : IDisposable {
    private const string MissingAssetPath = "Services/Nowhere/ONNX/no-such-model.onnx";

    private readonly string tempConfigDir = Path.Combine(Path.GetTempPath(), $"prism-config-{Guid.NewGuid():N}");
    private readonly string tempConfigPath;

    public PrismConfigurationTests() => this.tempConfigPath = TempConfigDirectory.Create(this.tempConfigDir);

    public void Dispose() {
        try { Directory.Delete(this.tempConfigDir, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    //  Shipped defaults

    [Fact]
    public void ShippedConfig_EnablesTheThreeBuiltModels_AndLeavesGenerationOff() {
        PrismConfiguration config = PrismConfiguration.LoadPrismConfig(
            ConfigLoader.RequireFile(PrismConfiguration.FileName));

        Assert.True(config.AiClassificationEnabled);
        Assert.True(config.AiDetectionEnabled);
        Assert.True(config.AiUpscalingEnabled);
        // No generation backend exists; true would silently skip the Gated-placeholder record path.
        Assert.False(config.AiGenerationEnabled);
    }

    //  Missing key fails loud

    [Theory]
    [InlineData("classification")]
    [InlineData("Detection")]
    [InlineData("Upscaling")]
    [InlineData("Generation")]
    public void MissingUseItKey_ThrowsAtLoad(string section) {
        string path = this.WriteConfig(root => ((JsonObject)root["Models"]![section]!).Remove("UseIt"));

        PrismConfigurationException thrown = Assert.Throws<PrismConfigurationException>(
            () => PrismConfiguration.LoadPrismConfig(path));
        Assert.Contains($"Models.{section}.UseIt", thrown.Message);
    }

    [Fact]
    public void NonBooleanUseIt_ThrowsAtLoad() {
        string path = this.WriteConfig(root => root["Models"]!["Detection"]!["UseIt"] = "yes");

        Assert.Throws<PrismConfigurationException>(() => PrismConfiguration.LoadPrismConfig(path));
    }

    //  Asset validation follows the toggle

    [Fact]
    public void DetectionEnabled_WithMissingModelAsset_ThrowsAtLoad() {
        string path = this.WriteConfig(root => {
            root["Models"]!["Detection"]!["Path"] = MissingAssetPath;
            root["Models"]!["Detection"]!["UseIt"] = true;
        });

        PrismConfigurationException thrown = Assert.Throws<PrismConfigurationException>(
            () => PrismConfiguration.LoadPrismConfig(path));
        Assert.Contains("YOLO26", thrown.Message);
    }

    [Fact]
    public void DetectionDisabled_WithMissingModelAsset_Loads() {
        string path = this.WriteConfig(root => {
            root["Models"]!["Detection"]!["Path"] = MissingAssetPath;
            root["Models"]!["Detection"]!["UseIt"] = false;
        });

        PrismConfiguration config = PrismConfiguration.LoadPrismConfig(path);

        Assert.False(config.AiDetectionEnabled);
        Assert.Equal(MissingAssetPath, config.YoloModelPath);
    }

    [Fact]
    public void UpscalingEnabled_WithMissingModelAsset_ThrowsAtLoad() {
        string path = this.WriteConfig(root => {
            root["Models"]!["Upscaling"]!["Path"] = MissingAssetPath;
            root["Models"]!["Upscaling"]!["UseIt"] = true;
        });

        PrismConfigurationException thrown = Assert.Throws<PrismConfigurationException>(
            () => PrismConfiguration.LoadPrismConfig(path));
        Assert.Contains("Real-ESRGAN", thrown.Message);
    }

    [Fact]
    public void UpscalingDisabled_WithMissingModelAsset_Loads() {
        string path = this.WriteConfig(root => {
            root["Models"]!["Upscaling"]!["Path"] = MissingAssetPath;
            root["Models"]!["Upscaling"]!["UseIt"] = false;
        });

        PrismConfiguration config = PrismConfiguration.LoadPrismConfig(path);

        Assert.False(config.AiUpscalingEnabled);
    }

    //  Toggles are independent

    [Fact]
    public void EachToggleIsReadFromItsOwnSection() {
        string path = this.WriteConfig(root => {
            root["Models"]!["classification"]!["UseIt"] = false;
            root["Models"]!["Detection"]!["UseIt"] = true;
            root["Models"]!["Upscaling"]!["UseIt"] = false;
            root["Models"]!["Generation"]!["UseIt"] = true;
        });

        PrismConfiguration config = PrismConfiguration.LoadPrismConfig(path);

        Assert.False(config.AiClassificationEnabled);
        Assert.True(config.AiDetectionEnabled);
        Assert.False(config.AiUpscalingEnabled);
        Assert.True(config.AiGenerationEnabled);
    }

    //  Helpers

    private string WriteConfig(Action<JsonObject> mutate) {
        JsonObject root = (JsonObject)JsonNode.Parse(File.ReadAllText(this.tempConfigPath))!;
        mutate(root);
        File.WriteAllText(this.tempConfigPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return this.tempConfigPath;
    }
}
