namespace Prism.Core;

/// <summary>
/// HTTP client implementation of <see cref="ITransformService"/>. POSTs the matching result to a remote
/// Transform host and returns its <see cref="TransformResult"/>. The enable flag is carried inside the
/// matching result (Ingest.Parameters), so the remote host derives it; the explicit flag is ignored.
/// Emits the Transformed event client-side.
/// </summary>
public sealed class HttpTransformService : ITransformService
{
    private readonly HttpClient client;

    /// <summary>Creates the client targeting the remote Transform host base address.</summary>
    public HttpTransformService(Uri baseAddress)
        => client = ServiceHttp.CreateClient(baseAddress);

    /// <summary>Creates the client over an externally-managed <see cref="HttpClient"/>.</summary>
    public HttpTransformService(HttpClient client)
        => this.client = client ?? throw new ArgumentNullException(nameof(client));

    /// <inheritdoc/>
    public async Task<TransformResult> TransformAsync(
        MatchingResult matched,
        bool transformEnabled,
        bool headcut,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
    {
        await StageProgress.EmitStarted(progress, matched.Ingest.JobID, PipelineStageNames.Transformed, cancellationToken);
        return await ServiceHttp.PostJson<MatchingResult, TransformResult>(
            client, PrismServiceRoutes.Transform, matched, cancellationToken);
    }
}
