/// <summary>
/// Owns pipeline processing and disposal of per-job resources.
/// Enforces the immutable stage order: Imported → Classified → Matched → Ordered → Renamed → Generated → Transformed → Exported.
/// Each stage delegates to its dedicated class; Pipeline wires boundaries and emits progress events.
/// </summary>
internal sealed class Pipeline
{
    private readonly PrismConfiguration configuration;
    private readonly ModelBuilder modelBuilder;

    /// <summary>
    /// Creates a Pipeline with its required validated configuration and pre-loaded Excel model builder.
    /// </summary>
    /// <param name="configuration">Validated PRISM configuration loaded at startup.</param>
    /// <param name="modelBuilder">Pre-loaded Excel model builder from ExcelConfig.json.</param>
    internal Pipeline(PrismConfiguration configuration, ModelBuilder modelBuilder)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.modelBuilder  = modelBuilder  ?? throw new ArgumentNullException(nameof(modelBuilder));
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
            await RunStage(context, PipelineStageNames.Imported,    (ctx, cfg) => ShellStage_Import.Run(ctx, cfg, modelBuilder), progress, cancellationToken);
            await RunStage(context, PipelineStageNames.Classified,  ShellStage_Classify.Run,  progress, cancellationToken);
            await RunStage(context, PipelineStageNames.Matched,     ShellStage_Match.Run,     progress, cancellationToken);
            await RunStage(context, PipelineStageNames.Ordered,     ShellStage_Order.Run,     progress, cancellationToken);
            await RunStage(context, PipelineStageNames.Renamed,     ShellStage_Rename.Run,    progress, cancellationToken);
            await RunStage(context, PipelineStageNames.Generated,   ShellStage_Generate.Run,  progress, cancellationToken);
            await RunStage(context, PipelineStageNames.Transformed, ShellStage_Transform.Run, progress, cancellationToken);
            await RunStage(context, PipelineStageNames.Exported,    ShellStage_Export.Run,    progress, cancellationToken);

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

        // Exporter builds the canonical manifest during the Exported stage — reuse it to avoid rebuilding.
        BatchManifest manifest = context.ExportResult?.FinalManifest ?? new BatchManifest
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

        return new PipelineResult("Completed", outputFormat, manifest, null, context.Warnings, context.ExportResult?.ZipBytes);
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
