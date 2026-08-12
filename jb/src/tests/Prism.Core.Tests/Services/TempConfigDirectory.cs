namespace PrismCoreTests.Services;

/// <summary>
/// A throwaway copy of the config files <see cref="PrismConfiguration.LoadPrismConfig"/> reads, so a test
/// can mutate <c>Prism_Config.json</c> without touching the shipped one. Only the five files the loader
/// actually opens are copied — the deployed config folder is shared with <c>ConfigLoaderTests</c>, which
/// creates and deletes transient <c>probe_*.json</c> files there, so a <c>*.json</c> glob races against it
/// and intermittently copies a file that no longer exists.
/// </summary>
internal static class TempConfigDirectory {
    // Prism_Config.json plus the four files ImageNgpValidator cross-checks at load time.
    private static readonly string[] RequiredFiles = [
        PrismConfiguration.FileName,
        "ImageNGP.json",
        "ImageRoles.json",
        "DetOrderRules.json",
        "ClipPrompts.json"
    ];

    /// <summary>Copies the loader's config files into <paramref name="targetDirectory"/> and returns the
    /// path of the copied <c>Prism_Config.json</c>.</summary>
    internal static string Create(string targetDirectory) {
        string sourceDirectory = Path.GetDirectoryName(ConfigLoader.RequireFile(PrismConfiguration.FileName))!;
        Directory.CreateDirectory(targetDirectory);

        foreach (string fileName in RequiredFiles)
            File.Copy(Path.Combine(sourceDirectory, fileName), Path.Combine(targetDirectory, fileName), overwrite: true);

        return Path.Combine(targetDirectory, PrismConfiguration.FileName);
    }
}
