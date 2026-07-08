namespace Prism.Lib.Ingress;

/// <summary>
/// Normalizes a public Dropbox share link to a direct-download URL and delegates to
/// <see cref="Fetch_HTTPS_DirectFile"/>. Public-only scope: OAuth-authenticated private
/// links are out of scope (V1 decision, 2026-06).
/// </summary>
internal sealed class Fetch_DropBox : IFetchStrategy
{
    private static readonly string[] _dropboxHosts =
        ["dropbox.com", "www.dropbox.com", "dl.dropboxusercontent.com"];

    private readonly IFetchStrategy _directFile;

    internal Fetch_DropBox(IFetchStrategy directFile) => _directFile = directFile;

    public static IFetchStrategy Create(string configDirectory) =>
        new Fetch_DropBox(Fetch_HTTPS_DirectFile.CreateForDelegate(configDirectory));

    /// <summary>Returns true for any URL whose host is a known Dropbox domain.</summary>
    public bool CanHandle(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
        && _dropboxHosts.Any(h => string.Equals(uri.Host, h, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Normalizes the share link to a direct-download URL (dl=1) and delegates the
    /// actual download to <see cref="Fetch_HTTPS_DirectFile"/>.
    /// </summary>
    public Task<ImageRecord_INPUT> FetchAsync(string url, string jobTempFolder, string jobID, CancellationToken cancellationToken)
    {
        string normalized = NormalizeUrl(url);
        return _directFile.FetchAsync(normalized, jobTempFolder, jobID, cancellationToken);
    }

    // -------------------------------------------------------------------------
    // URL normalization
    // -------------------------------------------------------------------------

    /// <summary>
    /// Converts a Dropbox share URL to a direct-download URL by setting dl=1 in the query string.
    /// dl.dropboxusercontent.com URLs are already direct downloads and pass through unchanged.
    /// </summary>
    private static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            return url;

        // dl.dropboxusercontent.com is already a direct download link — no rewrite needed.
        if (string.Equals(uri.Host, "dl.dropboxusercontent.com", StringComparison.OrdinalIgnoreCase))
            return url;

        // Rewrite or add dl=1 in the query string without taking a System.Web dependency.
        string qs = uri.Query.TrimStart('?');
        string[] parts = qs.Length == 0 ? [] : qs.Split('&');

        bool hasDl = false;
        List<string> rewritten = new(parts.Length + 1);

        foreach (string part in parts) {
            if (part.StartsWith("dl=", StringComparison.OrdinalIgnoreCase)) {
                rewritten.Add("dl=1");
                hasDl = true;
            } else {
                rewritten.Add(part);
            }
        }

        if (!hasDl) rewritten.Add("dl=1");

        UriBuilder builder = new(uri) { Query = string.Join("&", rewritten) };
        return builder.Uri.AbsoluteUri;
    }
}
