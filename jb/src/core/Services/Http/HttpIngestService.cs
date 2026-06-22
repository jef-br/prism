namespace Prism.Core;

/// <summary>
/// HTTP client implementation of <see cref="IIngestService"/>. POSTs the job request to a remote Ingest
/// host and returns its <see cref="IngestResult"/>. The local <see cref="IArtifactStore"/> is unused — the
/// remote host owns its own store over the shared local filesystem. Emits the Imported event client-side so
/// the SSE stream still reports the stage even when ingest runs out-of-process.
/// </summary>
public sealed class HttpIngestService : IIngestService
{
    private readonly HttpClient client;

    /// <summary>Creates the client targeting the remote Ingest host base address.</summary>
    public HttpIngestService(Uri baseAddress)
        => client = new HttpClient { BaseAddress = baseAddress };

    /// <summary>Creates the client over an externally-managed <see cref="HttpClient"/>.</summary>
    public HttpIngestService(HttpClient client)
        => this.client = client ?? throw new ArgumentNullException(nameof(client));

    /// <inheritdoc/>
    public async Task<IngestResult> ImportAsync(
        PrismJobRequest request,
        IArtifactStore store,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
    {
        await StageProgress.EmitStarted(progress, request.JobID, PipelineStageNames.Imported, cancellationToken);
        return await ServiceHttp.PostJson<PrismJobRequest, IngestResult>(
            client, PrismServiceRoutes.Ingest, request, cancellationToken);
    }
}
