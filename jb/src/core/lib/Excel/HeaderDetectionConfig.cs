namespace Prism.Lib.Excel;

/// <summary>
/// Header-detection thresholds from ExcelConfig.json.
/// </summary>
public sealed record HeaderDetectionConfig
{
    /// <summary>
    /// Minimum share of non-empty candidate cells that must match configured header indicators.
    /// </summary>
    public double MinimumMatchedColumnRatio { get; init; }

    /// <summary>
    /// Maximum edit-distance ratio accepted for an indicator match.
    /// </summary>
    public double MaximumEditDistanceRatio { get; init; }

    /// <summary>
    /// Confidence assigned to a one-character edit distance.
    /// </summary>
    public double EditDistanceOneConfidence { get; init; }

    /// <summary>
    /// Confidence assigned to a two-character edit distance.
    /// </summary>
    public double EditDistanceTwoConfidence { get; init; }

    /// <summary>
    /// Validates header-detection thresholds.
    /// </summary>
    public void Validate()
    {
        ValidateRatio(MinimumMatchedColumnRatio, "ExcelConfig.HeaderDetection.MinimumMatchedColumnRatio");
        ValidateRatio(MaximumEditDistanceRatio, "ExcelConfig.HeaderDetection.MaximumEditDistanceRatio");
        ValidateRatio(EditDistanceOneConfidence, "ExcelConfig.HeaderDetection.EditDistanceOneConfidence");
        ValidateRatio(EditDistanceTwoConfidence, "ExcelConfig.HeaderDetection.EditDistanceTwoConfidence");
    }

    private static void ValidateRatio(double value, string name)
    {
        if (value <= 0 || value > 1)
        {
            throw new PrismConfigurationException($"{name} must be greater than zero and less than or equal to one.");
        }
    }
}
