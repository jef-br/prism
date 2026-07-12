using Prism.Config;
using Xunit;

namespace PrismCoreTests.Services;

/// <summary>
/// Unit tests for the generic section-aware ConfigLoader: discovery failure messages, section
/// resolution, required-member enforcement, comment/case tolerance, timestamp-keyed caching, and
/// IValidatableConfig invocation.
/// </summary>
public class ConfigLoaderTests : IDisposable {
    private readonly string dir;

    public ConfigLoaderTests() {
        dir = Path.Combine(Path.GetTempPath(), "prism-configloader-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "config"));
    }

    public void Dispose() {
        Directory.Delete(dir, recursive: true);
    }

    private string WriteConfig(string fileName, string json) {
        string path = Path.Combine(dir, "config", fileName);
        File.WriteAllText(path, json);
        return path;
    }

    private sealed class ProbeSection {
        public required int Alpha { get; init; }
        public required float Beta { get; init; }
    }

    private sealed class ValidatedSection : IValidatableConfig {
        public required int Alpha { get; init; }
        public void Validate() {
            if (Alpha <= 0) throw new InvalidOperationException("Alpha must be > 0");
        }
    }

    [Fact]
    public void RequireFile_Missing_ThrowsListingSearchedPaths() {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => ConfigLoader.RequireFile("no_such_file_zz.json"));
        Assert.Contains("no_such_file_zz.json", ex.Message);
        Assert.Contains(Path.Combine(AppContext.BaseDirectory, "config"), ex.Message);
    }

    [Fact]
    public void Section_MissingSection_ThrowsNamingExistingSections() {
        string path = WriteConfig("probe_missing_section.json", """{ "Alpha": {}, "Bravo": {} }""");
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => LoadSectionFrom<ProbeSection>(path, "Charlie"));
        Assert.Contains("Charlie", ex.Message);
        Assert.Contains("Alpha", ex.Message);
        Assert.Contains("Bravo", ex.Message);
    }

    [Fact]
    public void Section_MisspelledKey_Throws() {
        string path = WriteConfig("probe_misspelled.json", """{ "Probe": { "Alfa": 1, "Beta": 2.0 } }""");
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => LoadSectionFrom<ProbeSection>(path, "Probe"));
        Assert.Contains("Probe", ex.Message);
    }

    [Fact]
    public void Section_CommentsAndCaseInsensitivity_Accepted() {
        string path = WriteConfig("probe_tolerant.json", """
            {
                // section name and keys deliberately differ in case
                "PROBE": { "alpha": 7, "BETA": 1.5 }
            }
            """);
        ProbeSection cfg = LoadSectionFrom<ProbeSection>(path, "Probe");
        Assert.Equal(7, cfg.Alpha);
        Assert.Equal(1.5f, cfg.Beta);
    }

    [Fact]
    public void Section_UnchangedFile_ReturnsCachedInstance() {
        string path = WriteConfig("probe_cache.json", """{ "Probe": { "Alpha": 1, "Beta": 2.0 } }""");
        ProbeSection first = LoadSectionFrom<ProbeSection>(path, "Probe");
        ProbeSection second = LoadSectionFrom<ProbeSection>(path, "Probe");
        Assert.Same(first, second);
    }

    [Fact]
    public void Section_TouchedTimestamp_Reparses() {
        string path = WriteConfig("probe_touch.json", """{ "Probe": { "Alpha": 1, "Beta": 2.0 } }""");
        ProbeSection first = LoadSectionFrom<ProbeSection>(path, "Probe");
        File.WriteAllText(path, """{ "Probe": { "Alpha": 99, "Beta": 2.0 } }""");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));
        ProbeSection second = LoadSectionFrom<ProbeSection>(path, "Probe");
        Assert.NotSame(first, second);
        Assert.Equal(99, second.Alpha);
    }

    [Fact]
    public void Section_ValidatableConfig_ValidateFailurePropagates() {
        string path = WriteConfig("probe_validate.json", """{ "Probe": { "Alpha": -1 } }""");
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => LoadSectionFrom<ValidatedSection>(path, "Probe"));
        Assert.Contains("Alpha must be > 0", ex.Message);
    }

    [Fact]
    public void Section_RealTransformConfig_FoundViaSourceTreeWalkUp() {
        // Discovery test against the real repo config: no cwd manipulation, relies on the
        // walk-up-from-binary candidate that test runs depend on.
        string path = ConfigLoader.RequireFile("transform_Config.json");
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void ModelAssetLocator_MissingAsset_ReturnsNull() {
        Assert.Null(ModelAssetLocator.Find("Services/NoSuchDir/ONNX/nothing.onnx"));
    }

    // ConfigLoader discovery searches fixed locations, so unit tests route through a cwd change
    // into the per-test temp dir (its config/ subfolder matches the cwd/config candidate).
    private T LoadSectionFrom<T>(string configPath, string section) where T : class {
        string original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(dir);
        try {
            return ConfigLoader.Section<T>(Path.GetFileName(configPath), section);
        } finally {
            Directory.SetCurrentDirectory(original);
        }
    }
}
