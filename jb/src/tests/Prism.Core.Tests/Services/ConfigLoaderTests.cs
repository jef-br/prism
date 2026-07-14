using Prism.Config;
using Xunit;

namespace PrismCoreTests.Services;

/// <summary>
/// Unit tests for the generic section-aware ConfigLoader: discovery failure messages, section
/// resolution, required-member enforcement, comment/case tolerance, timestamp-keyed caching, and
/// IValidatableConfig invocation. Writes uniquely-named probe files directly into the test binary's
/// config/ folder (already the first ConfigLoader discovery candidate) instead of mutating the
/// process-wide current directory, so tests stay safe under xUnit's default parallel-by-class runs.
/// </summary>
public class ConfigLoaderTests : IDisposable {
    private readonly string configDir = Path.Combine(AppContext.BaseDirectory, "config");
    private readonly List<string> writtenFiles = [];

    public void Dispose() {
        foreach (string path in writtenFiles) File.Delete(path);
    }

    private string WriteConfig(string json, [System.Runtime.CompilerServices.CallerMemberName] string testName = "") {
        Directory.CreateDirectory(configDir);
        string fileName = $"probe_{testName}_{Guid.NewGuid():N}.json";
        string path = Path.Combine(configDir, fileName);
        File.WriteAllText(path, json);
        writtenFiles.Add(path);
        return fileName;
    }

    private sealed class ProbeSection {
        public required int Alpha { get; init; }
        public required float Beta { get; init; }
    }

    // Mirrors a real section class: required props, no initializers, and a Validate() that fails loud
    // with PrismConfigurationException — the single config exception type every production
    // section class throws (T-4560).
    private sealed class ValidatedSection : IValidatableConfig {
        public required int Alpha { get; init; }
        public void Validate() {
            if (Alpha <= 0) throw new PrismConfigurationException("Alpha must be > 0");
        }
    }

    [Fact]
    public void RequireFile_Missing_ThrowsListingSearchedPaths() {
        PrismConfigurationException ex = Assert.Throws<PrismConfigurationException>(
            () => ConfigLoader.RequireFile("no_such_file_zz.json"));
        Assert.Contains("no_such_file_zz.json", ex.Message);
        Assert.Contains(Path.Combine(AppContext.BaseDirectory, "config"), ex.Message);
    }

    [Fact]
    public void Section_MissingSection_ThrowsNamingExistingSections() {
        string fileName = WriteConfig("""{ "Alpha": {}, "Bravo": {} }""");
        PrismConfigurationException ex = Assert.Throws<PrismConfigurationException>(
            () => ConfigLoader.Section<ProbeSection>(fileName, "Charlie"));
        Assert.Contains("Charlie", ex.Message);
        Assert.Contains("Alpha", ex.Message);
        Assert.Contains("Bravo", ex.Message);
    }

    [Fact]
    public void Section_MisspelledKey_Throws() {
        string fileName = WriteConfig("""{ "Probe": { "Alfa": 1, "Beta": 2.0 } }""");
        PrismConfigurationException ex = Assert.Throws<PrismConfigurationException>(
            () => ConfigLoader.Section<ProbeSection>(fileName, "Probe"));
        Assert.Contains("Probe", ex.Message);
    }

    [Fact]
    public void Section_CommentsAndCaseInsensitivity_Accepted() {
        string fileName = WriteConfig("""
            {
                // section name and keys deliberately differ in case
                "PROBE": { "alpha": 7, "BETA": 1.5 }
            }
            """);
        ProbeSection cfg = ConfigLoader.Section<ProbeSection>(fileName, "Probe");
        Assert.Equal(7, cfg.Alpha);
        Assert.Equal(1.5f, cfg.Beta);
    }

    [Fact]
    public void Section_UnchangedFile_ReturnsCachedInstance() {
        string fileName = WriteConfig("""{ "Probe": { "Alpha": 1, "Beta": 2.0 } }""");
        ProbeSection first = ConfigLoader.Section<ProbeSection>(fileName, "Probe");
        ProbeSection second = ConfigLoader.Section<ProbeSection>(fileName, "Probe");
        Assert.Same(first, second);
    }

    [Fact]
    public void Section_TouchedTimestamp_Reparses() {
        string fileName = WriteConfig("""{ "Probe": { "Alpha": 1, "Beta": 2.0 } }""");
        string path = Path.Combine(configDir, fileName);
        ProbeSection first = ConfigLoader.Section<ProbeSection>(fileName, "Probe");
        File.WriteAllText(path, """{ "Probe": { "Alpha": 99, "Beta": 2.0 } }""");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));
        ProbeSection second = ConfigLoader.Section<ProbeSection>(fileName, "Probe");
        Assert.NotSame(first, second);
        Assert.Equal(99, second.Alpha);
    }

    [Fact]
    public void Section_ValidatableConfig_ValidateFailurePropagates() {
        string fileName = WriteConfig("""{ "Probe": { "Alpha": -1 } }""");
        PrismConfigurationException ex = Assert.Throws<PrismConfigurationException>(
            () => ConfigLoader.Section<ValidatedSection>(fileName, "Probe"));
        Assert.Contains("Alpha must be > 0", ex.Message);
    }

    [Fact]
    public void Section_RealTransformConfig_FoundViaSourceTreeWalkUp() {
        // Discovery test against the real repo config: proves the walk-up-from-binary candidate
        // that every production call site outside a deployed build depends on.
        string path = ConfigLoader.RequireFile("transform_Config.json");
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void ModelAssetLocator_MissingAsset_ReturnsNull() {
        Assert.Null(ModelAssetLocator.Find("Services/NoSuchDir/ONNX/nothing.onnx"));
    }

    [Fact]
    public void ModelAssetLocator_RealAsset_FoundViaSourceTreeWalkUp() {
        // Mirrors YoloDetectorTests' resolution of the same shipped model — proves the
        // source-tree-walk-up branch (the one every dev-machine call site actually exercises).
        string? path = ModelAssetLocator.Find("Services/Matching/Analyzers/ONNX/yolo26s.onnx");
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
    }
}
