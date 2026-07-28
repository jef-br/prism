using Prism.Services.Matching;
using Xunit;

namespace PrismCoreTests.Classify;

/// <summary>
/// Pins the seed read-model that steers subject detection (T-4860 toggles a and b): Excel-over-CLIP
/// precedence for product colour, UNKNOWN normalising to absent, and the two derived signals the
/// detector branches on.
/// </summary>
public class SubjectSeedHintTests {
    private static ImageFeatureSnapshot Features(params (string Id, string Value)[] values) {
        ImageFeatureSnapshot snapshot = new();
        foreach ((string id, string value) in values) snapshot.Set(id, value, 1.0, "test");
        return snapshot;
    }

    // CanonicalProperties is get-only, so the JSON round-trip constructor is the supported way to seed one.
    private static FamilyIDRecord Family(params (string Key, string Value)[] properties) {
        Dictionary<string, string> canonical = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, string value) in properties) canonical[key] = value;
        return new FamilyIDRecord("FAM1", canonical, null, null, null, null);
    }

    [Fact]
    public void Resolve_ExcelProductColour_WinsOverClip() {
        SubjectSeedHint seed = SubjectSeedHint.Resolve(Features(("product-color", "blue")), Family(("productcolor", "red")));

        Assert.Equal("red", seed.EffectiveProductColor);
    }

    [Fact]
    public void Resolve_NoExcelRecord_FallsBackToClipColour() {
        SubjectSeedHint seed = SubjectSeedHint.Resolve(Features(("product-color", "blue")), family: null);

        Assert.Equal("blue", seed.EffectiveProductColor);
    }

    [Fact]
    public void Resolve_UnknownFeature_ReadsAsAbsent() {
        SubjectSeedHint seed = SubjectSeedHint.Resolve(Features(("product-color", "UNKNOWN"), ("background-color", "  ")), family: null);

        Assert.Null(seed.EffectiveProductColor);
        Assert.Null(seed.BackgroundColor);
    }

    [Fact]
    public void ProductNearBackground_SameColour_IsTrue() {
        SubjectSeedHint seed = SubjectSeedHint.Resolve(Features(("product-color", "white"), ("background-color", "WHITE")), family: null);

        Assert.True(seed.ProductNearBackground);
    }

    [Fact]
    public void ProductNearBackground_DifferentColour_IsFalse() {
        SubjectSeedHint seed = SubjectSeedHint.Resolve(Features(("product-color", "red"), ("background-color", "white")), family: null);

        Assert.False(seed.ProductNearBackground);
    }

    [Fact]
    public void ProductNearBackground_ColourMissing_IsFalse() {
        // Not "near" — unknown. The detector treats this case conservatively on its own side (it keeps
        // CLAHE on); this property must not claim a match it cannot see.
        SubjectSeedHint seed = SubjectSeedHint.Resolve(Features(("product-color", "white")), family: null);

        Assert.False(seed.ProductNearBackground);
    }

    [Fact]
    public void IsBackgroundFlat_SolidColour_IsTrue() {
        SubjectSeedHint seed = SubjectSeedHint.Resolve(Features(("background-type", "SOLIDCOLOR")), family: null);

        Assert.True(seed.IsBackgroundFlat);
    }

    [Fact]
    public void IsBackgroundFlat_RealLife_IsFalse() {
        SubjectSeedHint seed = SubjectSeedHint.Resolve(Features(("background-type", "REALLIFE")), family: null);

        Assert.False(seed.IsBackgroundFlat);
    }

    [Fact]
    public void IsBackgroundFlat_Unknown_IsFalse_NotTreatedAsSolidColour() {
        // The inversion the T-4860 review caught on the Transform side: an unmeasured background is not a
        // known-flat one. Reading UNKNOWN as flat would skip detection effort with no evidence for it.
        SubjectSeedHint unknown = SubjectSeedHint.Resolve(Features(("background-type", "UNKNOWN")), family: null);
        SubjectSeedHint absent = SubjectSeedHint.Resolve(Features(), family: null);

        Assert.False(unknown.IsBackgroundFlat);
        Assert.False(absent.IsBackgroundFlat);
    }
}
