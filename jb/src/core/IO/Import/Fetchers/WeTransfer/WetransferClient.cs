using Microsoft.Playwright;

namespace Prism.Core;

/// <summary>
/// Downloads files from WeTransfer, SwissTransfer, and other browser-driven file-sharing services.
/// Uses Playwright to automate cookie consent, optional password, and the download button interaction.
/// </summary>
internal class WetransferClient
{
    private readonly CancellationToken _defaultCt;

    /// <summary>Maximum download size enforced during streaming (10 GB).</summary>
    private const long MaxDownloadBytes = 10L * 1024 * 1024 * 1024;

    private static readonly string[] ConsentLabels =
    {
        // EN, FR, NL, DE ,ES ,IT
        "Accept all", "Accept All", "I accept", "I Accept", "Agree", "I agree", "Allow all",
        "Tout accepter", "Accepter tout", "J'accepte", "Autoriser tout",
        "Accepter les cookies", "Accepter et continuer", "Accepter",
        "Ik ga akkoord", "Akkoord", "Alle akkoord", "Alles akkoord", "Accepteer alles", "Accepteren", "Toestaan",
        "Ich akzeptiere", "Alle akzeptieren", "Akzeptieren", "Zustimmen", "Ich stimme zu", "Einverstanden", "Alle zulassen",
        "Aceptar todo", "Aceptar todos", "Aceptar", "Acepto", "Permitir todo", "De acuerdo",
        "Accetta tutto", "Accetta", "Accetto", "Consento", "Consenti tutto", "Approvo"
    };

    private static readonly string[] DownloadLabels =
    {
        // EN, FR, NL, DE ,ES ,IT
        "Download all files", "Download all", "Download",
        "Télécharger tous les fichiers", "Télécharger tout", "Télécharger", "Tout Télécharger",
        "Alle bestanden downloaden", "Alles downloaden", "Downloaden",
        "Alle Dateien herunterladen", "Alle herunterladen", "Herunterladen",
        "Descargar todos los archivos", "Descargar todo", "Descargar",
        "Scarica tutti i file", "Scarica tutto", "Scarica"
    };

    private static readonly HttpClient _http = new();
    private static readonly bool _isDebugging = System.Diagnostics.Debugger.IsAttached;

    /// <summary>Creates a client with no default cancellation token.</summary>
    public WetransferClient() : this(CancellationToken.None) { }

    /// <summary>
    /// Creates a client with an instance-level cancellation token.
    /// The token is linked into every <c>DownloadAsync</c> call, so cancelling it
    /// aborts any in-progress download regardless of the per-call token.
    /// </summary>
    public WetransferClient(CancellationToken cancellationToken) => _defaultCt = cancellationToken;

    /// <summary>Downloads the file at <paramref name="url"/>.</summary>
    public Task<WeTransferDownloadResult> DownloadAsync(string url) => DownloadAsync(url, null, _defaultCt);

    /// <summary>Downloads the file at <paramref name="url"/>, entering <paramref name="password"/> if prompted.</summary>
    public Task<WeTransferDownloadResult> DownloadAsync(string url, string? password) => DownloadAsync(url, password, _defaultCt);

    /// <summary>
    /// Downloads the file at <paramref name="url"/>, optionally entering a <paramref name="password"/>,
    /// and respects the provided <paramref name="cancellationToken"/> in addition to the instance-level token.
    /// </summary>
    /// <returns>
    /// A <see cref="WeTransferDownloadResult"/> containing the open file stream, file name, and byte count.
    /// The caller must dispose the result when done — this closes the stream and deletes the temp file.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the download button is not found (expired link) or the file exceeds 10 GB.
    /// </exception>
    public async Task<WeTransferDownloadResult> DownloadAsync(string url, string? password, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_defaultCt, cancellationToken);
        var ct = cts.Token;

