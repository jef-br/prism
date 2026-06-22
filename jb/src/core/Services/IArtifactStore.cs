namespace Prism.Core;

/// <summary>
/// Thin abstraction over the local job temp folder — the artifact bus shared by every PRISM service.
/// Resolves logical keys (normalized images, LAMBDA documents) to absolute local paths and persists
/// the per-image LAMBDA document so any downstream service can read a stage's output without a shared
/// mutable context. Local filesystem only — there is no cloud backing and never will be.
/// </summary>
public interface IArtifactStore
{
    /// <summary>Server-level temp root that holds one subfolder per job.</summary>
    string JobTempRoot { get; }

    /// <summary>Absolute path to the job's temp folder, created on demand.</summary>
    string JobFolder(Guid jobId);

    /// <summary>Absolute path to the JSON LAMBDA document for one image within a job.</summary>
    string LambdaDocumentPath(Guid jobId, string imageId);

    /// <summary>Writes the LAMBDA document to <c>{job}/lambda/{imageId}.json</c>.</summary>
    void SaveLambdaDocument(Guid jobId, string imageId, ImageRecord_LAMBDA lambda);

    /// <summary>Reads a previously saved LAMBDA document, or null when it does not exist.</summary>
    ImageRecord_LAMBDA? LoadLambdaDocument(Guid jobId, string imageId);
}
