namespace Prism.Core;

/// <summary>
/// In-process Ingest implementation. Wraps <see cref="Importer"/>: normalizes images, unpacks ZIPs, and
/// builds FamilyRecords from Excel, writing every artifact under the local job folder owned by the
/// <see cref="IArtifactStore"/>. Emits the Imported stage event.
/// </summary>
public sealed class IngestService : IIngestService
{
    private readonly PrismConfiguration configuration;
    private readonly ModelBuilder modelBuilder;

    /// <summary>Creates the service with the validated configuration and pre-loaded Excel model builder.</summary>
    public IngestService(PrismConfiguration configuration, ModelBuilder modelBuilder)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.modelBuilder  = modelBuilder  ?? throw new ArgumentNullException(nameof(modelBuilder));
    }

    /// <inheritdoc/>
    public async Task<IngestResult> ImportAsync(
        PrismJobRequest request,
        IArtifactStore store,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
    {
        await StageProgress.EmitStarted(progress, request.JobID, PipelineStageNames.Imported, cancellationToken);

        Importer importer = new(configuration, modelBuilder);
        ImportStageResult import = importer.Run(
            request.JobID,
            request.ImageRecords,
            request.ExcelRecords,
            request.ZipFileRecords,
            store.JobTempRoot);

        IReadOnlyList<string> warnings = import.ExcelDiagnostics
            .Where(d => d.Severity == ExcelDiagnosticSeverity.Error)
            .Select(d => $"Excel KO: {d.ReasonCode} — {d.Message}")
            .ToList();

        int koRecordCount = import.ImageKoRecords.Count + import.ZipKoRecords.Count;

        await StageProgress.EmitCompleted(progress, request.JobID, PipelineStageNames.Imported, import.NormalizedImages.Count, koRecordCount, cancellationToken);

        return new IngestResult
        {
            JobID              = request.JobID,
            Parameters         = request.PrismProcessingParameters!,
            NormalizedImages   = import.NormalizedImages,
            FamilyRecords      = import.FamilyRecords,
            JobTempFolder      = import.JobTempFolder,
            OriginalImageCount = request.ImageRecords.Count,
            OriginalExcelCount = request.ExcelRecords.Count,
            OriginalZipCount   = request.ZipFileRecords.Count,
            FirstExcelTempPath = request.ExcelRecords.Count > 0 ? request.ExcelRecords[0].TempFilePath : null,
            KoRecordCount      = koRecordCount,
            Warnings           = warnings
        };
    }
}
