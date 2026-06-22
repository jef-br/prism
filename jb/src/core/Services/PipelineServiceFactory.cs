namespace Prism.Core;

/// <summary>
/// Builds the <see cref="PipelineServices"/> set. Defaults to in-process implementations; when a remote
/// host URL is configured for a service (via environment variable), that service is swapped for its HTTP
/// client automatically — no pipeline or orchestrator change required. This is how WPF (no URLs → all
/// in-process) and a distributed deployment (URLs set → HTTP clients) share one code path.
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
        => new(
            new IngestService(configuration, modelBuilder),
            new MatchingService(configuration),
            new GenerateService(),
            new TransformService(),
            new LocalArtifactStore());

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

        ITransformService transform = RemoteUrl(TransformUrlVariable) is { } transformUrl
            ? new HttpTransformService(transformUrl)
            : new TransformService();

        return new PipelineServices(ingest, matching, generate, transform, new LocalArtifactStore());
    }

    /// <summary>Reads a service host URL from the environment, or null when unset/blank.</summary>
    private static Uri? RemoteUrl(string variableName)
    {
        string? value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(value) ? null : new Uri(value, UriKind.Absolute);
    }
}
