/// <summary>
/// Duplicate column handling thresholds from ExcelConfig.json.
/// </summary>
public sealed record DuplicateColumnHandlingConfig
{
    /// <summary>
    /// Minimum cell-overlap ratio used to merge columns with different headers.
    /// </summary>
    public double OverlapRatioForMerge { get; init; }

    /// <summary>
    /// Validates duplicate column thresholds.
    /// </summary>
    public void Validate()
    {
        if (OverlapRatioForMerge <= 0 || OverlapRatioForMerge > 1)
        {
            throw new InvalidOperationException("ExcelConfig.DuplicateColumnHandling.OverlapRatioForMerge must be greater than zero and less than or equal to one.");
        }
    }
}
