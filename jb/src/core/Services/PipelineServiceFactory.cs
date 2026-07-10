namespace Prism.Core;

/// <summary>
/// Builds the <see cref="PipelineServices"/> set. Defaults to in-process implementations; when a remote
/// host URL is configured for a service (via environment variable), that service is swapped for its HTTP
/// client automatically — no pipeline or orchestrator change required. This is how a monolith deployment
/// (no URLs → all in-process) and a distributed deployment (URLs set → HTTP clients) share one code path.
/// Discovery is environment-only and local-filesystem-friendly: every host shares the same job temp folder.
/// </summary>
public static class PipelineServiceFactory
{
    /// <summary>Environment variable naming the remote Ingest host base URL.</summary>
    public const string IngestUrlVariable = "PRISM_INGEST_URL";

    /// <summary>Environment variable naming the remote Matching host base URL.</summary>
    public const string MatchingUrlVariable = "PRISM_MATCHING_URL";

    /// <summary>Environment variable naming the remote Generate host base URL.</summary>
    public const string GenerateUrlVariable = "PRISM_GENERATE_URL";

    /// <summary>Environment variable naming the remote Transform host base URL.</summary>
    public const string TransformUrlVariable = "PRISM_TRANSFORM_URL";

    /// <summary>Builds an all-in-process service set (the modular monolith).</summary>
    public static PipelineServices CreateInProcess(PrismConfiguration configuration, ModelBuilder modelBuilder)
    {
        EnsureUpscalerReady(configuration);

        return new(
            new IngestService(configuration, modelBuilder),
            new MatchingService(configuration),
            new GenerateService(),
            new TransformService(),
            new LocalArtifactStore());
    }

    /// <summary>
    /// Builds a service set from environment discovery: each service runs in-process unless its URL
    /// variable is set, in which case the HTTP client to that remote host is used instead.
    /// </summary>
    public static PipelineServices CreateFromEnvironment(PrismConfiguration configuration, ModelBuilder modelBuilder)
    {
        IIngestService ingest = RemoteUrl(IngestUrlVariable) is { } ingestUrl
            ? new HttpIngestService(ingestUrl)
            : new IngestService(configuration, modelBuilder);

        IMatchingService matching = RemoteUrl(MatchingUrlVariable) is { } matchingUrl
            ? new HttpMatchingService(matchingUrl)
            : new MatchingService(configuration);

        IGenerateService generate = RemoteUrl(GenerateUrlVariable) is { } generateUrl
            ? new HttpGenerateService(generateUrl)
            : new GenerateService();

        ITransformService transform;
        if (RemoteUrl(TransformUrlVariable) is { } transformUrl)
        {
            transform = new HttpTransformService(transformUrl);
        }
        else
        {
            // Eagerly load the process-wide Real-ESRGAN GPU session before this in-process
            // TransformService runs (T-2800) — mirrors the MatchingService/CLIP eager-init above. Only
            // needed when Transform actually runs in this process; a remote Transform host initializes
            // its own copy at its own startup (Prism.ServiceHost/Program.cs), so there is nothing to do
            // on the HttpTransformService branch.
            EnsureUpscalerReady(configuration);
            transform = new TransformService();
        }

        return new PipelineServices(ingest, matching, generate, transform, new LocalArtifactStore());
    }

    /// <summary>
    /// Initializes the process-wide Real-ESRGAN GPU session (<see cref="Upscaler_g_p_u"/>) so the first
    /// Transform job that needs to upscale a below-minimum image doesn't crash (T-2800). Unlike CLIP,
    /// Upscaler_g_p_u is a static, process-wide resource, not one instance per service, and a missing
    /// model asset is not fatal here — <see cref="ImageUpscaler"/> falls back to the CPU Lanczos4 path
    /// via <see cref="Upscaler_g_p_u.IsReady"/>. <see cref="UpscaleService.Create"/> still throws
    /// <see cref="PrismConfigurationException"/> when DirectML is present but the model asset can't be
    /// located (correct for its other caller, the standalone Prism.ServiceHost, which should fail fast);
    /// that specific exception is swallowed here so a missing GPU model asset degrades to CPU instead of
    /// blocking pipeline construction.
    /// </summary>
    private static void EnsureUpscalerReady(PrismConfiguration configuration)
    {
        try { UpscaleService.Create(configuration); }
        catch (PrismConfigurationException) { }
    }

    /// <summary>Reads a service host URL from the environment, or null when unset/blank.</summary>
    private static Uri? RemoteUrl(string variableName)
    {
        string? value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(value) ? null : new Uri(value, UriKind.Absolute);
    }
}
