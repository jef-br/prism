namespace Prism.Core;

/// <summary>
/// Locates PRISM configuration files on disk using standard search paths.
/// </summary>
public static class PrismConfigLocator
{
    /// <summary>
    /// Finds the absolute path to Prism_Config.json by searching standard locations.
    /// Returns <c>null</c> if the file is not found in any candidate location.
    /// </summary>
    /// <returns>Absolute path to Prism_Config.json, or <c>null</c>.</returns>
    public static string? FindPrismConfigPath()
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
        if (string.IsNullOrEmpty(prismConfigPath))return null;

        string coreDirectory = Path.GetDirectoryName(prismConfigPath) ?? string.Empty;

        if (string.IsNullOrEmpty(coreDirectory)) return null;

        string candidate = Path.Combine(coreDirectory, relativePathFromCore);
        return File.Exists(candidate) ? candidate : null;
    }

    /// <summary>
    /// Resolves a large model asset (e.g. the CLIP ONNX model) that is deliberately not copied into every
    /// build output. Resolution order:
    /// <list type="number">
    /// <item>Beside Prism_Config.json — a production deployment ships the model with the config.</item>
    /// <item><c>PRISM_ONNX_MODEL_DIR</c> environment override, joined with the asset's relative path.</item>
    /// <item>The single source-tree copy under <c>jb/src/core/</c>, found by walking up from the binary —
    /// a dev/test convenience so the 146 MB model is never duplicated per project or per test run.</item>
    /// </list>
    /// Returns <c>null</c> when the asset cannot be found in any location.
    /// </summary>
    /// <param name="relativePathFromCore">Relative path from the core root, e.g. "Images/Classify/ONNX/clip-vit-b32-uint8/model_uint8.onnx".</param>
    internal static string? FindModelAsset(string relativePathFromCore)
    {
        string? besideConfig = FindFolderLocalConfig(relativePathFromCore);
        if (besideConfig is not null) return besideConfig;

        string? modelRoot = Environment.GetEnvironmentVariable("PRISM_ONNX_MODEL_DIR");
        if (!string.IsNullOrWhiteSpace(modelRoot))
        {
            string overridden = Path.Combine(modelRoot, relativePathFromCore);
            if (File.Exists(overridden)) return overridden;
        }

        return FindInSourceTree(relativePathFromCore);
    }

    /// <summary>
    /// Walks up from the running binary looking for <c>{ancestor}/jb/src/core/{relativePathFromCore}</c>,
    /// the canonical single copy of source-tracked assets. Returns the first match, or <c>null</c>.
    /// </summary>
    private static string? FindInSourceTree(string relativePathFromCore)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "jb", "src", "core", relativePathFromCore);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }
}
