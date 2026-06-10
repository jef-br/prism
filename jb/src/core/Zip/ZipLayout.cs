using System.IO;

/// <summary>
/// Defines the fixed PRISM zip output layout.
/// </summary>
public static class ZipLayout
{
    /// <summary>
    /// Folder name for exportable OK image artifacts.
    /// </summary>
    public const string OkFolderName = "OK";

    /// <summary>
    /// Folder name for KO image artifacts that were importable before later pipeline failure.
    /// </summary>
    public const string KoFolderName = "KO";

    /// <summary>
    /// File name for the canonical manifest projection.
    /// </summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>
    /// Builds the OK folder path below an output root.
    /// </summary>
    /// <param name="outputRootPath">Root folder used to assemble zip output.</param>
    /// <returns>The fixed OK folder path.</returns>
    public static string BuildOkFolderPath(string outputRootPath)
    {
        return Path.Combine(outputRootPath, OkFolderName);
    }

    /// <summary>
    /// Builds the KO folder path below an output root.
    /// </summary>
    /// <param name="outputRootPath">Root folder used to assemble zip output.</param>
    /// <returns>The fixed KO folder path.</returns>
    public static string BuildKoFolderPath(string outputRootPath)
    {
        return Path.Combine(outputRootPath, KoFolderName);
    }

    /// <summary>
    /// Builds the manifest path below an output root.
    /// </summary>
    /// <param name="outputRootPath">Root folder used to assemble zip output.</param>
    /// <returns>The fixed manifest file path.</returns>
    public static string BuildManifestPath(string outputRootPath)
    {
        return Path.Combine(outputRootPath, ManifestFileName);
    }
}
