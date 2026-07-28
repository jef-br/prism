using Xunit;

namespace PrismCoreTests.Transform;

/// <summary>
/// T-4820: TransformSeed resolves the Excel + CLIP seeding signals the Transformed stage steers on,
/// from the LAMBDA feature snapshot and the matched FamilyIDRecord, without recomputation. Guards the
/// resolution rules: Excel wins for product colour, SOLIDCOLOR = flat background, UNKNOWN/blank → null.
/// </summary>
public class TransformSeedTests {
    [Fact]
    public void Resolve_SurfacesClipAndExcelSignals_ExcelWinsForProductColor() {
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "a.jpg", ProductTypeId = "topwear" };
        lambda.Features.Set("product-color", "blue", 1.0, "clip");
        lambda.Features.Set("background-type", "SOLIDCOLOR", 1.0, "clip");
        lambda.Features.Set("background-color", "white", 1.0, "clip");
        FamilyIDRecord family = new("FAM001", new Dictionary<string, string> { ["productcolor"] = "navy" }, null, null, null, null);

        TransformSeed seed = TransformSeed.Resolve(lambda, family);

        Assert.Equal("topwear", seed.ProductTypeId);
        Assert.Equal("blue", seed.ClipProductColor);
        Assert.Equal("navy", seed.ExcelProductColor);
        Assert.Equal("navy", seed.EffectiveProductColor);
        Assert.Equal("SOLIDCOLOR", seed.BackgroundType);
        Assert.Equal("white", seed.BackgroundColor);
        Assert.True(seed.IsBackgroundFlat);
    }

    [Fact]
    public void Resolve_ClipFallbackForColor_AndNormalizesUnknownAndMissing() {
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "b.jpg" };
        lambda.Features.Set("product-color", "red", 1.0, "clip");
        // background-type left unmeasured (GetValue → "UNKNOWN"); no family record.

        TransformSeed seed = TransformSeed.Resolve(lambda, null);

        Assert.Equal("red", seed.EffectiveProductColor);
        Assert.Null(seed.ExcelProductColor);
        Assert.Null(seed.BackgroundType);
        Assert.False(seed.IsBackgroundFlat);
        Assert.Null(seed.ProductTypeId);
    }
}
