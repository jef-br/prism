/// <summary>
/// Owns pipeline processing and disposal of per-job resources.
/// Enforces the immutable stage order: Imported → Classified → Matched → Ordered → Renamed → Generated → Transformed → Exported.
/// Each stage delegates to its dedicated class; Pipeline wires boundaries and emits progress events.
/// </summary>
internal sealed class Pipeline
{
    private readonly PrismConfiguration configuration;

    /// <summary>
    /// Creates a Pipeline with its required validated configuration.
    /// </summary>
    /// <param name="configuration">Validated PRISM configuration loaded at startup.</param>
    internal Pipeline(PrismConfiguration configuration)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    // -------------------------------------------------------------------------
    // Stage name constants — single source of truth for the immutable stage order.
    // -------------------------------------------------------------------------

    /// <summary>Definitive stage names in immutable order.</summary>
    internal static readonly string[] StageOrder =
    [
        PipelineStageNames.Imported,
        PipelineStageNames.Classified,
        PipelineStageNames.Matched,
        PipelineStageNames.Ordered,
        PipelineStageNames.Renamed,
        PipelineStageNames.Generated,
        PipelineStageNames.Transformed,
        PipelineStageNames.Exported
    ];

    // -------------------------------------------------------------------------
    // Entry point
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs a validated job request through all eight pipeline stages in immutable order.
    /// Returns a structured result after the Exported stage completes.
    /// </summary>
    /// <param name="request">The validated job request.</param>
    /// <param name="progress">Optional progress callback invoked at each stage boundary.</param>
    /// <param name="cancellationToken">Host shutdown token — does not cancel user jobs.</param>
    /// <returns>Structured pipeline result.</returns>
    internal async Task<PipelineResult> RunAsync(
        PrismJobRequest request,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
    {
        PipelineContext context = InitializeContext(request);

        try
        {
            await RunStage(context, PipelineStageNames.Imported,    ImportStageShell.Run,    progress, cancellationToken);
            await RunStage(context, PipelineStageNames.Classified,  ClassifyStageShell.Run,  progress, cancellationToken);
            await RunStage(context, PipelineStageNames.Matched,     MatchStageShell.Run,     progress, cancellationToken);
            await RunStage(context, PipelineStageNames.Ordered,     OrderStageShell.Run,     progress, cancellationToken);
            await RunStage(context, PipelineStageNames.Renamed,     RenameStageShell.Run,    progress, cancellationToken);
            await RunStage(context, PipelineStageNames.Generated,   GenerateStageShell.Run,  progress, cancellationToken);
            await RunStage(context, PipelineStageNames.Transformed, TransformStageShell.Run, progress, cancellationToken);
            await RunStage(context, PipelineStageNames.Exported,    ExportStageShell.Run,    progress, cancellationToken);

            return BuildSuccessResult(context, request);
        }
        catch (Exception exception) when (exception is not PrismConfigurationException)
        {
            return BuildFailedResult(context, request, exception);
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    // -------------------------------------------------------------------------
    // Stage runner — emits a progress event then delegates to the stage's shell.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Emits the stage-start progress event, then runs the stage's shell delegate.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="stageName">Definitive stage name for this step.</param>
    /// <param name="runShell">The stage's shell entry point.</param>
    /// <param name="progress">Optional progress callback.</param>
    /// <param name="cancellationToken">Host shutdown token.</param>
    private async Task RunStage(
        PipelineContext context,
        string stageName,
        Action<PipelineContext, PrismConfiguration> runShell,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
    {
        await EmitStageProgress(context.JobID, stageName, progress, cancellationToken);
        runShell(context, configuration);
    }

    // -------------------------------------------------------------------------
    // Context lifecycle
    // -------------------------------------------------------------------------

    private static PipelineContext InitializeContext(PrismJobRequest request)
    {
        return new PipelineContext(
            request.JobID,
            request.ImageRecords,
            request.ExcelRecords,
            request.ZipFileRecords,
            request.PrismProcessingParameters!,
            DateTimeOffset.UtcNow);
    }

    private static void ReleaseContext(PipelineContext context)
    {
        context.Dispose();
    }

    // -------------------------------------------------------------------------
    // Result builders
    // -------------------------------------------------------------------------

    private static PipelineResult BuildSuccessResult(PipelineContext context, PrismJobRequest request)
    {
        string outputFormat = request.PrismProcessingParameters?.Format ?? "json";

        BatchManifest manifest = new()
        {
            JobID = context.JobID,
            Summary = new BatchManifestSummary
            {
                ImageCount = context.ImageRecords.Count,
                ExcelCount = context.ExcelRecords.Count,
                ZipCount   = context.ZipFileRecords.Count,
                OkRenamed  = context.OkRenamedCount,
                KoRecords  = context.KoRecordCount
            },
            RouteSummaries = StageOrder.Select(stage => $"{stage}: completed.").ToArray(),
            Warnings       = context.Warnings
        };

        return new PipelineResult("Completed", outputFormat, manifest, null, context.Warnings);
    }

    private static PipelineResult BuildFailedResult(PipelineContext context, PrismJobRequest request, Exception exception)
    {
        string outputFormat = request.PrismProcessingParameters?.Format ?? "json";

        BatchManifest manifest = new()
        {
            JobID    = context.JobID,
            Summary  = new BatchManifestSummary
            {
                ImageCount = context.ImageRecords.Count,
                ExcelCount = context.ExcelRecords.Count,
                ZipCount   = context.ZipFileRecords.Count,
                OkRenamed  = 0,
                KoRecords  = context.ImageRecords.Count
            },
            RouteSummaries = context.CompletedStages
                .Select(stage => $"{stage}: completed.")
                .Concat([$"Pipeline failed: {exception.Message}"])
                .ToArray(),
            Warnings = context.Warnings
        };

        return new PipelineResult("Failed", outputFormat, manifest, exception.Message, context.Warnings);
    }

    // -------------------------------------------------------------------------
    // Progress helper
    // -------------------------------------------------------------------------

    private static async Task EmitStageProgress(
        Guid jobID,
        string stageName,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
    {
        if (progress is null)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        await progress(new PipelineProgressEvent
        {
            JobID       = jobID,
            Stage       = stageName,
            Severity    = "Information",
            SafeMessage = $"Stage {stageName} started.",
            Timestamp   = DateTimeOffset.UtcNow
        });
    }
}

// =============================================================================
// Supporting types
// =============================================================================

/// <summary>
/// Definitive stage name constants. Matches the immutable pipeline stage order exactly.
/// </summary>
internal static class PipelineStageNames
{
    internal const string Imported   = "Imported";
    internal const string Classified = "Classified";
    internal const string Matched    = "Matched";
    internal const string Ordered    = "Ordered";
    internal const string Renamed    = "Renamed";
    internal const string Generated  = "Generated";
    internal const string Transformed = "Transformed";
    internal const string Exported   = "Exported";
}

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

    // -------------------------------------------------------------------------
    // Imported stage outputs — set by ImportStageShell after Run() completes.
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

/// <summary>
/// Structured result returned by <see cref="Pipeline.RunAsync"/> to the Prism facade.
/// </summary>
internal sealed record PipelineResult(
    string Status,
    string OutputFormat,
    BatchManifest Manifest,
    string? FailureReason,
    IReadOnlyList<string> Warnings);
