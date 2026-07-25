using Xunit;

namespace PrismCoreTests.ServiceHost;

/// <summary>
/// Failure-path coverage for the four public service HTTP clients. With the infinite transport timeout
/// (ServiceHttp.CreateClient) the clients' only failure bounds are connection errors, non-success
/// statuses, and the caller's CancellationToken — each is pinned here so a regression in distributed
/// error handling fails the suite instead of hanging a production job.
/// </summary>
[Collection("Service Host")]
public class HttpServiceFailurePathTests {
    private readonly ServiceHostFixture fixture;

    // Port 1 is never serviced on a dev/CI box: connect is refused immediately, no timeout wait.
    private static readonly Uri DeadHost = new("http://127.0.0.1:1/");

    public HttpServiceFailurePathTests(ServiceHostFixture fixture) {
        this.fixture = fixture;
    }

    [Fact]
    public async Task MatchAsync_HostUnreachable_ThrowsHttpRequestException() {
        var client = new HttpMatchingService(DeadHost);
        IngestResult ingest = ServiceHostTestHelpers.CreateMinimalIngestResult(createNormalizedJpeg: false);
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.MatchAsync(ingest, new LocalArtifactStore(), null, CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_HostUnreachable_ThrowsHttpRequestException() {
        var client = new HttpGenerateService(DeadHost);
        MatchingResult matched = ServiceHostTestHelpers.CreateMinimalMatchingResult();
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GenerateAsync(matched, false, null, CancellationToken.None));
    }

    [Fact]
    public async Task TransformAsync_HostUnreachable_ThrowsHttpRequestException() {
        var client = new HttpTransformService(DeadHost);
        MatchingResult matched = ServiceHostTestHelpers.CreateMinimalMatchingResult();
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.TransformAsync(matched, false, false, null, CancellationToken.None));
    }

    [Fact]
    public async Task UpscaleAsync_HostUnreachable_ThrowsHttpRequestException() {
        var client = new HttpUpscaleService(DeadHost);
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.UpscaleAsync(ServiceHostTestHelpers.CreateTestJpegBytes(), 2.0, CancellationToken.None));
    }

    [Fact]
    public async Task UpscaleAsync_ServerError_ThrowsHttpRequestException() {
        var client = new HttpUpscaleService(fixture.Client);
        byte[] notAnImage = [1, 2, 3];
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.UpscaleAsync(notAnImage, 2.0, CancellationToken.None));
    }

    [Fact]
    public async Task MatchAsync_PreCancelledToken_ThrowsOperationCanceled() {
        var client = new HttpMatchingService(fixture.Client);
        IngestResult ingest = ServiceHostTestHelpers.CreateMinimalIngestResult(createNormalizedJpeg: false);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.MatchAsync(ingest, new LocalArtifactStore(), null, cts.Token));
    }
}
