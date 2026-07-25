namespace Prism.Lib.Excel;

/// <summary>
/// Duplicate column handling thresholds from ExcelConfig.json.
/// </summary>
public sealed record DuplicateColumnHandlingConfig {
    /// <summary>
    /// Minimum cell-overlap ratio used to merge columns with different headers.
    /// </summary>
    public double OverlapRatioForMerge { get; init; }

    /// <summary>
    /// Validates duplicate column thresholds.
    /// </summary>
    public void Validate() {
        if (this.OverlapRatioForMerge <= 0 || this.OverlapRatioForMerge > 1) {
            throw new PrismConfigurationException("ExcelConfig.DuplicateColumnHandling.OverlapRatioForMerge must be greater than zero and less than or equal to one.");
        }
    }
}
