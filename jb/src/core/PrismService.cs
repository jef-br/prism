namespace Prism.Core;

/// <summary>
/// PRISM facade. Accepts core-facing job requests, validates them, and delegates
/// real pipeline work to <see cref="Pipeline"/>. Reads like a recipe:
/// Initialize sets up validated resources; Process expresses the job lifecycle;
/// helpers below each method do their named step.
/// </summary>
public sealed class PrismService : IDisposable {
    private readonly PrismConfiguration configuration;
    private readonly ModelBuilder modelBuilder;
    private readonly Pipeline pipeline;

    // -------------------------------------------------------------------------
    // Lifecycle — Initialize
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates the PRISM facade, loads and validates all configuration on startup.
    /// Throws <see cref="PrismConfigurationException"/> if any required config file or model asset is missing or invalid.
    /// </summary>
    public PrismService() {
        (this.configuration, this.modelBuilder) = Initialize();
        this.pipeline = new Pipeline(this.configuration, this.modelBuilder);
    }

    /// <summary>
    /// Creates the PRISM facade with already-loaded configuration and model builder.
    /// Intended for testing and injection scenarios where config is pre-validated.
    /// </summary>
    /// <param name="configuration">Pre-validated PRISM configuration.</param>
    /// <param name="modelBuilder">Pre-loaded Excel model builder.</param>
    public PrismService(PrismConfiguration configuration, ModelBuilder modelBuilder) {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.modelBuilder = modelBuilder ?? throw new ArgumentNullException(nameof(modelBuilder));
        this.pipeline = new Pipeline(this.configuration, this.modelBuilder);
    }

    /// <summary>Disposes the pipeline and its owned resources (CLIP ONNX session).</summary>
    public void Dispose() => this.pipeline.Dispose();

    // -------------------------------------------------------------------------
    // Entry point — Process
    // -------------------------------------------------------------------------

    /// <summary>
    /// Processes one PRISM job through the full pipeline. Reads top to bottom like the pipeline route:
    /// import the batch, match every image into a LAMBDA, generate supplemental images, transform, then
    /// export. Each step hands a typed result to the next — there is no shared mutable context. Real stage
    /// work is delegated to <see cref="Pipeline"/> and its services.
    /// </summary>
    /// <param name="request">The normalized core-facing job request.</param>
    /// <param name="progress">Progress callback used by API SSE transport and workbench direct invocation.</param>
    /// <param name="cancellationToken">Host shutdown token — does not cancel accepted user jobs.</param>
    /// <returns>A structured PRISM job result.</returns>
    public async Task<PrismJobResult> Process(
        PrismJobRequest request,
        Func<PipelineProgressEvent, Task>? progress = null,
        CancellationToken cancellationToken = default) {
        ValidateRequest(request);

        try {
            IngestResult normalizedImagesAndFamilies = await this.Import(request, progress, cancellationToken);
            MatchingResult matchedImages = await this.Match(normalizedImagesAndFamilies, progress, cancellationToken);
            (MatchingResult matchedWithGenerations, IReadOnlyList<ImageRecord_GENERATED> generatedImages)
                                                          = await this.GenerateSupplementalImages(matchedImages, progress, cancellationToken);
            TransformResult transformedImages = await this.TransformImages(matchedWithGenerations, progress, cancellationToken);
            ExportArtifacts manifestAndZip = await this.Export(transformedImages, generatedImages, request, progress, cancellationToken);

            return BuildSuccessResult(request, manifestAndZip);
        }
        catch (Exception exception) when (exception is not PrismConfigurationException) {
            return BuildFailedResult(request, exception);
        }
    }

    // -------------------------------------------------------------------------
    // Match-only routes — Import + Match + Order, no Transform or Export.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs the full import → classify → match → order pipeline and returns only the filename mapping.
    /// Writes one normalized JPEG per image to the local job temp folder (same as <see cref="Process"/>);
    /// no Transform or Export artifacts are produced.
    /// </summary>
    public async Task<MatchOnlyResult> MatchOnlyAsync(
        PrismJobRequest request,
        CancellationToken cancellationToken = default) {
        ValidateRequest(request);
        IngestResult ingest = await this.Import(request, null, cancellationToken);
        MatchingResult matched = await this.Match(ingest, null, cancellationToken);

        // Mirror the export-time det-gap policy so match-only filenames match the full pipeline.
        if (!this.configuration.DetOrderGapsAllowed)
            ImageOrderer.CompactDetOrder(matched.LambdaRecords);

        return BuildMatchOnlyResult(matched.LambdaRecords);
    }

