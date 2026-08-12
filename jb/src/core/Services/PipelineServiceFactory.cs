namespace Prism.Core;

/// <summary>
/// Builds the <see cref="PipelineServices"/> set. Defaults to in-process implementations; when a remote
/// host URL is configured for a public service (via environment variable), that service is swapped for its
/// HTTP client automatically — no pipeline or orchestrator change required. This is how a monolith
/// deployment (no URLs → all in-process) and a distributed deployment (URLs set → HTTP clients) share one
/// code path. Only the public services (Matching, Generate, Transform, Upscale) are remotable; core
/// (ingress, export, job mechanics) always runs in-process. Discovery is environment-only and
/// local-filesystem-friendly: every host shares the same job temp folder.
/// </summary>
public static class PipelineServiceFactory {
    /// <summary>Environment variable naming the remote Matching host base URL.</summary>
    public const string MatchingUrlVariable = "PRISM_MATCHING_URL";

    /// <summary>Environment variable naming the remote Generate host base URL.</summary>
    public const string GenerateUrlVariable = "PRISM_GENERATE_URL";

    /// <summary>Environment variable naming the remote Transform host base URL.</summary>
    public const string TransformUrlVariable = "PRISM_TRANSFORM_URL";

    /// <summary>Environment variable naming the remote Upscale host base URL.</summary>
    public const string UpscaleUrlVariable = "PRISM_UPSCALE_URL";

    /// <summary>Builds an all-in-process service set (the modular monolith).</summary>
    public static PipelineServices CreateInProcess(PrismConfiguration configuration, ModelBuilder modelBuilder) {
        EnsureUpscalerReady(configuration);

        return new(
            new IngestService(configuration, modelBuilder),
            new MatchingService(configuration),
            new GenerateService(configuration),
            new TransformService(),
            new LocalArtifactStore());
    }

    /// <summary>
    /// Builds a service set from environment discovery: each service runs in-process unless its URL
    /// variable is set, in which case the HTTP client to that remote host is used instead.
    /// </summary>
    public static PipelineServices CreateFromEnvironment(PrismConfiguration configuration, ModelBuilder modelBuilder) {
        // Ingest is core, not a public service — it always runs in-process where the pipeline runs.
        // Media enters PRISM only through ingress (see PRISM-overview.md "Core vs. Features").
        IIngestService ingest = new IngestService(configuration, modelBuilder);

        IMatchingService matching = RemoteUrl(MatchingUrlVariable) is { } matchingUrl
            ? new HttpMatchingService(matchingUrl)
            : new MatchingService(configuration);

        IGenerateService generate = RemoteUrl(GenerateUrlVariable) is { } generateUrl
            ? new HttpGenerateService(generateUrl)
            : new GenerateService(configuration);

        ITransformService transform;
        if (RemoteUrl(TransformUrlVariable) is { } transformUrl) {
            transform = new HttpTransformService(transformUrl);
        }
        else if (RemoteUrl(UpscaleUrlVariable) is { } upscaleUrl) {
            // In-process Transform delegating upscaling to a remote Upscale host — no local
            // Real-ESRGAN session needed in this process.
            transform = new TransformService(new HttpUpscaleService(upscaleUrl));
        }
        else {
            // Eagerly load the process-wide Real-ESRGAN GPU session before this in-process
            // TransformService runs (T-2800) — mirrors the MatchingService/CLIP eager-init above. Only
            // needed when Transform upscales locally in this process; a remote Transform host initializes
            // its own copy at its own startup (Prism.ServiceHost/Program.cs), so there is nothing to do
            // on the HttpTransformService branch.
            EnsureUpscalerReady(configuration);
            transform = new TransformService();
        }

        return new PipelineServices(ingest, matching, generate, transform, new LocalArtifactStore());
    }

    /// <summary>
    /// Initializes the process-wide Real-ESRGAN session (<see cref="Upscaler"/>) so the first
    /// Transform job that needs to upscale a below-minimum image doesn't crash (T-2800). Unlike CLIP,
    /// Upscaler is a static, process-wide resource, not one instance per service. A missing or
    /// unloadable model asset propagates as <see cref="PrismConfigurationException"/> — there is no
    /// fallback upscaler, so startup fails loud like it does for the YOLO model (T-4110). With
    /// Models.Upscaling.UseIt false the session is never created: TransformService forces
    /// <c>allowEsrganUpscale</c> off from the same config value, so nothing in this process can reach it.
    /// </summary>
    private static void EnsureUpscalerReady(PrismConfiguration configuration) {
        if (!configuration.AiUpscalingEnabled) return;

        UpscaleService.Create(configuration);
    }

    /// <summary>Reads a service host URL from the environment, or null when unset/blank.</summary>
    private static Uri? RemoteUrl(string variableName) {
        string? value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(value) ? null : new Uri(value, UriKind.Absolute);
    }
}
