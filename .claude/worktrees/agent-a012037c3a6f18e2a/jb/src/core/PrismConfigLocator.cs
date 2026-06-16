/// <summary>
/// Locates PRISM configuration files on disk using standard search paths.
/// </summary>
internal static class PrismConfigLocator
{
    /// <summary>
    /// Finds the absolute path to Prism_Config.json by searching standard locations.
    /// Returns <c>null</c> if the file is not found in any candidate location.
    /// </summary>
    /// <returns>Absolute path to Prism_Config.json, or <c>null</c>.</returns>
    internal static string? FindPrismConfigPath()
    {
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "Prism_Config.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Prism_Config.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "core", "Prism_Config.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "jb", "src", "core", "Prism_Config.json")
        ];

        return Array.Find(candidates, File.Exists);
    }

    /// <summary>
    /// Resolves the absolute path to a folder-local config file relative to the core root.
    /// Returns <c>null</c> if the file is not found.
    /// </summary>
    /// <param name="relativePathFromCore">Relative path from the core config root, e.g. "Excel/ExcelConfig.json".</param>
    /// <returns>Absolute path to the folder-local config, or <c>null</c>.</returns>
    internal static string? FindFolderLocalConfig(string relativePathFromCore)
    {
        string prismConfigPath = FindPrismConfigPath() ?? string.Empty;

        if (string.IsNullOrEmpty(prismConfigPath))
        {
            return null;
        }

        string coreDirectory = Path.GetDirectoryName(prismConfigPath) ?? string.Empty;

        if (string.IsNullOrEmpty(coreDirectory))
        {
            return null;
        }

        string candidate = Path.Combine(coreDirectory, relativePathFromCore);
        return File.Exists(candidate) ? candidate : null;
    }
}
