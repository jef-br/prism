/// <summary>
/// Mutable per-job state threaded through all pipeline stages.
/// Disposed by <see cref="Pipeline"/> after the Exported stage or on failure.
/// </summary>
internal sealed class PipelineContext : IDisposable
{
    private readonly List<string> completedStages = [];
    private readonly List<string> warnings = [];
    private bool disposed;

    internal PipelineContext(
        Guid jobID,
        IReadOnlyList<ImageRecord_INPUT> imageRecords,
        IReadOnlyList<InputExcelFileRecord> excelRecords,
        IReadOnlyList<InputZipFileRecord> zipFileRecords,
        PrismProcessingParameters parameters,
        DateTimeOffset startedAt)
    {
        JobID          = jobID;
        ImageRecords   = imageRecords;
        ExcelRecords   = excelRecords;
        ZipFileRecords = zipFileRecords;
        Parameters     = parameters;
        StartedAt      = startedAt;
    }

    /// <summary>PRISM-owned job identifier.</summary>
    internal Guid JobID { get; }

    /// <summary>Accepted image input records.</summary>
    internal IReadOnlyList<ImageRecord_INPUT> ImageRecords { get; }

    /// <summary>Accepted Excel input records.</summary>
    internal IReadOnlyList<InputExcelFileRecord> ExcelRecords { get; }

    /// <summary>Accepted zip input records.</summary>
    internal IReadOnlyList<InputZipFileRecord> ZipFileRecords { get; }

    /// <summary>Caller-supplied processing parameters.</summary>
    internal PrismProcessingParameters Parameters { get; }

    /// <summary>UTC time the pipeline context was created.</summary>
    internal DateTimeOffset StartedAt { get; }

    /// <summary>Number of images successfully renamed by the Renamed stage.</summary>
    internal int OkRenamedCount { get; set; }

    /// <summary>Number of KO records accumulated across all stages.</summary>
    internal int KoRecordCount { get; set; }

    /// <summary>Classified image records produced by the Classified stage.</summary>
    internal List<ImageRecord_LAMBDA> LambdaRecords { get; } = [];

    /// <summary>Number of visual duplicate images suppressed by the Classified stage.</summary>
    internal int DuplicatesRemoved { get; set; }

    /// <summary>Number of images successfully assigned a phenotype by the Classified stage.</summary>
    internal int PhenotypeAssignedCount { get; set; }

    /// <summary>Generated image records produced by the Generated stage.</summary>
    internal List<ImageRecord_GENERATED> GeneratedRecords { get; } = [];

    /// <summary>Number of generated records created by the Generated stage.</summary>
    internal int GeneratedCount { get; set; }

    /// <summary>Number of non-KO images that received a transform decision in the Transformed stage.</summary>
    internal int OkTransformedCount { get; set; }

    // -------------------------------------------------------------------------
    // Exported stage outputs — set by ShellStage_Export after Run() completes.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Result produced by the Exported stage.
    /// Carries the ZIP bytes (when format is "zip") and the fully-populated manifest.
    /// Null until the Exported stage completes.
    /// </summary>
    internal ExportStageResult? ExportResult { get; set; }

    // -------------------------------------------------------------------------
    // Imported stage outputs — set by ShellStage_Import after Run() completes.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Normalized image records produced by the Imported stage.
    /// Replaces <see cref="ImageRecords"/> as the working image collection for all
    /// downstream stages (Classified through Exported).
    /// Null until the Imported stage completes.
    /// </summary>
    internal ImportStageResult? ImportResult { get; set; }

    // -------------------------------------------------------------------------
    // Accessors used by downstream stages
    // -------------------------------------------------------------------------

    /// <summary>
    /// Normalized images available after the Imported stage.
    /// Returns the raw pre-import list when the import stage has not run yet.
    /// </summary>
    internal IReadOnlyList<ImageRecord_INPUT> NormalizedImages =>
        ImportResult?.NormalizedImages ?? ImageRecords;

    /// <summary>
    /// Family records built from the IEM during the Imported stage.
    /// Empty until the Imported stage completes.
    /// </summary>
    internal IReadOnlyList<FamilyRecord> FamilyRecords =>
        ImportResult?.FamilyRecords ?? [];

    // -------------------------------------------------------------------------
    // Stage tracking
    // -------------------------------------------------------------------------

    /// <summary>Stages completed before the current point.</summary>
    internal IReadOnlyList<string> CompletedStages => completedStages;

    /// <summary>Safe warnings accumulated across all stages.</summary>
    internal IReadOnlyList<string> Warnings => warnings;

    /// <summary>Records that this stage has completed successfully.</summary>
    internal void MarkStageCompleted(string stageName) => completedStages.Add(stageName);

    /// <summary>Appends a safe warning from any stage.</summary>
    internal void AddWarning(string warning) => warnings.Add(warning);

    /// <summary>
    /// Releases any unmanaged resources held by per-job stage classes.
    /// Stage agents attach disposables here; Pipeline calls Dispose in finally.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
    }
}
