namespace Prism.Api;

/// <summary>
/// Result of API ingress validation and core request construction.
/// </summary>
internal sealed record PrismProcessIngressResult
{
    public PrismJobRequest? Request { get; init; }
    public PrismPreCoreErrorResponse? Error { get; init; }

    public static PrismProcessIngressResult FromRequest(PrismJobRequest request)
    {
        return new PrismProcessIngressResult { Request = request };
    }

    public static PrismProcessIngressResult FromError(PrismPreCoreErrorResponse error)
    {
        return new PrismProcessIngressResult { Error = error };
    }
}
