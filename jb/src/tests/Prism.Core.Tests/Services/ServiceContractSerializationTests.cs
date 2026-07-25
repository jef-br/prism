using System.Text.Json;
using Xunit;

namespace PrismCoreTests.Services;

/// <summary>
/// Guards the JSON wire contract the Phase 2 HTTP services depend on. The pipeline result records cross
/// process boundaries as JSON (PascalCase), so any type inside them must survive a round-trip. A regression
/// here is invisible in-process but breaks the distributed pipeline — e.g. a FamilyIDRecord whose get-only
/// CanonicalProperties deserialize empty makes every non-FamilyID matcher rule silently fail.
/// </summary>
public class ServiceContractSerializationTests {
    // The HTTP clients and service host both use PascalCase (PropertyNamingPolicy = null).
    private static readonly JsonSerializerOptions WireOptions = new() { PropertyNamingPolicy = null };

    [Fact]
    public void FamilyRecord_RoundTrips_CanonicalProperties_AndCaseInsensitiveLookup() {
        var family = new FamilyIDRecord("90861025");
        family.MergeProperty(
            new ExcelPropertyValue("article", ["20213024"], []),
            ExcelColumnClassification.Numerical);

        string json = JsonSerializer.Serialize(family, WireOptions);
        FamilyIDRecord restored = JsonSerializer.Deserialize<FamilyIDRecord>(json, WireOptions)!;

        Assert.Equal("90861025", restored.FamilyID);
        Assert.True(restored.CanonicalProperties.ContainsKey("article"), "CanonicalProperties lost over JSON.");
        Assert.Equal(family.CanonicalProperties["article"], restored.CanonicalProperties["article"]);
        // The matchers look up columns case-insensitively — the comparer must survive the round-trip.
        Assert.True(restored.CanonicalProperties.ContainsKey("ARTICLE"), "Case-insensitive comparer lost over JSON.");
    }

    [Fact]
    public void ImageRecordLambda_RoundTrips_Features() {
        var lambda = new ImageRecord_LAMBDA { InitialFullName = "x.jpg", SelectedPhenotype = "front-packshot" };
        lambda.Features.Set("hero-is-human", "FALSE", 0.67, "clip");

        string json = JsonSerializer.Serialize(lambda, WireOptions);
        ImageRecord_LAMBDA restored = JsonSerializer.Deserialize<ImageRecord_LAMBDA>(json, WireOptions)!;

        Assert.Equal("front-packshot", restored.SelectedPhenotype);
        Assert.Equal("FALSE", restored.Features.GetValue("hero-is-human"));
    }

    /// <summary>
    /// T-3500: NormalizedJpegBytes is the in-process Import→Match handoff (avoids a redundant decode of
    /// NormalizedJpgPath when both stages run in the same process). It must never cross the HTTP boundary —
    /// a real, separately-deployed Matching service (HttpMatchingService/Prism.ServiceHost) has no use for
    /// stale bytes and must always fall back to reading NormalizedJpgPath from the shared job folder. This
    /// guards the [JsonIgnore] on that field: NormalizedJpgPath and the other wire fields must still survive
    /// the round trip untouched.
    /// </summary>
    [Fact]
    public void ImageRecordInput_RoundTrips_ButOmitsInMemoryNormalizedBytes() {
        var input = new ImageRecord_INPUT {
            InitialFullName = "x.jpg",
            NormalizedJpgPath = @"C:\job\normalized\x.jpg",
            NormalizedJpegBytes = [1, 2, 3, 4],
            NormalizedWidth = 800,
            NormalizedHeight = 600
        };

        string json = JsonSerializer.Serialize(input, WireOptions);
        ImageRecord_INPUT restored = JsonSerializer.Deserialize<ImageRecord_INPUT>(json, WireOptions)!;

        Assert.DoesNotContain("NormalizedJpegBytes", json);
        Assert.Equal(@"C:\job\normalized\x.jpg", restored.NormalizedJpgPath);
        Assert.Equal(800, restored.NormalizedWidth);
        Assert.Equal(600, restored.NormalizedHeight);
        Assert.Null(restored.NormalizedJpegBytes);
    }
}
