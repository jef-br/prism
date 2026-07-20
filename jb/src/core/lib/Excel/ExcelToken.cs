namespace Prism.Lib.Excel;

/// <summary>
/// One normalized Excel token retained as matching evidence.
/// </summary>
public sealed record ExcelToken(
    string TokenID,
    string FamilyID,
    string PropertyName,
    string NormalizedValue);