    /// <summary>
    /// Lite match: builds LAMBDA records from filenames only (no image decode, no disk writes for images),
    /// parses Excel to get FamilyRecords, then runs ImageMatcher + ImageOrderer.
    /// Bracket 4 (CLIP semantic) is skipped because no Tags are present. Det order falls back to
    /// source-index within each family.
    /// </summary>
    public MatchOnlyResult MatchLite(
        IReadOnlyList<ImageRecord_INPUT> imageInputs,
        IReadOnlyList<InputExcelFileRecord> excelInputs) {
        IEnumerable<string> excelPaths = excelInputs
            .Where(e => e.TempFilePath is not null)
            .Select(e => e.TempFilePath!);

        ExcelModelBuildResult built = this.modelBuilder.BuildFromExcelFiles(excelPaths);

        List<ImageRecord_LAMBDA> lambdas = imageInputs
            .Select(r => new ImageRecord_LAMBDA { InitialFullName = r.InitialFullName })
            .ToList();

        ImageMatcher.Run(lambdas, built.FamilyRecords);
        ImageOrderer.Run(lambdas, built.FamilyRecords);
        ImageRenamer.Run(lambdas);

        // Mirror the export-time det-gap policy so MatchLite filenames match the full pipeline.
        if (!this.configuration.DetOrderGapsAllowed)
            ImageOrderer.CompactDetOrder(lambdas);

        return BuildMatchOnlyResult(lambdas);
    }

    /// <summary>Projects a LAMBDA collection into the client-facing filename mapping.</summary>
    private static MatchOnlyResult BuildMatchOnlyResult(IReadOnlyList<ImageRecord_LAMBDA> lambdas) {
        var map = new Dictionary<string, string?>(lambdas.Count);
        int matched = 0, unmatched = 0;

        foreach (ImageRecord_LAMBDA lambda in lambdas) {
            bool isMatched = !lambda.IsKo && !string.IsNullOrEmpty(lambda.Family);
            map[lambda.InitialFullName] = isMatched ? lambda.NewName : null;
            if (isMatched) matched++; else unmatched++;
        }

        return new MatchOnlyResult { FileNameMap = map, Matched = matched, Unmatched = unmatched };
    }

    // -------------------------------------------------------------------------
    // Pipeline route — each step delegates to Pipeline and names what it produces.
    // -------------------------------------------------------------------------

