using System.Collections.Generic;

namespace Prism.Lib.Excel;

/// <summary>
/// One zero-based worksheet row.
/// </summary>
public sealed record ExcelWorksheetRow(int RowIndex, IReadOnlyList<string> Cells);
