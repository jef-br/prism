using Xunit;

namespace PrismCoreTests.Classify;

/// <summary>
/// Unit tests for <see cref="ImageFeatureSnapshot"/>: the per-image feature dictionary.
/// </summary>
public class ImageFeatureSnapshotTests
{
    // ─── Default / UNKNOWN contract ────────────────────────────────────────────

    [Fact]
    public void GetValue_UnsetFeature_ReturnsUNKNOWN()
    {
        var snapshot = new ImageFeatureSnapshot();
        Assert.Equal("UNKNOWN", snapshot.GetValue("hero-is-human"));
    }

    [Fact]
    public void GetValue_UnsetFeature_NeverReturnsNull()
    {
        var snapshot = new ImageFeatureSnapshot();
        Assert.NotNull(snapshot.GetValue("nonexistent-feature"));
    }

    // ─── Round-trip: Set → GetValue ────────────────────────────────────────────

    [Fact]
    public void GetValue_AfterSet_ReturnsValue()
    {
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("aspect-ratio", "1.3333", 1.0, "geometry");
        Assert.Equal("1.3333", snapshot.GetValue("aspect-ratio"));
    }

    [Fact]
    public void Set_OverwritesPreviousValue()
    {
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("background-type", "REALLIFE", 0.72, "heuristic");
        snapshot.Set("background-type", "SOLIDCOLOR", 0.92, "imagesharp");
        Assert.Equal("SOLIDCOLOR", snapshot.GetValue("background-type"));
    }

    [Fact]
    public void Set_EmptyStringValue_IsStoredNotConvertedToUnknown()
    {
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("product-color", "", 0.0, "heuristic");
        Assert.Equal("", snapshot.GetValue("product-color"));
    }

    // ─── Case-insensitive key lookup ───────────────────────────────────────────

    [Fact]
    public void GetValue_CaseInsensitive_FindsFeatureRegardlessOfCase()
    {
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("aspect-ratio", "0.7500", 1.0, "geometry");

        Assert.Equal("0.7500", snapshot.GetValue("ASPECT-RATIO"));
        Assert.Equal("0.7500", snapshot.GetValue("Aspect-Ratio"));
        Assert.Equal("0.7500", snapshot.GetValue("aspect-ratio"));
    }

    // ─── TryGet contract ───────────────────────────────────────────────────────

    [Fact]
    public void TryGet_WhenNotSet_ReturnsFalseAndNullValue()
    {
        var snapshot = new ImageFeatureSnapshot();
        bool found = snapshot.TryGet("hero-orientation", out ImageFeatureValue? value);
        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void TryGet_WhenSet_ReturnsTrueAndPopulatesValue()
    {
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("white-background", "true", 0.92, "imagesharp");

        bool found = snapshot.TryGet("white-background", out ImageFeatureValue? value);

        Assert.True(found);
        Assert.NotNull(value);
        Assert.Equal("true", value.Value);
        Assert.Equal(0.92, value.Confidence);
        Assert.Equal("imagesharp", value.Source);
    }

    [Fact]
    public void TryGet_ConfidencePreservedExactly()
    {
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("skin-tone-area", "0.1234", 0.75, "imagesharp");

        snapshot.TryGet("skin-tone-area", out ImageFeatureValue? value);
        Assert.Equal(0.75, value!.Confidence);
    }

    [Fact]
    public void TryGet_ZeroConfidence_IsPreserved()
    {
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("pose-type", "UNKNOWN", 0.0, "heuristic");

        snapshot.TryGet("pose-type", out ImageFeatureValue? value);
        Assert.Equal(0.0, value!.Confidence);
    }

    [Fact]
    public void TryGet_SourcePreservedExactly()
    {
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("intersection-count", "2", 0.85, "heuristic");

        snapshot.TryGet("intersection-count", out ImageFeatureValue? value);
        Assert.Equal("heuristic", value!.Source);
    }

    // ─── All dictionary ────────────────────────────────────────────────────────

    [Fact]
    public void All_ReturnsAllSetFeatures()
    {
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("aspect-ratio", "1.0000", 1.0, "geometry");
        snapshot.Set("white-background", "true", 0.92, "imagesharp");
        snapshot.Set("intersection-count", "0", 0.85, "heuristic");

        Assert.Equal(3, snapshot.All.Count);
        Assert.True(snapshot.All.ContainsKey("aspect-ratio"));
        Assert.True(snapshot.All.ContainsKey("white-background"));
        Assert.True(snapshot.All.ContainsKey("intersection-count"));
    }

    [Fact]
    public void All_IsReadOnly_CannotBeModifiedDirectly()
    {
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("aspect-ratio", "1.0000", 1.0, "geometry");

        var all = snapshot.All;
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, ImageFeatureValue>>(all);
    }

    [Fact]
    public void All_StartsEmpty()
    {
        var snapshot = new ImageFeatureSnapshot();
        Assert.Empty(snapshot.All);
    }
}
