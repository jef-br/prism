namespace Prism.Core;

/// <summary>
/// Result returned after building the Internal Excel Model.
/// </summary>
public sealed record ExcelModelBuildResult(
    InternalExcelModel Model,
    IReadOnlyList<ExcelProcessingDiagnostic> Diagnostics)
{
    /// <summary>
    /// FamilyRecord projection consumed by downstream matching.
    /// </summary>
    public IReadOnlyList<FamilyRecord> FamilyRecords => Model.ToFamilyRecords();
}
