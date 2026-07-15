namespace Prism.Api;

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
    public IReadOnlyList<string> SessionRuntimeProviders { get; init; } = [];
    public bool ConfigReady { get; init; }
    public bool RequiredModelAssetsReady { get; init; }
    public bool TempStorageReady { get; init; }
    public string Notes { get; init; } = string.Empty;
}
