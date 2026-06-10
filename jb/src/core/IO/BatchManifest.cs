/*
Represents the batch-level manifest projected into zip and JSON output.
*/

/// <summary>
/// Batch-level manifest projected into zip and JSON output.
/// </summary>
public sealed record BatchManifest
{
    /// <summary>
    /// PRISM-owned job identifier when a job has been created.
    /// </summary>
    public Guid? JobID { get; init; }
}
