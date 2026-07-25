namespace Prism.Lib.Excel;

/// <summary>
/// Worksheet area scanned for a likely header row.
/// </summary>
public sealed record HeaderRowSearchSpace {
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
    public void Validate() {
        if (this.FirstRow < 0 || this.LastRow < this.FirstRow) {
            throw new PrismConfigurationException("ExcelConfig.HeaderRowSearchSpace rows must be zero-based and ordered.");
        }

        if (this.FirstColumn < 0 || this.LastColumn < this.FirstColumn) {
            throw new PrismConfigurationException("ExcelConfig.HeaderRowSearchSpace columns must be zero-based and ordered.");
        }
    }
}
