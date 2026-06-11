/// <summary>
/// Progress event emitted by a PRISM pipeline stage.
/// </summary>
public sealed record PipelineProgressEvent
{
    /// <summary>
    /// PRISM-owned job identifier.
    /// </summary>
    public Guid JobID { get; init; }

    /// <summary>
    /// Route stage that emitted this progress event.
    /// </summary>
    public string Stage { get; init; } = string.Empty;

    /// <summary>
    /// Safe current item name or identifier when available.
    /// </summary>
    public string? CurrentItem { get; init; }

    /// <summary>
    /// Number of completed items when known.
    /// </summary>
    public int? CompletedCount { get; init; }

    /// <summary>
    /// Total item count when known.
    /// </summary>
    public int? TotalCount { get; init; }

    /// <summary>
    /// Safe event severity.
    /// </summary>
    public string Severity { get; init; } = "Information";

    /// <summary>
    /// Safe progress message.
    /// </summary>
    public string SafeMessage { get; init; } = string.Empty;

    /// <summary>
    /// Event timestamp in UTC.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
