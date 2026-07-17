using Xunit;

namespace PrismCoreTests.ServiceHost;

/// <summary>
/// Real-HTTP roundtrip tests for HttpTransformService against Prism.ServiceHost.
/// Exercises the HTTP client calling the remote transform service with a MatchingResult payload.
/// </summary>
[Collection("Service Host")]
public class HttpTransformServiceTests
{
    private readonly ServiceHostFixture fixture;

    public HttpTransformServiceTests(ServiceHostFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task TransformAsync_WithMinimalMatchingResult_ReturnsTransformResult()
    {
        // Arrange
        var client = new HttpTransformService(fixture.Client);
        MatchingResult matched = ServiceHostTestHelpers.CreateMinimalMatchingResult();

        // Act
        TransformResult result = await client.TransformAsync(matched, false, false, null, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(matched.Ingest.JobID, result.Matched.Ingest.JobID);
        Assert.NotNull(result.Matched);
        Assert.NotNull(result.Matched.LambdaRecords);
    }

    [Fact]
    public async Task TransformAsync_ResultCarriesFamilyIDAndPhenotype()
    {
        // Arrange
        var client = new HttpTransformService(fixture.Client);
        MatchingResult matched = ServiceHostTestHelpers.CreateMinimalMatchingResult();
        string expectedFamilyID = matched.LambdaRecords[0].Family;
        string expectedPhenotype = matched.LambdaRecords[0].SelectedPhenotype;

        // Act
        TransformResult result = await client.TransformAsync(matched, false, false, null, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Matched.LambdaRecords);
        if (result.Matched.LambdaRecords.Count > 0)
        {
            // Family and phenotype should pass through unchanged (transform disabled).
            Assert.Equal(expectedFamilyID, result.Matched.LambdaRecords[0].Family);
            Assert.Equal(expectedPhenotype, result.Matched.LambdaRecords[0].SelectedPhenotype);
        }
    }

    [Fact]
    public async Task TransformAsync_HealthEndpoint_ReturnsOk()
    {
        // Arrange
        var httpClient = fixture.Client;

        // Act
        HttpResponseMessage response = await httpClient.GetAsync("/prism-service/transform/health");

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Health endpoint returned {response.StatusCode}");
    }
}
