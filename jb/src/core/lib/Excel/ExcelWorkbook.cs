using System.Collections.Generic;

namespace Prism.Lib.Excel;

/// <summary>
/// Workbook data loaded from one Excel-like source file.
/// </summary>
public sealed record ExcelWorkbook(string SourceFile, IReadOnlyList<ExcelWorksheet> Worksheets);
