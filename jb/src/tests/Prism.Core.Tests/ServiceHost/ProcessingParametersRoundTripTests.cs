using System.Text.Json;
using Xunit;

namespace PrismCoreTests.ServiceHost;

/// <summary>
/// T-4930: the ESRGAN toggle reaches the Transform stage on <see cref="PrismProcessingParameters"/>,
/// which rides inside MatchingResult across the matching→transform HTTP boundary — so the flag has to
/// survive a System.Text.Json round-trip under the same web defaults the ServiceHost routes use, and
/// has to read false when the caller omitted it.
/// </summary>
public class ProcessingParametersRoundTripTests {
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void AllowEsrganUpscale_SurvivesTheServiceBoundaryRoundTrip() {
        PrismProcessingParameters original = new() { AllowEsrganUpscale = true, Headcut = true, Format = "json" };

        string json = JsonSerializer.Serialize(original, WebOptions);
        PrismProcessingParameters restored = JsonSerializer.Deserialize<PrismProcessingParameters>(json, WebOptions)!;

        Assert.True(restored.AllowEsrganUpscale);
        Assert.True(restored.Headcut);
        Assert.Equal("json", restored.Format);
    }

    // Default-off is the product decision, not an accident of bool's default: an omitted field and an
    // explicit false must both land on the cheap Lanczos path.
    [Fact]
    public void OmittedField_DefaultsToOff() {
        PrismProcessingParameters restored = JsonSerializer.Deserialize<PrismProcessingParameters>(
            """{"format":"zip"}""", WebOptions)!;

        Assert.False(restored.AllowEsrganUpscale);
        Assert.False(new PrismProcessingParameters().AllowEsrganUpscale);
    }

    [Fact]
    public void ExplicitTrue_IsCarriedNotIgnored() {
        PrismProcessingParameters restored = JsonSerializer.Deserialize<PrismProcessingParameters>(
            """{"allowEsrganUpscale":true}""", WebOptions)!;

        Assert.True(restored.AllowEsrganUpscale);
    }
}
