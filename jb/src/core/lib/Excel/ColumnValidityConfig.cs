namespace Prism.Lib.Excel;

/// <summary>
/// Column validity thresholds from ExcelConfig.json.
/// </summary>
public sealed record ColumnValidityConfig
{
    /// <summary>
    /// Minimum share of data rows that must have a non-empty value for a column to survive.
    /// </summary>
    public double MinimumUsefulValueRatio { get; init; }

    /// <summary>
    /// Validates column validity thresholds.
    /// </summary>
    public void Validate()
    {
        if (MinimumUsefulValueRatio <= 0 || MinimumUsefulValueRatio > 1)
        {
            throw new InvalidOperationException("ExcelConfig.ColumnValidity.MinimumUsefulValueRatio must be greater than zero and less than or equal to one.");
        }
    }
}
