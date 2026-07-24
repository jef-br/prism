using Xunit;

namespace PrismCoreTests.Ingest;

/// <summary>
/// Tests for the URL ingestion path: <see cref="FetchDispatcher"/> routing and the
/// <see cref="Fetch_HTTPS_DirectFile"/> fallback fetcher. Policy failures (blocked host, malformed
/// URL, HTTP error) must come back as KO records, never exceptions. Blocked-URL tests run against
/// the shipped HostRules.json without touching the network — validation precedes any request; the
/// success and HTTP-error paths use permissive rules against a loopback-only local server.
/// </summary>
public class FetcherTests : IClassFixture<ImporterFixture> {
    private readonly ImporterFixture fixture;

    public FetcherTests(ImporterFixture fixture) {
        this.fixture = fixture;
    }

    //  Dispatcher routing (shipped HostRules.json)

    [Theory]
    [InlineData("https://example.com/product.jpg", true)]
    [InlineData("http://example.com/product.jpg", true)]
    [InlineData("ftp://example.com/product.jpg", false)]
    [InlineData("not a url at all", false)]
    public void Dispatcher_CanHandle_FollowsAllowedSchemes(string url, bool expected) {
        FetchDispatcher dispatcher = FetchDispatcher.Create(fixture.ConfigDirectory);

        Assert.Equal(expected, dispatcher.CanHandle(url));
    }

    //  Blocked / malformed inputs — KO without network

    [Fact]
    public async Task DirectFile_LoopbackUrl_IsBlockedByShippedRules() {
        // networkRanges.allowLoopback is false in HostRules.json; the block fires before any connect.
        IFetchStrategy fetcher = Fetch_HTTPS_DirectFile.Create(fixture.ConfigDirectory);

        ImageRecord_INPUT record = await fetcher.FetchAsync("http://127.0.0.1:9/img.jpg", fixture.TempRoot, "job-blocked", CancellationToken.None);

        Assert.Equal(ImportStatus.KO, record.ImportStatus);
        Assert.Equal("fetch.url_blocked", record.KoReasonCode);
    }

    [Fact]
    public async Task DirectFile_MalformedUrl_KoBlocked() {
        IFetchStrategy fetcher = Fetch_HTTPS_DirectFile.Create(fixture.ConfigDirectory);

        ImageRecord_INPUT record = await fetcher.FetchAsync("::definitely not a url::", fixture.TempRoot, "job-malformed", CancellationToken.None);

        Assert.Equal(ImportStatus.KO, record.ImportStatus);
        Assert.Equal("fetch.url_blocked", record.KoReasonCode);
    }

    //  Download paths — permissive rules against a loopback server

    [Fact]
    public async Task DirectFile_LocalServer_DownloadsToJobTemp() {
        byte[] payload = new byte[8192];
        new Random(31).NextBytes(payload);
        using LoopbackHttpServer server = new(path => path == "/product.jpg" ? (200, payload) : (404, []));

        ImageRecord_INPUT record = await NewLoopbackFetcher().FetchAsync($"{server.BaseUrl}/product.jpg", fixture.TempRoot, "job-ok", CancellationToken.None);

        Assert.Equal(ImportStatus.Ok, record.ImportStatus);
        Assert.Equal(ImageSourceKind.RemoteUrl, record.SourceKind);
        Assert.Equal("product.jpg", record.InitialFullName);
        Assert.True(File.Exists(record.TempFilePath), "Downloaded file must exist in the job temp folder.");
        Assert.Equal(payload, await File.ReadAllBytesAsync(record.TempFilePath!));
    }

    [Fact]
    public async Task DirectFile_HttpError_KoHttpError() {
        using LoopbackHttpServer server = new(_ => (404, []));

        ImageRecord_INPUT record = await NewLoopbackFetcher().FetchAsync($"{server.BaseUrl}/missing.jpg", fixture.TempRoot, "job-404", CancellationToken.None);

        Assert.Equal(ImportStatus.KO, record.ImportStatus);
        Assert.Equal("fetch.http_error", record.KoReasonCode);
    }

    //  Helpers

    /// <summary>
    /// A direct-file fetcher whose rules admit the loopback test server. Mirrors what
    /// Fetch_HTTPS_DirectFile.Create builds, minus the production loopback/localhost blocks.
    /// </summary>
    private static Fetch_HTTPS_DirectFile NewLoopbackFetcher() {
        HostRules_Config rules = new() {
            AllowedSchemes      = ["http", "https"],
            BlockedSchemes      = ["ftp"],
            BlockedHostPatterns = [],
            Redirects = new() {
                AllowGenericDirectFileRedirects = false,
                AllowFetcherOwnedRedirects       = false
            },
            NetworkRanges = new() {
                AllowPrivate               = true,
                AllowLinkLocal             = true,
                AllowLoopback              = true,
                RejectAnyLoopbackDnsResult = false
            },
            Timeouts = new() {
                ConnectSeconds        = 10,
                ResponseHeaderSeconds = 10,
                IdleReadSeconds       = 10,
                TotalFetchSeconds     = 30
            },
            WeTransferPolling = new() {
                ConsentClickTimeoutMs         = 5000,
                ConsentHiddenWaitTimeoutMs    = 10000,
                ConsentSettleDelayMs          = 300,
                DownloadButtonClickTimeoutMs  = 10000,
                DownloadWaitTimeoutMs         = 60000,
                StreamBufferSizeBytes         = 81920,
                MaxDownloadGb                 = 10,
                ConsentBannerPasses           = 2
            },
            Testing = new() {
                AllowLocalhost = true
            }
        };

        HttpClientHandler handler = new() { AllowAutoRedirect = false, UseCookies = false };
        HttpClient http = new(handler) { Timeout = Timeout.InfiniteTimeSpan };
        return new Fetch_HTTPS_DirectFile(rules, http);
    }
}
