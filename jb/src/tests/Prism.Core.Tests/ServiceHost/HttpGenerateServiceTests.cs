using Xunit;

namespace PrismCoreTests.ServiceHost;

/// <summary>
/// Real-HTTP roundtrip tests for HttpGenerateService against Prism.ServiceHost.
/// Exercises the HTTP client calling the remote generate service with a MatchingResult payload.
/// </summary>
[Collection("Service Host")]
public class HttpGenerateServiceTests
{
    private readonly ServiceHostFixture fixture;

    public HttpGenerateServiceTests(ServiceHostFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task GenerateAsync_WithMinimalMatchingResult_ReturnsGenerateResult()
    {
        // Arrange
        var client = new HttpGenerateService(fixture.Client);
        MatchingResult matched = ServiceHostTestHelpers.CreateMinimalMatchingResult();

        // Act
        GenerateResult result = await client.GenerateAsync(matched, false, null, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(matched.Ingest.JobID, result.MatchedWithGenerations.Ingest.JobID);
        Assert.NotNull(result.MatchedWithGenerations);
        Assert.NotNull(result.GeneratedImages);
    }

    [Fact]
    public async Task GenerateAsync_ResultCarriesInputPhenotypes()
    {
        // Arrange
        var client = new HttpGenerateService(fixture.Client);
        MatchingResult matched = ServiceHostTestHelpers.CreateMinimalMatchingResult();
        string expectedPhenotype = matched.LambdaRecords[0].SelectedPhenotype;

        // Act
        GenerateResult result = await client.GenerateAsync(matched, false, null, CancellationToken.None);

        // Assert
        Assert.NotNull(result.MatchedWithGenerations.LambdaRecords);
        if (result.MatchedWithGenerations.LambdaRecords.Count > 0)
        {
            // Phenotype should be preserved through the generate stage (generation disabled).
            Assert.Equal(expectedPhenotype, result.MatchedWithGenerations.LambdaRecords[0].SelectedPhenotype);
        }
    }

    [Fact]
    public async Task GenerateAsync_HealthEndpoint_ReturnsOk()
    {
        // Arrange
        var httpClient = fixture.Client;

        // Act
        HttpResponseMessage response = await httpClient.GetAsync("/prism-service/generate/health");

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Health endpoint returned {response.StatusCode}");
    }
}
