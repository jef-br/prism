namespace Prism.Core;

/// <summary>
/// Route paths shared by the HTTP client implementations and the service host, so the wire contract has
/// a single source of truth. Each pipeline-visible service is reachable at its own path; every service
/// also exposes a <c>{route}/health</c> endpoint plus a root <see cref="Health"/>.
/// </summary>
public static class PrismServiceRoutes
{
    /// <summary>POST PrismJobRequest → IngestResult.</summary>
    public const string Ingest = "/prism-service/ingest";

    /// <summary>POST IngestResult → MatchingResult.</summary>
    public const string Match = "/prism-service/match";

    /// <summary>POST MatchingResult → GenerateResult.</summary>
    public const string Generate = "/prism-service/generate";

    /// <summary>POST MatchingResult → TransformResult.</summary>
    public const string Transform = "/prism-service/transform";

    /// <summary>Root health endpoint for a host.</summary>
    public const string Health = "/health";
}