        string folder = Path.Combine(Path.GetTempPath(), "wetransfer-client", "downloads");
        Directory.CreateDirectory(folder);

        string tempName = $"downloaded_file_{Path.GetFileNameWithoutExtension(Path.GetRandomFileName())}.zip";
        string downloadPath = Path.Combine(folder, tempName);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions {
            // Show the browser window when a debugger is attached so you can watch the interaction
            Headless = !_isDebugging,
            SlowMo = _isDebugging ? 500 : 0
            }
        );

        var browserContext = await browser.NewContextAsync(new() { AcceptDownloads = true });
        var page = await browserContext.NewPageAsync();

        try {
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle });

            //  Phase 1: OneTrust cookie banner 
            // The OneTrust overlay intercepts pointer events on everything below it.
            // We must dismiss it via its stable DOM ID before touching anything else.
            var oneTrustSdk = page.Locator("#onetrust-consent-sdk");
            if (await oneTrustSdk.IsVisibleAsync())
            {
                var oneTrustBtn = page.Locator("#onetrust-accept-btn-handler");
                if (await oneTrustBtn.IsVisibleAsync()) {
                    try {
                        await oneTrustBtn.ClickAsync(new() { Timeout = 5_000 });
                    }
                    catch { }
                }

                try {
                    await oneTrustSdk.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 10_000 });
                } catch { }
            }

            //  Phase 2: provider-specific consent banners 
            async Task<bool> TryClickConsentAsync(string label) {
                try {
                    var buttons = await page.GetByRole(AriaRole.Button, new() { Name = label, Exact = true }).AllAsync();
                    bool any = false;
                    foreach (var b in buttons) {
                        if (!await b.IsVisibleAsync()) continue;
                        any = true;
                        try { await b.ClickAsync(new() { Timeout = 5_000 }); await page.WaitForTimeoutAsync(300); } catch { }
                    }
                    return any;
                } catch {
                    return false;
                }
            }

            // Two passes: a second banner can appear once the first is dismissed
            for (int pass = 1; pass <= 2; pass++) {
                bool clickedThisPass = false;
                foreach (string label in ConsentLabels) {
                    if (await TryClickConsentAsync(label)) {
                        clickedThisPass = true;
                    }
                }

                if (!clickedThisPass) break;

                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }

            ct.ThrowIfCancellationRequested();

            //  Phase 3: optional password prompt 
            // WeTransfer uses HTML placeholder attributes; SwissTransfer uses floating labels.
            if (!string.IsNullOrWhiteSpace(password)) {
                // EN, FR, NL, De, ES, IT
                string[] passwordTexts =
                [
                    "Password", "Enter a password *",
                    "Mot de passe", "Saisir un mot de passe *",
                    "Wachtwoord", "Voer een wachtwoord in",
                    "Passwort", "Passwort eingeben",
                    "Contraseña", "Introduce una contraseña",
                    "Inserisci una password"
                ];
                // EN, FR, NL, De, ES, IT
                string[] continueBtnLabels =
                [
                    "Continue", "Approve",
                    "Continuer", "Valider",
                    "Doorgaan", "Bevestigen",
                    "Weiter", "Bestätigen",
                    "Continuar", "Confirmar",
                    "Continua", "Conferma"
                ];

                ILocator? passwordInput = null;
                foreach (string text in passwordTexts) {
                    var byLabel = page.GetByLabel(text, new() { Exact = true });
                    if (await byLabel.IsVisibleAsync()) {
                        passwordInput = byLabel;
                        break;
                    }

                    var byPlaceholder = page.GetByPlaceholder(text, new() { Exact = true });
                    if (await byPlaceholder.IsVisibleAsync()) {
                        passwordInput = byPlaceholder;
                        break;
                    }
                }

                if (passwordInput is not null) {
                    await passwordInput.FillAsync(password);
                    foreach (string label in continueBtnLabels) {
                        var btn = page.GetByRole(AriaRole.Button, new() { Name = label, Exact = true });
                        if (await btn.IsVisibleAsync()) {
                            await btn.ClickAsync(new() { Timeout = 5_000 });
                            break;
                        }
                    }

                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                }
            }

            //  Phase 4: find and click the download button 
            async Task<ILocator?> FindDownloadButtonAsync() {
                foreach (string label in DownloadLabels) {
                    var btns = await page.GetByRole(AriaRole.Button, new() { Name = label, Exact = true }).AllAsync();
                    if (btns.Count == 0) {
                        btns = await page.GetByRole(AriaRole.Link, new() { Name = label, Exact = true }).AllAsync();
                    }

                    foreach (var b in btns) {
                        if (await b.IsVisibleAsync()) {
                            return b;
                        }
                    }
                }

                return null;
            }

            var downloadBtn = await FindDownloadButtonAsync();
            if (downloadBtn is null) {
                string screenshotPath = Path.Combine(Path.GetTempPath(), "wetransfer-client", $"debug_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
                await page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });
                throw new InvalidOperationException($"Download button not found — the link may have expired or the page layout has changed. Page screenshot saved to: {screenshotPath}");
            }

            var download = await page.RunAndWaitForDownloadAsync( async () => {
                    try {
                        await downloadBtn.ClickAsync(new() { Timeout = 10_000 });
                    } catch { }
                },
                new PageRunAndWaitForDownloadOptions { Timeout = 60_000 }
            );

            string resolvedFileName = download.SuggestedFilename is { Length: > 0 } s ? s : tempName;
            long? totalBytes = await TryGetContentLengthAsync(download.Url, ct);

            if (totalBytes.HasValue && totalBytes.Value > MaxDownloadBytes){
                throw new InvalidOperationException($"File too large: {totalBytes.Value / (1024d * 1024d * 1024d):0.##} GB. Limit is 10 GB.");
            }

            {
                await using var source = await download.CreateReadStreamAsync();
                await using var target = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
                byte[] buffer = new byte[81920];
                long totalRead = 0;
                int read;
                while ((read = await source.ReadAsync(buffer.AsMemory(), ct)) > 0) {
                    await target.WriteAsync(buffer.AsMemory(0, read), ct);
                    totalRead += read;
                    if (totalRead > MaxDownloadBytes) {
                        throw new InvalidOperationException("File exceeds the 10 GB limit, download aborted.");
                    }
                }
            }

            var fileStream = new FileStream(downloadPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
            return new WeTransferDownloadResult(fileStream, resolvedFileName, totalBytes, downloadPath);
        } catch {
            if (File.Exists(downloadPath)) {
                File.Delete(downloadPath);
            }

            throw;
        } finally {
            await page.CloseAsync();
        }
    }

    private static async Task<long?> TryGetContentLengthAsync(string url, CancellationToken ct) {
        try {
            // First attempt: HEAD request (fast, no body download)
            using var headReq = new HttpRequestMessage(HttpMethod.Head, url);
            using var headRes = await _http.SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead, ct);
            if (headRes.IsSuccessStatusCode && headRes.Content.Headers.ContentLength.HasValue) {
                return headRes.Content.Headers.ContentLength.Value;
            }

            // Fallback: GET with Range: bytes=0-0 — some CDN pre-signed URLs reject HEAD
            // but honour range-GET; the 206 Content-Range header carries the full file size.
            using var rangeReq = new HttpRequestMessage(HttpMethod.Get, url);
            rangeReq.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
            using var rangeRes = await _http.SendAsync(rangeReq, HttpCompletionOption.ResponseHeadersRead, ct);
            if (rangeRes.StatusCode == System.Net.HttpStatusCode.PartialContent && rangeRes.Content.Headers.ContentRange?.Length.HasValue == true) {
                return rangeRes.Content.Headers.ContentRange.Length!.Value;
            }
        } catch { }
        return null;
    }
}