    /// <summary>Imports the batch: normalized images on the local job folder + FamilyRecords from Excel.</summary>
    private Task<IngestResult> Import(
        PrismJobRequest request,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
        => this.pipeline.ImportAsync(request, progress, cancellationToken);

    /// <summary>Converts every normalized image into an enriched LAMBDA (classify → match → order → rename).</summary>
    private Task<MatchingResult> Match(
        IngestResult normalizedImagesAndFamilies,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
        => this.pipeline.MatchAsync(normalizedImagesAndFamilies, progress, cancellationToken);

    /// <summary>
    /// Generates supplemental images. Returns both outputs explicitly: the LAMBDA collection enriched in
    /// place with generation route state, and the new synthetic image records.
    /// </summary>
    private Task<GenerateResult> GenerateSupplementalImages(
        MatchingResult matchedImages,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
        => this.pipeline.GenerateAsync(matchedImages, matchedImages.Ingest.Parameters.Generation, progress, cancellationToken);

    /// <summary>Transforms each non-KO image, attaching an OutputRecord carrying the transform outcome.</summary>
    private Task<TransformResult> TransformImages(
        MatchingResult matchedWithGenerations,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
        => this.pipeline.TransformAsync(
            matchedWithGenerations,
            matchedWithGenerations.Ingest.Parameters.Transform,
            matchedWithGenerations.Ingest.Parameters.Headcut,
            progress,
            cancellationToken);

    /// <summary>Exports the fully-enriched LAMBDAs and generated images into the manifest and optional ZIP.</summary>
    private Task<ExportArtifacts> Export(
        TransformResult transformedImages,
        IReadOnlyList<ImageRecord_GENERATED> generatedImages,
        PrismJobRequest request,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
        => this.pipeline.ExportAsync(transformedImages, generatedImages, request, progress, cancellationToken);

    // -------------------------------------------------------------------------
    // Helpers — called by Initialize
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads Prism_Config.json, validates all required folder-local config files,
    /// and pre-loads the Excel model builder so ExcelConfig.json failures surface at startup.
    /// Throws <see cref="PrismConfigurationException"/> if any required asset is missing or invalid.
    /// </summary>
    private static (PrismConfiguration Config, ModelBuilder ExcelModelBuilder) Initialize() {
        PrismConfiguration config = PrismConfiguration.LoadPrismConfig(
            ConfigLoader.RequireFile(PrismConfiguration.FileName));
        ValidateRequiredFolderLocalConfigs();
        ModelBuilder modelBuilder = ModelBuilder.FromConfigFile(ConfigLoader.RequireFile("ExcelConfig.json"));
        return (config, modelBuilder);
    }

    private static void ValidateRequiredFolderLocalConfigs() {
        string[] requiredFolderLocalConfigs =
        [
            "ExcelConfig.json",
            "HostRules.json",
            "MatchingConfig.json",
            "TranslationDictionary.json",
            "DetOrderRules.json",
            "DetOrderKeywordStems.json"
        ];

        // RequireFile throws PrismConfigurationException naming the file and every path it searched.
        foreach (string configFileName in requiredFolderLocalConfigs)
            ConfigLoader.RequireFile(configFileName);
    }

    // -------------------------------------------------------------------------
    // Helpers — called by Process
    // -------------------------------------------------------------------------

    /// <summary>
    /// Validates that the request satisfies all pre-pipeline requirements.
    /// Throws <see cref="ArgumentException"/> for caller-supplied structural failures.
    /// </summary>
    private static void ValidateRequest(PrismJobRequest request) {
        if (request is null) {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.JobID == Guid.Empty) {
            throw new ArgumentException("PrismJobRequest.JobID is required.", nameof(request));
        }

        if (request.PrismProcessingParameters is null) {
            throw new ArgumentException("PrismProcessingParameters is required.", nameof(request));
        }

        // ZIP files are extracted by the Import stage — allow ZIP-only requests through here.
        if (request.ImageRecords.Count == 0 && request.ZipFileRecords.Count == 0) {
            throw new ArgumentException("At least one accepted image record is required.", nameof(request));
        }

        if (request.ExcelRecords.Count == 0 && request.ZipFileRecords.Count == 0) {
            throw new ArgumentException("At least one accepted Excel record is required.", nameof(request));
        }
    }

    /// <summary>
    /// Projects the completed Export artifacts into the caller-facing <see cref="PrismJobResult"/>.
    /// </summary>
    private static PrismJobResult BuildSuccessResult(PrismJobRequest request, ExportArtifacts manifestAndZip) {
        BatchManifest manifest = manifestAndZip.Manifest;

        return new PrismJobResult {
            JobID = request.JobID,
            ClientRequestToken = request.ClientRequestToken,
            Status = "Completed",
            OutputFormat = request.PrismProcessingParameters?.Format ?? "json",
            FailureReason = null,
            Warnings = manifest.Warnings,
            Manifest = manifest,
            ZipBytes = manifestAndZip.ZipBytes,
            OkImages = manifestAndZip.JourneyItems.Where(j => j.Output is not null).ToList(),
            KoImages = manifestAndZip.JourneyItems.Where(j => j.Output is null).ToList()
        };
    }

    /// <summary>
    /// Builds a failed-job result when an unexpected (non-configuration) exception aborts the pipeline.
    /// Every input image is reported as KO; no artifacts are produced.
    /// </summary>
    private static PrismJobResult BuildFailedResult(PrismJobRequest request, Exception exception) {
        BatchManifest manifest = new() {
            JobID = request.JobID,
            Summary = new BatchManifestSummary {
                ImageCount = request.ImageRecords.Count,
                ExcelCount = request.ExcelRecords.Count,
                ZipCount = request.ZipFileRecords.Count,
                OkRenamed = 0,
                KoRecords = request.ImageRecords.Count
            },
            RouteSummaries = [$"Pipeline failed: {exception.Message}"],
            Warnings = []
        };

        return new PrismJobResult {
            JobID = request.JobID,
            ClientRequestToken = request.ClientRequestToken,
            Status = "Failed",
            OutputFormat = request.PrismProcessingParameters?.Format ?? "json",
            FailureReason = exception.Message,
            Warnings = [],
            Manifest = manifest,
            ZipBytes = null,
            OkImages = [],
            KoImages = []
        };
    }
}
