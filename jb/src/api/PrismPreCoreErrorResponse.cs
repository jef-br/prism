namespace Prism.Api;

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
