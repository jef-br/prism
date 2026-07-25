namespace Prism.Lib.Excel;

/// <summary>
/// Result returned after building the Internal Excel Model.
/// </summary>
public sealed record ExcelModelBuildResult(
    InternalExcelModel Model,
    IReadOnlyList<ExcelProcessingDiagnostic> Diagnostics) {
    /// <summary>
    /// FamilyIDRecord projection consumed by downstream matching.
    /// </summary>
    public IReadOnlyList<FamilyIDRecord> FamilyRecords => this.Model.ToFamilyRecords();
}
