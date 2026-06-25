using System.Net;
using System.Net.Http.Headers;

namespace Prism.Core;

/// <summary>
/// Downloads a file from a direct HTTP/HTTPS URL, validates the request against HostRules.json,
/// streams the response to the job temp folder, and returns an <see cref="ImageRecord_INPUT"/>.
/// Acts as the fallback fetcher — handles any URL not claimed by a specialist strategy.
/// </summary>
internal sealed class Fetch_HTTPS_DirectFile : IFetchStrategy
{
    private const string KoReasonBlocked    = "fetch.url_blocked";
    private const string KoReasonHttpError  = "fetch.http_error";
    private const string KoReasonTimeout    = "fetch.timeout";
    private const string KoReasonNetworkErr = "fetch.network_error";

    private readonly HostRules_Config _rules;
    private readonly HttpClient _http;

    /// <summary>
    /// Creates an instance backed by the given host rules and a shared <see cref="HttpClient"/>.
    /// The <paramref name="http"/> client must have no automatic redirect handler —
    /// redirect behaviour is controlled by HostRules.
    /// </summary>
    internal Fetch_HTTPS_DirectFile(HostRules_Config rules, HttpClient http)
    {
        _rules = rules;
        _http  = http;
    }

    /// <summary>
    /// Loads HostRules.json from <paramref name="configDirectory"/> and creates an instance
    /// with a redirect-aware <see cref="HttpClient"/> configured from the loaded rules.
    /// </summary>
    internal static Fetch_HTTPS_DirectFile Create(string configDirectory)
    {
        HostRules_Config rules = HostRules_Config.Load(configDirectory);

        // Disable automatic redirects so we can apply redirect policy from HostRules.
        var handler = new HttpClientHandler();
        handler.AllowAutoRedirect = false;
        handler.UseCookies        = false;

        var http = new HttpClient(handler);
        http.Timeout = Timeout.InfiniteTimeSpan; // CancellationToken drives timeout instead.
        http.DefaultRequestHeaders.UserAgent.ParseAdd("PRISM-Fetcher/1.0");

        return new Fetch_HTTPS_DirectFile(rules, http);
    }

    /// <summary>
    /// Returns true for any URL whose scheme appears in <c>allowedSchemes</c> in HostRules.json.
    /// Specialist fetchers (WeTransfer, DropBox) claim their URLs first; this class handles the rest.
    /// </summary>
    public bool CanHandle(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) {
            return false;
        }

        return _rules.AllowedSchemes.Any(s => string.Equals(s, uri.Scheme, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Downloads the resource at <paramref name="url"/>, applying HostRules validation at every step.
    /// On validation failure or network error, returns a KO <see cref="ImageRecord_INPUT"/> rather than throwing.
    /// </summary>
    public async Task<ImageRecord_INPUT> FetchAsync(string url, string jobTempFolder, string jobID, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? startUri)) {
            return KoRecord(url, KoReasonBlocked, "The URL is not well-formed.");
        }

        string? blockReason = CheckUrlBlocked(startUri);
        if (blockReason is not null) {
            return KoRecord(url, KoReasonBlocked, blockReason);
        }

        using var totalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        totalCts.CancelAfter(TimeSpan.FromSeconds(_rules.TotalFetchSeconds));
        var ct = totalCts.Token;

        try {
            Uri resolvedUri = await FollowRedirectsAsync(startUri, url, ct);
            return await DownloadToTempAsync(resolvedUri, url, jobTempFolder, ct);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            // Total-fetch timeout fired (not the external caller's cancellation).
            return KoRecord(url, KoReasonTimeout,
                $"The download did not complete within the configured {_rules.TotalFetchSeconds}-second limit.");
        } catch (OperationCanceledException) {
            // Caller cancelled — propagate normally.
            throw;
        } catch (HttpRequestException ex) {
            return KoRecord(url, KoReasonNetworkErr, $"Network error: {ex.Message}");
        } catch (InvalidOperationException ex) {
            // Redirect policy violation surfaced as InvalidOperationException by FollowRedirectsAsync.
            return KoRecord(url, KoReasonBlocked, ex.Message);
        }
    }

    // -------------------------------------------------------------------------
    // Redirect resolution
    // -------------------------------------------------------------------------

