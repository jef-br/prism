namespace Prism.Core;

/// <summary>
/// Routes a remote URL to the first <see cref="IFetchStrategy"/> that can handle it
/// and delegates the download. Priority order: Fetch_DropBox → Fetch_WeTransfer → Fetch_HTTPS_DirectFile.
/// </summary>
public sealed class FetchDispatcher
{
    private readonly IReadOnlyList<IFetchStrategy> _strategies;

    internal FetchDispatcher(IReadOnlyList<IFetchStrategy> strategies) => _strategies = strategies;

    /// <summary>
    /// Creates a dispatcher backed by all registered fetch strategies, using HostRules.json
    /// from the configured config directory discovered via <see cref="PrismConfigLocator"/>.
    /// </summary>
    public static FetchDispatcher Create()
    {
        string? hostRulesPath = PrismConfigLocator.FindFolderLocalConfig("HostRules.json");
        if (hostRulesPath is null)
            throw new PrismConfigurationException(
                "HostRules.json not found — cannot initialise fetch strategies.");

        string configDirectory = Path.GetDirectoryName(hostRulesPath)!;
        return Create(configDirectory);
    }

    /// <summary>
    /// Creates a dispatcher from an explicit config directory.
    /// Specialist fetchers MUST come before <see cref="Fetch_HTTPS_DirectFile"/>;
    /// the generic HTTPS fetcher claims any http/https URL and must be last.
    /// </summary>
    public static FetchDispatcher Create(string configDirectory) => new([
        Fetch_DropBox.Create(configDirectory),
        Fetch_WeTransfer.Create(configDirectory),
        Fetch_HTTPS_DirectFile.Create(configDirectory)   // fallback — must be last
    ]);

    /// <summary>Returns true when at least one strategy can handle the given URL.</summary>
    public bool CanHandle(string url) => _strategies.Any(s => s.CanHandle(url));

    /// <summary>Fetches the URL using the first matching strategy.</summary>
    public Task<ImageRecord_INPUT> FetchAsync(
        string url, string jobTempFolder, string jobID, CancellationToken ct) =>
        _strategies.First(s => s.CanHandle(url)).FetchAsync(url, jobTempFolder, jobID, ct);
}
