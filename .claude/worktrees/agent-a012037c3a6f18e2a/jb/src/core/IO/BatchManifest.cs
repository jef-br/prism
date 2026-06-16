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
    /// Safe route-stage summaries emitted for this job.
    /// </summary>
    public IReadOnlyList<string> RouteSummaries { get; init; } = [];

    /// <summary>
    /// Safe warnings emitted while building the result.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Batch-level counts projected into the manifest.
/// </summary>
public sealed record BatchManifestSummary
{
    /// <summary>
    /// Number of accepted image records.
    /// </summary>
    public int ImageCount { get; init; }

    /// <summary>
    /// Number of accepted Excel records.
    /// </summary>
    public int ExcelCount { get; init; }

    /// <summary>
    /// Number of accepted zip records.
    /// </summary>
    public int ZipCount { get; init; }

    /// <summary>
    /// Number of OK renamed outputs.
    /// </summary>
    public int OkRenamed { get; init; }

    /// <summary>
    /// Number of KO records.
    /// </summary>
    public int KoRecords { get; init; }
}
