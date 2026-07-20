using System.Collections.Generic;

namespace Prism.Lib.Excel;

/// <summary>
/// Worksheet data represented as ordered rows and cells.
/// </summary>
public sealed record ExcelWorksheet(string SourceFile, string Name, IReadOnlyList<ExcelWorksheetRow> Rows);
