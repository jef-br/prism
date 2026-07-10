using Prism.Core;
using Xunit;

namespace PrismCoreTests.Analyzers;

/// <summary>
/// Tests for ProductTypeResolver value/token mapping and the producttype-over-ngp precedence,
/// plus Analyzer_ProductType / Analyzer_FilenameEvidence evidence wiring.
/// </summary>
public class ProductTypeResolverTests
{
    private static ProductTypeResolver LoadResolver()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "jb", "src", "core", "config", "ProductTypeMap.json");
            if (File.Exists(candidate)) return ProductTypeResolver.Load(candidate);
        }
        throw new InvalidOperationException("ProductTypeMap.json not found in source tree.");
    }

    private static FamilyIDRecord Family(string familyId, params (string Key, string Value)[] properties)
    {
        var record = new FamilyIDRecord(familyId);
        foreach ((string key, string value) in properties)
            record.MergeProperty(new ExcelPropertyValue(key, [value], []), ExcelColumnClassification.Categorical);
        return record;
    }

    [Fact]
    public void ResolveValue_SpanishTerm_MapsToClothingTops()
    {
        Assert.Equal("clothing-tops", LoadResolver().ResolveValue("camiseta"));
    }

    [Fact]
    public void ResolveValue_CanonicalSlug_PassesThrough()
    {
        Assert.Equal("footwear", LoadResolver().ResolveValue("Footwear"));
    }

    [Fact]
    public void ResolveValue_MultiWordValue_ResolvesViaToken()
    {
        Assert.Equal("bags-accessories", LoadResolver().ResolveValue("Leather tote bag"));
    }

    [Fact]
    public void ResolveValue_Unmapped_ReturnsNull()
    {
        Assert.Null(LoadResolver().ResolveValue("zzz-unmappable-thing"));
    }

    [Fact]
    public void ResolveFromFamily_ProducttypeWinsOverNgp()
    {
        FamilyIDRecord family = Family("10000001", ("producttype", "camiseta"), ("ngp", "sneakers"));
        Assert.Equal("clothing-tops", LoadResolver().ResolveFromFamily(family));
    }

    [Fact]
    public void ResolveFromFamily_NgpUsedWhenProducttypeAbsent()
    {
        FamilyIDRecord family = Family("10000002", ("ngp", "sneakers"));
        Assert.Equal("footwear", LoadResolver().ResolveFromFamily(family));
    }

    [Fact]
    public void AnalyzerProductType_IemBeatsClipLabel()
    {
        var lambda = new ImageRecord_LAMBDA { InitialFullName = "x.jpg" };
        lambda.Features.Set("product-type-label", "shoes", 0.9, "onnx");
        FamilyIDRecord family = Family("10000003", ("producttype", "camiseta"));

        Analyzer_ProductType.Analyze(lambda, family, LoadResolver());

        Assert.Equal("clothing-tops", lambda.ProductTypeId);
    }

    [Fact]
    public void AnalyzerProductType_ClipLabelFallbackWhenNoFamily()
    {
        var lambda = new ImageRecord_LAMBDA { InitialFullName = "x.jpg" };
        lambda.Features.Set("product-type-label", "shoes", 0.9, "onnx");

        Analyzer_ProductType.Analyze(lambda, null, LoadResolver());

        Assert.Equal("footwear", lambda.ProductTypeId);
    }

    [Fact]
    public void FilenameEvidence_ProductTypeAndOrientationFromTokens()
    {
        var lambda = new ImageRecord_LAMBDA { InitialFullName = "headphone_4435345_A_FRONT.jpg" };

        Analyzer_FilenameEvidence.Analyze(lambda, LoadResolver(), new FilenameAnalyzerConfig());

        Assert.Equal("electronics-small", lambda.ProductTypeId);
        Assert.Equal("FRONT", lambda.Features.GetValue("hero-orientation"));
    }

    [Fact]
    public void FilenameEvidence_DoesNotOverrideStrongerOrientation()
    {
        var lambda = new ImageRecord_LAMBDA { InitialFullName = "shirt_123_BACK.jpg" };
        lambda.Features.Set("hero-orientation", "FRONT", 0.95, "onnx");

        Analyzer_FilenameEvidence.Analyze(lambda, LoadResolver(), new FilenameAnalyzerConfig());

        Assert.Equal("FRONT", lambda.Features.GetValue("hero-orientation"));
    }

    [Fact]
    public void FilenameEvidence_DoesNotOverrideIemProductType()
    {
        var lambda = new ImageRecord_LAMBDA { InitialFullName = "headphone_1.jpg", ProductTypeId = "clothing-tops" };

        Analyzer_FilenameEvidence.Analyze(lambda, LoadResolver(), new FilenameAnalyzerConfig());

        Assert.Equal("clothing-tops", lambda.ProductTypeId);
    }
}
