namespace Prism.Core;

/// <summary>
/// HTTP client implementation of <see cref="IMatchingService"/>. POSTs the ingest result to a remote
/// Matching host and returns its <see cref="MatchingResult"/>. Emits the Classified → Matched → Ordered →
/// Renamed events client-side so the SSE stream still reports the four stages this service owns.
/// </summary>
public sealed class HttpMatchingService : IMatchingService
{
    private readonly HttpClient client;

    /// <summary>Creates the client targeting the remote Matching host base address.</summary>
    public HttpMatchingService(Uri baseAddress)
        => client = ServiceHttp.CreateClient(baseAddress);

    /// <summary>Creates the client over an externally-managed <see cref="HttpClient"/>.</summary>
    public HttpMatchingService(HttpClient client)
        => this.client = client ?? throw new ArgumentNullException(nameof(client));

    /// <inheritdoc/>
    public async Task<MatchingResult> MatchAsync(
        IngestResult ingest,
        IArtifactStore store,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
    {
        await StageProgress.EmitStarted(progress, ingest.JobID, PipelineStageNames.Classified, cancellationToken);
        await StageProgress.EmitStarted(progress, ingest.JobID, PipelineStageNames.Matched, cancellationToken);
        await StageProgress.EmitStarted(progress, ingest.JobID, PipelineStageNames.Ordered, cancellationToken);
        await StageProgress.EmitStarted(progress, ingest.JobID, PipelineStageNames.Renamed, cancellationToken);

        return await ServiceHttp.PostJson<IngestResult, MatchingResult>(
            client, PrismServiceRoutes.Match, ingest, cancellationToken);
    }
}
