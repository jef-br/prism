namespace Prism.Core;

/// <summary>
/// Pipeline-visible Ingest service. Turns raw input (images, ZIPs, Excel) into normalized JPEGs on the
/// local job folder plus the FamilyRecords parsed from Excel. Owns the Imported stage boundary.
/// In Phase 1 the only implementation is in-process; Phase 2 adds an HTTP client implementation.
/// </summary>
public interface IIngestService {
    /// <summary>Runs the Imported stage for one job and returns the normalized images and family records.</summary>
    Task<IngestResult> ImportAsync(
        PrismJobRequest request,
        IArtifactStore store,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken);
}
