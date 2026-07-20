namespace Prism.Lib.Ingress;

/// <summary>
/// Smoke test for the Imported stage. Exercises <see cref="Importer"/> against a local
/// image folder and Excel file, then prints a result summary.
/// Designed to be invoked from a console runner or test harness once fixtures are available.
/// </summary>
public static class ImportSmokeTest
{
    /// <summary>
    /// Runs the Imported stage smoke test against a local image directory and Excel file.
    /// Does not throw on import KO items — those appear in the result summary.
    /// Throws when the Importer itself fails to initialize or the config is invalid.
    /// </summary>
    /// <param name="imageFolder">
    /// Absolute path to a folder containing image files.
    /// Example: <c>test/datasets/CiMini/</c>
    /// </param>
    /// <param name="excelFilePath">
    /// Absolute path to an Excel workbook.
    /// Example: <c>test/datasets/CiMini/ci-mini.xlsx</c>
    /// </param>
    /// <param name="configPath">
    /// Absolute path to <c>Prism_Config.json</c>. Pass null to use auto-detection.
    /// </param>
    /// <returns>A human-readable result summary.</returns>
    public static ImportSmokeTestResult Run(
        string imageFolder,
        string excelFilePath,
        string? configPath = null)
    {
        // Validate fixture paths first.
        if (!Directory.Exists(imageFolder))
        {
            return ImportSmokeTestResult.Blocked(
                $"Image folder not found: {imageFolder}");
        }

        if (!File.Exists(excelFilePath))
        {
            return ImportSmokeTestResult.Blocked(
                $"Excel file not found: {excelFilePath}");
        }

        // Load configuration.
        string resolvedConfigPath = ResolveConfigPath(configPath);
        PrismConfiguration configuration = PrismConfiguration.LoadPrismConfig(resolvedConfigPath);

        string excelConfigPath = Path.Combine(
            Path.GetDirectoryName(resolvedConfigPath)!,
            "Excel",
            "ExcelConfig.json");
        ModelBuilder modelBuilder = ModelBuilder.FromConfigFile(excelConfigPath);
        Importer importer = new(configuration, modelBuilder);

        // Build input records from the fixture image folder.
        List<ImageRecord_INPUT> imageRecords = BuildImageRecordsFromFolder(imageFolder);
        List<InputExcelFileRecord> excelRecords =
        [
            new InputExcelFileRecord
            {
                SourceReference = excelFilePath,
                TempFilePath    = excelFilePath
            }
        ];

        // Run the stage.
        Guid jobID      = Guid.NewGuid();
        string tempRoot = Path.Combine(Path.GetTempPath(), "PRISM_SmokeTest");
        ImportStageResult result = importer.Run(
            jobID,
            imageRecords,
            excelRecords,
            zipRecords: [],
            jobTempRoot: tempRoot);

        return ImportSmokeTestResult.Completed(result);
    }

    /// <summary>
    /// Scans a folder for image files and builds <see cref="ImageRecord_INPUT"/> records
    /// pointing to each file.
    /// </summary>
    private static List<ImageRecord_INPUT> BuildImageRecordsFromFolder(string imageFolder)
    {
        string[] imageExtensions = [".jpg", ".jpeg", ".png", ".tif", ".tiff", ".pdf", ".webp", ".bmp", ".gif"];

        return Directory
            .EnumerateFiles(imageFolder, "*", SearchOption.AllDirectories)
            .Where(file => imageExtensions.Contains(
                Path.GetExtension(file),
                StringComparer.OrdinalIgnoreCase))
            .Select(file => new ImageRecord_INPUT
            {
                // InitialFullName is a full local path for direct-file smoke test invocation.
                // The Importer resolves this as the readable source path.
                InitialFullName = file,
                SourceKind      = ImageSourceKind.LocalPath
            })
            .ToList();
    }

    /// <summary>
    /// Resolves the config path, falling back to auto-detection when null.
    /// </summary>
    private static string ResolveConfigPath(string? configPath)
    {
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            return configPath;
        }

        return ConfigLoader.RequireFile(PrismConfiguration.FileName);
    }
}
