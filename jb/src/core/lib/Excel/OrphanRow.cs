using System.Collections.Generic;

namespace Prism.Lib.Excel;

/// <summary>
/// One data row parsed from a worksheet that could not resolve a FamilyID — either the worksheet
/// has no FamilyID column at all, or the row's primary-key cell is invalid. Buffered during model
/// building so <see cref="OrphanRowJoiner"/> can attach it to an existing family via shared keys
/// once every workbook has been processed.
/// </summary>
public sealed record OrphanRow(
    string SourceFile,
    string WorksheetName,
    int RowIndex,
    IReadOnlyList<ExcelPropertyValue> PropertyValues,
    IReadOnlyDictionary<string, ExcelColumnClassification> ColumnClassifications);
