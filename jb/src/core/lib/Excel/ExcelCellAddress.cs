namespace Prism.Contracts;

/// <summary>
/// Address of a source cell that contributed to the Internal Excel Model.
/// </summary>
public sealed record ExcelCellAddress(
    string SourceFile,
    string WorksheetName,
    int RowNumber,
    int ColumnNumber,
    string HeaderName);
