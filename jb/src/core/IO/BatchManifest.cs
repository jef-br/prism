namespace Prism.Core;

/// <summary>
/// Batch-level manifest projected into zip and JSON output.
/// </summary>
public sealed record BatchManifest
{
    /// <summary>
    /// PRISM-owned job identifier when a job has been created.
    /// </summary>
    public Guid? JobID { get; init; }

    /// <summary>
    /// Summary counts safe for API, workbench, and manifest consumers.
    /// </summary>
    public BatchManifestSummary Summary { get; init; } = new();

    /// <summary>
    /// Per-image rows — one entry per lambda record processed by the pipeline.
    /// </summary>
    public IReadOnlyList<ManifestImageRow> ImageRows { get; init; } = [];

    /// <summary>
    /// Safe route-stage summaries emitted for this job.
    /// </summary>
    public IReadOnlyList<string> RouteSummaries { get; init; } = [];

    /// <summary>
    /// Safe warnings emitted while building the result.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
