/// <summary>
/// Describes the current readiness of the API host and minimal PRISM processing adapter.
/// </summary>
internal sealed record PrismHealthResponse
{
    public string Message { get; init; } = string.Empty;
    public bool CanAcceptJobs { get; init; }
    public bool ProcessingWired { get; init; }
    public int ActiveJobCount { get; init; }
    public int QueuedJobCount { get; init; }
    public int MaxQueuedJobs { get; init; }
    public int MaxConcurrentJobs { get; init; }
    public IReadOnlyList<string> SupportedRuntimeProviders { get; init; } = [];
    public bool ConfigReady { get; init; }
    public bool RequiredModelAssetsReady { get; init; }
    public bool TempStorageReady { get; init; }
    public string Notes { get; init; } = string.Empty;
}

/// <summary>
/// Safe public configuration response.
/// </summary>
internal sealed record PrismSafeConfigResponse
{
    public bool ConfigReady { get; init; }
    public bool SafeConfigurationAvailable { get; init; }
    public IReadOnlyList<string> AcceptedMediaTypes { get; init; } = [];
    public IReadOnlyList<string> OutputFormats { get; init; } = [];
    public PrismVisibleFeatureFlags VisibleFeatureFlags { get; init; } = new(false, false, false, false, false);
    public PrismSafeLimitResponse Limits { get; init; } = new();
    public PrismQueueConfigResponse Queue { get; init; } = new(0, 0);
    public string Notes { get; init; } = string.Empty;
}

/// <summary>
/// Feature flags safe to expose to callers.
/// </summary>
internal sealed record PrismVisibleFeatureFlags(
    bool Rename,
    bool Transform,
    bool Generation,
    bool ProgressSse,
    bool MinimalCoreAdapter);

/// <summary>
/// Queue limits safe to expose.
/// </summary>
internal sealed record PrismQueueConfigResponse(int MaxQueuedJobs, int MaxConcurrentJobs);

/// <summary>
/// Envelope returned immediately after a job is accepted.
/// </summary>
internal sealed record PrismJobStartEnvelope
{
    public Guid JobID { get; init; }
    public string? ClientRequestToken { get; init; }
    public string ProgressUrl { get; init; } = string.Empty;
    public string ResultUrl { get; init; } = string.Empty;
    public string Status { get; init; } = "Queued";
}

/// <summary>
/// Pre-core API error payload.
/// </summary>
internal sealed record PrismPreCoreErrorResponse
{
    public string CorrelationId { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> Details { get; init; } = [];
    public IReadOnlyList<string> FieldErrors { get; init; } = [];
    public bool Retryable { get; init; }

    /// <summary>
    /// Creates a safe pre-core API error payload.
    /// </summary>
    public static PrismPreCoreErrorResponse Create(
        string correlationId,
        string code,
        string message,
        IReadOnlyList<string> details,
        IReadOnlyList<string> fieldErrors,
        bool retryable = false)
    {
        return new PrismPreCoreErrorResponse
        {
            CorrelationId = correlationId,
            Code = code,
            Message = message,
            Details = details,
            FieldErrors = fieldErrors,
            Retryable = retryable
        };
    }
}

/// <summary>
/// JSON result envelope matching the documented top-level shape.
/// </summary>
internal sealed record PrismJsonResultEnvelope(PrismJobResult? Result)
{
    public BatchManifest? Manifest => Result?.Manifest;
    public PrismJsonImagesEnvelope Images { get; init; } = new();
    public object? OriginalImages => null;
}

/// <summary>
/// Per-image journey groups returned by JSON result retrieval.
/// </summary>
internal sealed record PrismJsonImagesEnvelope
{
    public IReadOnlyList<object> Ok { get; init; } = [];
    public IReadOnlyList<object> Ko { get; init; } = [];
}

/// <summary>
/// URLs assigned to an accepted job.
/// </summary>
internal sealed record PrismJobUrls(string ProgressUrl, string ResultUrl);
