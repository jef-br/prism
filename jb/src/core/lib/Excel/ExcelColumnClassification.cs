namespace Prism.Contracts;

/// <summary>
/// Dynamic classification assigned to an accepted Excel column.
/// </summary>
public enum ExcelColumnClassification {
    /// <summary>
    /// The FamilyID column — the configured primary key of an Excel record.
    /// </summary>
    FamilyID,

    /// <summary>
    /// All useful values are numeric.
    /// </summary>
    Numerical,

    /// <summary>
    /// Short, low-cardinality string values.
    /// </summary>
    Categorical,

    /// <summary>
    /// Longer or high-cardinality string values.
    /// </summary>
    Descriptive,

    /// <summary>
    /// Values contain both letters and digits.
    /// </summary>
    Mixed
}