    /// <summary>
    /// Follows HTTP redirects up to the redirect limit configured in HostRules.
    /// Validates each hop against the block rules.
    /// </summary>
    private async Task<Uri> FollowRedirectsAsync(Uri uri, string originalUrl, CancellationToken ct)
    {
        // Maximum redirect hops: use responseHeaderSeconds as a proxy budget;
        // HostRules.json does not expose a redirect count field — cap at 10.
        const int MaxRedirects = 10;
        int hops = 0;

        while (true) {
            using var req = new HttpRequestMessage(HttpMethod.Head, uri);
            using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts2.CancelAfter(TimeSpan.FromSeconds(_rules.ResponseHeaderSeconds));

            HttpResponseMessage resp;
            try {
                resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts2.Token);
            } catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
                throw new OperationCanceledException(ct); // treat response-header timeout as overall timeout
            }

            using (resp) {
                bool isRedirect = resp.StatusCode is
                    HttpStatusCode.MovedPermanently or
                    HttpStatusCode.Found or
                    HttpStatusCode.SeeOther or
                    HttpStatusCode.TemporaryRedirect or
                    HttpStatusCode.PermanentRedirect;

                if (!isRedirect) {
                    return uri;
                }

                if (!_rules.AllowGenericDirectFileRedirects) {
                    throw new InvalidOperationException(
                        "The server issued a redirect, but HostRules.json prohibits generic direct-file redirects.");
                }

                Uri? location = resp.Headers.Location;
                if (location is null) {
                    return uri;
                }

                // Resolve relative redirect URIs against the current base.
                if (!location.IsAbsoluteUri) {
                    location = new Uri(uri, location);
                }

                string? blockReason = CheckUrlBlocked(location);
                if (blockReason is not null) {
                    throw new InvalidOperationException($"Redirect target blocked: {blockReason}");
                }

                uri = location;
                hops++;

                if (hops > MaxRedirects) {
                    throw new InvalidOperationException(
                        $"Exceeded maximum redirect depth of {MaxRedirects} hops.");
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Download
    // -------------------------------------------------------------------------

    /// <summary>
    /// Streams the response body to a temp file inside <paramref name="jobTempFolder"/>
    /// and returns an OK <see cref="ImageRecord_INPUT"/> with <see cref="ImageRecord_INPUT.TempFilePath"/> set.
    /// </summary>
    private async Task<ImageRecord_INPUT> DownloadToTempAsync(Uri uri, string originalUrl, string jobTempFolder, CancellationToken ct)
    {
        Directory.CreateDirectory(jobTempFolder);

        using var req = new HttpRequestMessage(HttpMethod.Get, uri);

        HttpResponseMessage resp;
        try {
            var cts2 = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts2.CancelAfter(TimeSpan.FromSeconds(_rules.ResponseHeaderSeconds));
            resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts2.Token);
        } catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            throw new OperationCanceledException(ct);
        }

        using (resp) {
            if (!resp.IsSuccessStatusCode) {
                return KoRecord(originalUrl, KoReasonHttpError,
                    $"The server returned HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}.");
            }

            string fileName    = ResolveFileName(uri, resp.Content.Headers);
            string destPath    = Path.Combine(jobTempFolder, fileName);
            long? contentLen   = resp.Content.Headers.ContentLength;

            await using var src  = await resp.Content.ReadAsStreamAsync(ct);
            await using var dest = new FileStream(destPath, FileMode.Create, FileAccess.Write,
                FileShare.None, bufferSize: 81_920, useAsync: true);

            await StreamWithIdleTimeoutAsync(src, dest, _rules.IdleReadSeconds, ct);

            ImageRecord_INPUT record = new();
            record.InitialFullName = fileName;
            record.TempFilePath    = destPath;
            record.SourceKind      = ImageSourceKind.RemoteUrl;
            record.ByteLength      = contentLen ?? new FileInfo(destPath).Length;
            record.ImportStatus    = ImportStatus.Ok;

            return record;
        }
    }

