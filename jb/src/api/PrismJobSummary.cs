namespace Prism.Api;

/// <summary>
/// Compact per-job summary for the job-list endpoint.
/// </summary>
internal sealed record PrismJobSummary(
    Guid JobID,
    string Status,
    bool IsTerminal,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string ProgressUrl,
    string ResultUrl,
    int OkImages,
    int KoImages);
