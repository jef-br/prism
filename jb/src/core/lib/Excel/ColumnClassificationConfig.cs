namespace Prism.Lib.Excel;

/// <summary>
/// Dynamic column classification thresholds from ExcelConfig.json.
/// </summary>
public sealed record ColumnClassificationConfig {
    /// <summary>
    /// Maximum unique non-empty values for a string column to be considered categorical.
    /// </summary>
    public int CategoricalMaximumUniqueValues { get; init; }

    /// <summary>
    /// Maximum value length for a string column to be considered categorical.
    /// </summary>
    public int CategoricalMaximumValueLength { get; init; }

    /// <summary>
    /// Validates column classification thresholds.
    /// </summary>
    public void Validate() {
        if (this.CategoricalMaximumUniqueValues <= 0) {
            throw new PrismConfigurationException("ExcelConfig.ColumnClassification.CategoricalMaximumUniqueValues must be greater than zero.");
        }

        if (this.CategoricalMaximumValueLength <= 0) {
            throw new PrismConfigurationException("ExcelConfig.ColumnClassification.CategoricalMaximumValueLength must be greater than zero.");
        }
    }
}