    // -------------------------------------------------------------------------
    // Validation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns a human-readable block reason when the URI violates HostRules, or null when it is permitted.
    /// </summary>
    private string? CheckUrlBlocked(Uri uri)
    {
        if (!_rules.AllowedSchemes.Any(s => string.Equals(s, uri.Scheme, StringComparison.OrdinalIgnoreCase))) {
            return $"URL scheme '{uri.Scheme}' is not in the HostRules allowedSchemes list.";
        }

        if (_rules.BlockedSchemes.Any(s => string.Equals(s, uri.Scheme, StringComparison.OrdinalIgnoreCase))) {
            return $"URL scheme '{uri.Scheme}' is explicitly blocked by HostRules.";
        }

        string host = uri.Host;

        if (!_rules.AllowLoopback && IsLoopback(host)) {
            return $"Loopback addresses are not permitted by HostRules (host: {host}).";
        }

        if (!_rules.AllowLocalhost && IsLocalhost(host)) {
            return $"Localhost is not permitted by HostRules (host: {host}).";
        }

        if (_rules.RejectAnyLoopbackDnsResult) {
            // Synchronous DNS check — performed before the actual TCP connect.
            // Prevents SSRF via DNS rebinding to 127.x addresses.
            if (ResolvesToLoopback(host)) {
                return $"Host '{host}' resolves to a loopback address, which is rejected by HostRules.";
            }
        }

        foreach (string pattern in _rules.BlockedHostPatterns) {
            if (MatchesHostPattern(host, pattern)) {
                return $"Host '{host}' matches the blocked pattern '{pattern}'.";
            }
        }

        return null;
    }

    private static bool IsLoopback(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        if (IPAddress.TryParse(host, out IPAddress? addr)) {
            return IPAddress.IsLoopback(addr);
        }

        return false;
    }

    private static bool IsLocalhost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase);

    private static bool ResolvesToLoopback(string host)
    {
        try {
            IPAddress[] addresses = Dns.GetHostAddresses(host);
            return addresses.Any(IPAddress.IsLoopback);
        } catch {
            return false;
        }
    }

    /// <summary>
    /// Matches a host against a pattern that may use a leading wildcard (e.g. <c>*.reddit.com</c>).
    /// </summary>
    private static bool MatchesHostPattern(string host, string pattern)
    {
        if (pattern.StartsWith("*.", StringComparison.Ordinal)) {
            string suffix = pattern[1..]; // ".reddit.com"
            return host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, suffix[1..], StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(host, pattern, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Streaming helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Copies <paramref name="src"/> to <paramref name="dest"/>, enforcing a per-chunk idle read timeout.
    /// Throws <see cref="OperationCanceledException"/> when the idle window expires.
    /// </summary>
    private static async Task StreamWithIdleTimeoutAsync(Stream src, Stream dest, int idleSeconds, CancellationToken ct)
    {
        byte[] buf = new byte[81_920];
        int read;

        while (true) {
            using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            idleCts.CancelAfter(TimeSpan.FromSeconds(idleSeconds));

            try {
                read = await src.ReadAsync(buf.AsMemory(), idleCts.Token);
            } catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
                throw new OperationCanceledException(ct);
            }

            if (read == 0) {
                break;
            }

            await dest.WriteAsync(buf.AsMemory(0, read), ct);
        }
    }

    // -------------------------------------------------------------------------
    // Filename helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolves the download filename from the Content-Disposition header or the URI path.
    /// Generates a unique fallback name when neither source provides one.
    /// </summary>
    private static string ResolveFileName(Uri uri, HttpContentHeaders headers)
    {
        // Prefer Content-Disposition filename.
        string? cdFilename = headers.ContentDisposition?.FileNameStar
                          ?? headers.ContentDisposition?.FileName;

        if (!string.IsNullOrWhiteSpace(cdFilename)) {
            string clean = SanitizeFileName(cdFilename.Trim('"'));
            if (!string.IsNullOrWhiteSpace(clean)) {
                return clean;
            }
        }

        // Fall back to the last non-empty URI path segment.
        string lastSegment = Path.GetFileName(uri.AbsolutePath.TrimEnd('/'));
        if (!string.IsNullOrWhiteSpace(lastSegment)) {
            string clean = SanitizeFileName(Uri.UnescapeDataString(lastSegment));
            if (!string.IsNullOrWhiteSpace(clean)) {
                return clean;
            }
        }

        return $"download_{Guid.NewGuid():N}.bin";
    }

    private static string SanitizeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    // -------------------------------------------------------------------------
    // KO factory
    // -------------------------------------------------------------------------

    private static ImageRecord_INPUT KoRecord(string url, string reasonCode, string safeMessage)
    {
        ImageRecord_INPUT rec = new();
        rec.InitialFullName = url;
        rec.SourceKind      = ImageSourceKind.RemoteUrl;
        rec.ImportStatus    = ImportStatus.KO;
        rec.KoReasonCode    = reasonCode;
        rec.KoSafeMessage   = safeMessage;
        return rec;
    }
}
