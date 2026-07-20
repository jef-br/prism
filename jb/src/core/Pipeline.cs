namespace Prism.Core;

/// <summary>
/// Implementation layer the <see cref="PrismService"/> orchestrator controls. Holds the in-process
/// pipeline services and the local <see cref="IArtifactStore"/>, and exposes one method per stage group
/// (Import, Match, Generate, Transform, Export). Each method threads a typed result record forward — there
/// is no shared mutable context. The services emit the eight stage progress events in immutable order:
/// Imported → Classified → Matched → Ordered → Renamed → Generated → Transformed → Exported.
/// </summary>
internal sealed class Pipeline : IDisposable
{
    private readonly IArtifactStore artifactStore;
    private readonly IIngestService ingestService;
    private readonly IMatchingService matchingService;
    private readonly IGenerateService generateService;
    private readonly ITransformService transformService;
    private readonly bool detOrderGapsAllowed;

    /// <summary>
    /// Creates a Pipeline by discovering its services from the environment: in-process by default, or HTTP
    /// clients to remote hosts when their URL variables are set. The API uses this path.
    /// </summary>
    /// <param name="configuration">Validated PRISM configuration loaded at startup.</param>
    /// <param name="modelBuilder">Pre-loaded Excel model builder from ExcelConfig.json.</param>
    internal Pipeline(PrismConfiguration configuration, ModelBuilder modelBuilder)
        : this(PipelineServiceFactory.CreateFromEnvironment(
            configuration ?? throw new ArgumentNullException(nameof(configuration)),
            modelBuilder ?? throw new ArgumentNullException(nameof(modelBuilder))),
            configuration!.DetOrderGapsAllowed)
    {
    }

    /// <summary>
    /// Creates a Pipeline over an explicit service set. This is the DI seam: callers inject in-process or
    /// HTTP-client implementations without the pipeline knowing which.
    /// </summary>
    /// <param name="services">The service implementations and shared artifact store this pipeline runs on.</param>
    /// <param name="detOrderGapsAllowed">Output.DET-ORDER-GAPS-ALLOWED policy; forwarded to Export for det compaction.</param>
    internal Pipeline(PipelineServices services, bool detOrderGapsAllowed = false)
    {
        ArgumentNullException.ThrowIfNull(services);

        artifactStore       = services.ArtifactStore;
        ingestService       = services.Ingest;
        matchingService     = services.Matching;
        generateService     = services.Generate;
        transformService    = services.Transform;
        this.detOrderGapsAllowed = detOrderGapsAllowed;
    }

    /// <summary>Disposes services that own native resources (e.g. the CLIP ONNX session in MatchingService).</summary>
    public void Dispose() { if (matchingService is IDisposable d) d.Dispose(); }

    // -------------------------------------------------------------------------
    // Stage name constants — single source of truth for the immutable stage order.
    // -------------------------------------------------------------------------

    /// <summary>Definitive stage names in immutable order. Consumed by the Export manifest builder.</summary>
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
    // Stage-group methods — called in order by PrismService.Process().
    // -------------------------------------------------------------------------

    /// <summary>Imported stage: normalize input and build FamilyRecords.</summary>
    internal Task<IngestResult> ImportAsync(
        PrismJobRequest request,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
        => ingestService.ImportAsync(request, artifactStore, progress, cancellationToken);

    /// <summary>Classified → Matched → Ordered → Renamed: convert every image into an enriched LAMBDA.</summary>
    internal Task<MatchingResult> MatchAsync(
        IngestResult ingest,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
        => matchingService.MatchAsync(ingest, artifactStore, progress, cancellationToken);

    /// <summary>Generated stage: enrich hero LAMBDAs and create synthetic images.</summary>
    internal Task<GenerateResult> GenerateAsync(
        MatchingResult matched,
        bool generationEnabled,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
        => generateService.GenerateAsync(matched, generationEnabled, progress, cancellationToken);

    /// <summary>Transformed stage: route each non-KO LAMBDA through its transformation.</summary>
    internal Task<TransformResult> TransformAsync(
        MatchingResult matched,
        bool transformEnabled,
        bool headcut,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
        => transformService.TransformAsync(matched, transformEnabled, headcut, progress, cancellationToken);

    /// <summary>
    /// Exported stage: assemble the canonical manifest (and ZIP when requested) from the final LAMBDA
    /// collection. Export is assembled here, not by a separate service.
    /// </summary>
    internal async Task<ExportArtifacts> ExportAsync(
        TransformResult transformed,
        IReadOnlyList<ImageRecord_GENERATED> generatedImages,
        PrismJobRequest request,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
    {
        await StageProgress.EmitStarted(progress, request.JobID, PipelineStageNames.Exported, cancellationToken);
        ExportArtifacts artifacts = Exporter.Run(BuildExportRequest(transformed, generatedImages, request));
        await StageProgress.EmitCompleted(progress, request.JobID, PipelineStageNames.Exported, artifacts.Manifest.Summary.OkTransformed, artifacts.Manifest.Summary.KoRecords, cancellationToken);
        return artifacts;
    }

    /// <summary>
    /// Gathers the final LAMBDA collection plus every accumulated count into the explicit
    /// <see cref="ExportRequest"/> the Exporter needs to build the manifest summary.
    /// </summary>
    private ExportRequest BuildExportRequest(
        TransformResult transformed,
        IReadOnlyList<ImageRecord_GENERATED> generatedImages,
        PrismJobRequest request)
    {
        MatchingResult matched = transformed.Matched;
        IngestResult   ingest  = matched.Ingest;

        return new ExportRequest
        {
            JobID              = request.JobID,
            LambdaRecords      = matched.LambdaRecords,
            NormalizedImages   = ingest.NormalizedImages,
            FirstExcelTempPath = ingest.FirstExcelTempPath,
            Format             = request.PrismProcessingParameters?.Format ?? "json",
            ImageCount         = ingest.OriginalImageCount,
            ExcelCount         = ingest.OriginalExcelCount,
            ZipCount           = ingest.OriginalZipCount,
            OkRenamedCount     = matched.OkRenamedCount,
            KoRecordCount      = ingest.KoRecordCount + matched.KoRecordCount,
            OkTransformedCount = transformed.OkTransformedCount,
            GeneratedCount     = generatedImages.Count,
            DetOrderGapsAllowed = detOrderGapsAllowed,
            Warnings           = [.. ingest.Warnings, .. matched.Warnings]
        };
    }
}
