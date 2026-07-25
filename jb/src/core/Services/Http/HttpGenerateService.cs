namespace Prism.Core;

/// <summary>
/// HTTP client implementation of <see cref="IGenerateService"/>. POSTs the matching result to a remote
/// Generate host and returns its <see cref="GenerateResult"/>. The enable flag is carried inside the
/// matching result (Ingest.Parameters), so the remote host derives it; the explicit flag is ignored.
/// Emits the Generated event client-side.
/// </summary>
public sealed class HttpGenerateService : IGenerateService {
    private readonly HttpClient client;

    /// <summary>Creates the client targeting the remote Generate host base address.</summary>
    public HttpGenerateService(Uri baseAddress)
        => this.client = ServiceHttp.CreateClient(baseAddress);

    /// <summary>Creates the client over an externally-managed <see cref="HttpClient"/>.</summary>
    public HttpGenerateService(HttpClient client)
        => this.client = client ?? throw new ArgumentNullException(nameof(client));

    /// <inheritdoc/>
    public async Task<GenerateResult> GenerateAsync(
        MatchingResult matched,
        bool generationEnabled,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken) {
        await StageProgress.EmitStarted(progress, matched.Ingest.JobID, PipelineStageNames.Generated, cancellationToken);
        return await ServiceHttp.PostJson<MatchingResult, GenerateResult>(
            this.client, PrismServiceRoutes.Generate, matched, cancellationToken);
    }
}
