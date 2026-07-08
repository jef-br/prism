namespace Prism.Lib.Excel;

/// <summary>
/// Worksheet area scanned for a likely header row.
/// </summary>
public sealed record HeaderRowSearchSpace
{
    /// <summary>
    /// Zero-based first row to inspect.
    /// </summary>
    public int FirstRow { get; init; }

    /// <summary>
    /// Zero-based last row to inspect.
    /// </summary>
    public int LastRow { get; init; }

    /// <summary>
    /// Zero-based first column to inspect.
    /// </summary>
    public int FirstColumn { get; init; }

    /// <summary>
    /// Zero-based last column to inspect.
    /// </summary>
    public int LastColumn { get; init; }

    /// <summary>
    /// Validates the configured worksheet search bounds.
    /// </summary>
    public void Validate()
    {
        if (FirstRow < 0 || LastRow < FirstRow)
        {
            throw new InvalidOperationException("ExcelConfig.HeaderRowSearchSpace rows must be zero-based and ordered.");
        }

        if (FirstColumn < 0 || LastColumn < FirstColumn)
        {
            throw new InvalidOperationException("ExcelConfig.HeaderRowSearchSpace columns must be zero-based and ordered.");
        }
    }
}
