namespace Prism.Api;

/// <summary>
/// Queue limits safe to expose.
/// </summary>
internal sealed record PrismQueueConfigResponse(int MaxQueuedJobs, int MaxConcurrentJobs);
