namespace Prism.Lib.Ingress;

/// <summary>Fetches a file from a WeTransfer share link using a headless browser.</summary>
internal sealed class Fetch_WeTransfer : IFetchStrategy {
    private static readonly string[] _hostPatterns = ["wetransfer.com", "we.tl"];
    private readonly HostRules_Config _rules;
    private Fetch_WeTransfer(HostRules_Config rules) => this._rules = rules;
    public static IFetchStrategy Create(string configDirectory) => new Fetch_WeTransfer(HostRules_Config.Load(configDirectory));
    public bool CanHandle(string url) => _hostPatterns.Any(h => url.Contains(h, StringComparison.OrdinalIgnoreCase));

    public async Task<ImageRecord_INPUT> FetchAsync(string url, string jobTempFolder, string jobID, CancellationToken cancellationToken) {
        var client = new WetransferClient(this._rules.WeTransferPolling, cancellationToken);
        await using var result = await client.DownloadAsync(url, password: null, cancellationToken);
        string destPath = Path.Combine(jobTempFolder, result.FileName);
        await using var destStream = File.OpenWrite(destPath);
        await result.Content.CopyToAsync(destStream, cancellationToken);
        return new ImageRecord_INPUT {
            InitialFullName = result.FileName,
            TempFilePath = destPath,
            SourceKind = ImageSourceKind.RemoteUrl
        };
    }
}
