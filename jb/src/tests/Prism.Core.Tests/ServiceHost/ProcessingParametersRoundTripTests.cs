using System.Text.Json;
using Xunit;

namespace PrismCoreTests.ServiceHost;

/// <summary>
/// T-4930: the ESRGAN toggle reaches the Transform stage on <see cref="PrismProcessingParameters"/>, which
/// rides inside MatchingResult across the matching→transform HTTP boundary. The flag crosses two different
/// serializer configurations on the way, and they do not agree on naming — so each is exercised against the
/// real thing rather than against a self-consistent stand-in that would pass either way.
/// </summary>
public class ProcessingParametersRoundTripTests {
    // What the ServiceHost deserializes with: ConfigureHttpJsonOptions starts from web defaults, and
    // Prism.ServiceHost/Program.cs then sets the naming policy back to null (PascalCase on the wire).
    // Replicated rather than referenced because that configuration is built inline in a top-level Program.
    private static readonly JsonSerializerOptions ServiceHostOptions =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = null };

    // What PrismProcessIngressReader.DeserializeRequest reads the browser's request part with.
    private static readonly JsonSerializerOptions ApiIngressOptions =
        new() { PropertyNameCaseInsensitive = true };

    // The real wire path: HttpTransformService POSTs MatchingResult with ServiceHttp.Json, the Transform
    // host reads it with its own options. Serializing with the actual client object means a change to it
    // is caught here rather than in a distributed run.
    [Fact]
    public void AllowEsrganUpscale_SurvivesTheClientToHostWirePath() {
        PrismProcessingParameters original = new() { AllowEsrganUpscale = true, Headcut = true, Format = "json" };

        string wire = JsonSerializer.Serialize(original, ServiceHttp.Json);
        PrismProcessingParameters restored = JsonSerializer.Deserialize<PrismProcessingParameters>(wire, ServiceHostOptions)!;

        Assert.True(restored.AllowEsrganUpscale);
        Assert.True(restored.Headcut);
        Assert.Equal("json", restored.Format);
    }

    // The two configurations disagree on casing, so the property name has to be read case-insensitively at
    // both ends. Pinning the actual wire text keeps that from being an accident.
    [Fact]
    public void WireFormat_IsPascalCase_AndReadsBackUnderEitherConfiguration() {
        string wire = JsonSerializer.Serialize(new PrismProcessingParameters { AllowEsrganUpscale = true }, ServiceHttp.Json);

        Assert.Contains("\"AllowEsrganUpscale\":true", wire);
        Assert.True(JsonSerializer.Deserialize<PrismProcessingParameters>(wire, ServiceHostOptions)!.AllowEsrganUpscale);
        Assert.True(JsonSerializer.Deserialize<PrismProcessingParameters>(wire, ApiIngressOptions)!.AllowEsrganUpscale);
    }

    // The workbench sends camelCase; the API ingress reader is the only thing that makes that work.
    [Fact]
    public void WorkbenchCamelCase_IsAcceptedByTheApiIngressReader() {
        PrismProcessingParameters restored = JsonSerializer.Deserialize<PrismProcessingParameters>(
            """{"allowEsrganUpscale":true,"format":"zip"}""", ApiIngressOptions)!;

        Assert.True(restored.AllowEsrganUpscale);
    }

    // Default-off is the product decision, not an accident of bool's default: an omitted field must land on
    // the cheap Lanczos path under every configuration the flag travels through.
    [Fact]
    public void OmittedField_DefaultsToOff_UnderEveryConfiguration() {
        Assert.False(JsonSerializer.Deserialize<PrismProcessingParameters>("""{"Format":"zip"}""", ServiceHostOptions)!.AllowEsrganUpscale);
        Assert.False(JsonSerializer.Deserialize<PrismProcessingParameters>("""{"format":"zip"}""", ApiIngressOptions)!.AllowEsrganUpscale);
        Assert.False(new PrismProcessingParameters().AllowEsrganUpscale);
    }
}
