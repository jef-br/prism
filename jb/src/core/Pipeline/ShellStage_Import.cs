/// <summary>
/// Shell delegate for the Imported stage.
/// Receives raw input records; normalizes images, unpacks zips, and parses Excel into the IEM.
/// Real implementation lives in <c>Importer.cs</c> and <c>ZipHandler.cs</c>.
/// </summary>
internal static class ShellStage_Import
{
    /// <summary>
    /// Runs the Imported stage for a job context.
    /// Delegates all normalization, zip extraction, and IEM construction to <see cref="Importer"/>.
    /// KO records are stored in the context and do not stop the batch.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    /// <param name="modelBuilder">Pre-loaded Excel model builder from startup.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration, ModelBuilder modelBuilder)
    {
        Importer importer = new(configuration, modelBuilder);

        string jobTempRoot = Path.Combine(Path.GetTempPath(), "PRISM");
        ImportStageResult importResult = importer.Run(
            context.JobID,
            context.ImageRecords,
            context.ExcelRecords,
            context.ZipFileRecords,
            jobTempRoot);

        context.ImportResult = importResult;
        context.KoRecordCount += importResult.ImageKoRecords.Count + importResult.ZipKoRecords.Count;

        foreach (ExcelProcessingDiagnostic diagnostic in importResult.ExcelDiagnostics.Where(IsExcelKo))
        {
            context.AddWarning($"Excel KO: {diagnostic.ReasonCode} — {diagnostic.Message}");
        }

        context.MarkStageCompleted(PipelineStageNames.Imported);
    }

    /// <summary>
    /// Determines whether an Excel diagnostic represents a KO item.
    /// </summary>
    private static bool IsExcelKo(ExcelProcessingDiagnostic diagnostic)
    {
        return diagnostic.Severity == ExcelDiagnosticSeverity.Error;
    }
}
