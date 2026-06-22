namespace Prism.Core;

/// <summary>
/// The set of service implementations a <see cref="Pipeline"/> runs on, plus the shared artifact store.
/// Each member may be an in-process implementation or an HTTP client to a remote host — the pipeline does
/// not care which. This is the seam that lets the same code run as a modular monolith or as distributed
/// services (Phase 2).
/// </summary>
public sealed record PipelineServices(
    IIngestService Ingest,
    IMatchingService Matching,
    IGenerateService Generate,
    ITransformService Transform,
    IArtifactStore ArtifactStore);
