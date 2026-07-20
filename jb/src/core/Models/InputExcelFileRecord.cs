namespace Prism.Contracts;

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
