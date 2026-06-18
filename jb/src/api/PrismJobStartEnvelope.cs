
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

