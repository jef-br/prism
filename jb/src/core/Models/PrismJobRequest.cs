namespace Prism.Contracts;

/// <summary>
/// Structured PRISM job request after API ingress has normalized caller inputs into core-facing records.
/// </summary>
public sealed record PrismJobRequest
{
    /// <summary>
    /// PRISM-owned internal job identifier.
    /// </summary>
    public Guid JobID { get; init; }

    /// <summary>
    /// Optional caller token echoed for correlation but never used as the internal job ID.
    /// </summary>
    public string? ClientRequestToken { get; init; }

    /// <summary>
    /// Accepted image input records.
    /// </summary>
    public IReadOnlyList<ImageRecord_INPUT> ImageRecords { get; init; } = [];

    /// <summary>
    /// Accepted Excel input records.
    /// </summary>
    public IReadOnlyList<InputExcelFileRecord> ExcelRecords { get; init; } = [];

    /// <summary>
    /// Accepted zip input records.
    /// </summary>
    public IReadOnlyList<InputZipFileRecord> ZipFileRecords { get; init; } = [];

    /// <summary>
    /// Caller-selected processing parameters.
    /// </summary>
    public PrismProcessingParameters? PrismProcessingParameters { get; init; }
}
