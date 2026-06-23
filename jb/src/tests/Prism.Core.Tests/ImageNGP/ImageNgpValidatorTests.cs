using Xunit;

namespace PrismCoreTests.ImageNGP;

/// <summary>
/// Tests for <see cref="ImageNgpValidator"/> and <see cref="ImageNgpVocabulary"/>: the real shipped
/// config validates clean, and seeded typos in each rule/mapping file fail loud at startup.
/// </summary>
public class ImageNgpValidatorTests
{
    private static readonly string CoreConfigDirectory = ResolveCoreConfigDirectory();
    private static readonly string VocabularyPath = Path.Combine(CoreConfigDirectory, "ImageNGP.json");

    // ─── Real config ───────────────────────────────────────────────────────────

    [Fact]
    public void Validate_RealShippedConfig_DoesNotThrow()
    {
        // The real ImageRoles.json, DetOrderRules.json, and ClipPrompts.json must all reference only
        // ids/values defined in ImageNGP.json. This proves the vocabulary is a correct superset.
        ImageNgpValidator.Validate(CoreConfigDirectory);
    }

    // ─── Vocabulary contract ─────────────────────────────────────────────────────

    [Fact]
    public void Vocabulary_KnownAndUnknownFeatures()
    {
        var vocab = ImageNgpVocabulary.Load(VocabularyPath);
        Assert.True(vocab.HasFeature("hero-orientation"));
        Assert.False(vocab.HasFeature("hero-orientaton")); // typo
    }

    [Fact]
    public void Vocabulary_IsAllowedValue_EnumNumericAndUnknown()
    {
        var vocab = ImageNgpVocabulary.Load(VocabularyPath);
        Assert.True(vocab.IsAllowedValue("hero-orientation", "FRONT"));
        Assert.False(vocab.IsAllowedValue("hero-orientation", "SIDEWAYS"));
        Assert.True(vocab.IsAllowedValue("intersection-count", "0"));    // integer parse
        Assert.False(vocab.IsAllowedValue("intersection-count", "two")); // not a number
        Assert.True(vocab.IsAllowedValue("hero-orientation", "UNKNOWN")); // always accepted
    }

    [Fact]
    public void Vocabulary_HasPhenotype()
    {
        var vocab = ImageNgpVocabulary.Load(VocabularyPath);
        Assert.True(vocab.HasPhenotype("front-packshot"));
        Assert.False(vocab.HasPhenotype("front-packshott"));
    }

    // ─── Seeded failures ─────────────────────────────────────────────────────────

    [Fact]
    public void Validate_UnknownFeatureInImageRoles_Throws_NamingFeature()
    {
        using var fixture = new ConfigFixture();
        fixture.WriteImageRoles("""
            { "phenotypes": [ { "id": "front-packshot", "required": [ { "feature": "made-up-feature", "equals": "X" } ] } ] }
            """);

        var ex = Assert.Throws<PrismConfigurationException>(() => ImageNgpValidator.Validate(fixture.Root));
        Assert.Contains("made-up-feature", ex.Message);
    }

    [Fact]
    public void Validate_BadEnumValueInImageRoles_Throws()
    {
        using var fixture = new ConfigFixture();
        fixture.WriteImageRoles("""
            { "phenotypes": [ { "id": "front-packshot", "required": [ { "feature": "hero-orientation", "equals": "SIDEWAYS" } ] } ] }
            """);

        var ex = Assert.Throws<PrismConfigurationException>(() => ImageNgpValidator.Validate(fixture.Root));
        Assert.Contains("SIDEWAYS", ex.Message);
    }

    [Fact]
    public void Validate_UnknownPhenotypeInDetOrder_Throws()
    {
        using var fixture = new ConfigFixture();
        fixture.WriteDetOrder("""
            { "productTypes": { "default": { "det0": { "keyword": "front", "phenotypes": ["not-a-phenotype"] } } } }
            """);

        var ex = Assert.Throws<PrismConfigurationException>(() => ImageNgpValidator.Validate(fixture.Root));
        Assert.Contains("not-a-phenotype", ex.Message);
    }

    [Fact]
    public void Validate_UnknownPhenotypeInImageRoles_Throws()
    {
        using var fixture = new ConfigFixture();
        fixture.WriteImageRoles("""
            { "phenotypes": [ { "id": "ghost-front-typo", "required": [ { "feature": "hero-is-human", "equals": "FALSE" } ] } ] }
            """);

        var ex = Assert.Throws<PrismConfigurationException>(() => ImageNgpValidator.Validate(fixture.Root));
        Assert.Contains("ghost-front-typo", ex.Message);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// A temporary core-config directory seeded with a minimal valid vocabulary and rule/mapping
    /// files. Tests overwrite a single file to seed a specific failure.
    /// </summary>
    private sealed class ConfigFixture : IDisposable
    {
        public string Root { get; }

        public ConfigFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "prism-ngp-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);

            Write("ImageNGP.json", """
                { "features": [
                    { "id": "hero-is-human",      "datatype": "enum",    "values": ["TRUE","FALSE","UNKNOWN"] },
                    { "id": "hero-orientation",   "datatype": "enum",    "values": ["FRONT","BACK","SIDEON","DIAGONAL","TOP","BOTTOM","UNKNOWN"] },
                    { "id": "intersection-count", "datatype": "integer" }
                  ],
                  "phenotypes": [ "front-packshot", "back-packshot" ] }
                """);
            WriteImageRoles("""
                { "phenotypes": [ { "id": "front-packshot", "required": [ { "feature": "hero-orientation", "equals": "FRONT" } ] } ] }
                """);
            WriteDetOrder("""
                { "productTypes": { "default": { "det0": { "keyword": "front", "phenotypes": ["front-packshot"] } } } }
                """);
            Write("ClipPrompts.json", """
                { "prompts": [ { "prompt": "a front view", "feature": "hero-orientation", "value": "FRONT" } ] }
                """);
        }

        public void WriteImageRoles(string json) => Write("ImageRoles.json", json);
        public void WriteDetOrder(string json) => Write("DetOrderRules.json", json);

        private void Write(string file, string json)
            => File.WriteAllText(Path.Combine(Root, file), json, System.Text.Encoding.UTF8);

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); } catch { /* best effort */ }
        }
    }

    private static string ResolveCoreConfigDirectory()
    {
        var assemblyDir = new FileInfo(typeof(ImageNgpValidatorTests).Assembly.Location).DirectoryName
            ?? throw new InvalidOperationException("Cannot determine assembly directory");

        var current = new DirectoryInfo(assemblyDir);
        while (current.Parent != null)
        {
            var candidate = Path.Combine(current.FullName, "jb", "src", "core", "config");
            if (File.Exists(Path.Combine(candidate, "ImageNGP.json")))
                return candidate;
            current = current.Parent;
        }

        var fallback = @"c:\Users\JefB\Documents\JBGITROOT\prism\jb\src\core\config";
        if (File.Exists(Path.Combine(fallback, "ImageNGP.json")))
            return fallback;

        throw new FileNotFoundException("Core config directory (with ImageNGP.json) not found.");
    }
}
