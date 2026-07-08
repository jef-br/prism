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

/// <summary>
/// Core-facing metadata for one accepted Excel workbook.
/// </summary>
public sealed record InputExcelFileRecord
{
    /// <summary>
    /// Safe source filename or remote reference.
    /// </summary>
    public string SourceReference { get; init; } = string.Empty;

    /// <summary>
    /// Accepted byte length when known.
    /// </summary>
    public long? ByteLength { get; init; }

    /// <summary>
    /// Absolute path to a job-temp copy of the file when the API or caller has spilled the
    /// uploaded bytes to disk. When populated, the Imported stage reads from this path.
    /// When null, <see cref="SourceReference"/> is treated as the readable local file path.
    /// </summary>
    public string? TempFilePath { get; init; }
}

/// <summary>
/// Core-facing metadata for one accepted zip archive.
/// </summary>
public sealed record InputZipFileRecord
{
    /// <summary>
    /// Safe source filename or remote reference.
    /// </summary>
    public string SourceReference { get; init; } = string.Empty;

    /// <summary>
    /// Accepted byte length when known.
    /// </summary>
    public long? ByteLength { get; init; }

    /// <summary>
    /// Absolute path to a job-temp copy of the archive when the API or caller has spilled the
    /// uploaded bytes to disk. When populated, the Imported stage reads from this path.
    /// When null, <see cref="SourceReference"/> is treated as the readable local file path.
    /// </summary>
    public string? TempFilePath { get; init; }
}
