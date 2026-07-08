namespace Prism.Services.Matching;

/// <summary>
/// Pipeline-visible Matching service. Owns the ImageRecord_INPUT → ImageRecord_LAMBDA conversion:
/// internally it runs FeatureAnalysis, Classification, and ImageNGP, then the matching waterfall,
/// the det-order assignment, and the rename validation. Emits the Classified, Matched, Ordered, and
/// Renamed stage events as it goes. FeatureAnalysis/Classification/ImageNGP are not visible to the
/// orchestrator — they are reached only through this service.
/// </summary>
public interface IMatchingService
{
    /// <summary>Converts every normalized image into an enriched LAMBDA document.</summary>
    Task<MatchingResult> MatchAsync(
        IngestResult ingest,
        IArtifactStore store,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken);
}
