namespace Prism.Lib.Excel;

/// <summary>
/// Validation rules for the configured FamilyID primary key.
/// </summary>
public sealed record FamilyIdProperties {
    /// <summary>
    /// Indicates whether the primary key must contain digits only.
    /// </summary>
    public bool? IsNumeric { get; init; }

    /// <summary>
    /// Required primary-key length.
    /// </summary>
    public int Length { get; init; }

    /// <summary>
    /// Validates FamilyID requirements.
    /// </summary>
    public void Validate() {
        if (!this.IsNumeric.HasValue) {
            throw new PrismConfigurationException("ExcelConfig.FamilyIDProperties.IsNumeric is required.");
        }

        if (this.Length <= 0) {
            throw new PrismConfigurationException("ExcelConfig.FamilyIDProperties.Length must be greater than zero.");
        }
    }
}
