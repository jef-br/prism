using Xunit;

namespace PrismCoreTests.ServiceHost;

/// <summary>
/// Real-HTTP roundtrip tests for HttpMatchingService against Prism.ServiceHost.
/// Every test exercises the actual HTTP client calling through to the remote matching service,
/// not mocked, against a real in-memory WebApplicationFactory host.
/// </summary>
[Collection("Service Host")]
public class HttpMatchingServiceTests
{
    private readonly ServiceHostFixture fixture;

    public HttpMatchingServiceTests(ServiceHostFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task MatchAsync_WithMinimalIngestResult_ReturnsMatchingResult()
    {
        // Arrange
        var client = new HttpMatchingService(fixture.Client);
        IngestResult ingest = ServiceHostTestHelpers.CreateMinimalIngestResult();

        // Act
        MatchingResult result = await client.MatchAsync(ingest, new LocalArtifactStore(), null, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ingest.JobID, result.Ingest.JobID);
        Assert.NotNull(result.LambdaRecords);
    }

    [Fact]
    public async Task MatchAsync_ResultCarriesInputImageCount()
    {
        // Arrange
        var client = new HttpMatchingService(fixture.Client);
        IngestResult ingest = ServiceHostTestHelpers.CreateMinimalIngestResult();

        // Act
        MatchingResult result = await client.MatchAsync(ingest, new LocalArtifactStore(), null, CancellationToken.None);

        // Assert
        // Verify the result has the expected schema shape.
        Assert.NotNull(result.Ingest);
        Assert.NotNull(result.Ingest.Parameters);

        // LambdaRecords should contain one record per input image.
        // Some may be KO'd (IsKo=true), others OK — total should match input count.
        Assert.Equal(ingest.NormalizedImages.Count, result.LambdaRecords.Count);
    }

    [Fact]
    public async Task MatchAsync_HealthEndpoint_ReturnsOk()
    {
        // Arrange
        var httpClient = fixture.Client;

        // Act
        HttpResponseMessage response = await httpClient.GetAsync("/prism-service/match/health");

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Health endpoint returned {response.StatusCode}");
    }
}
