using System.Collections.Generic;

namespace Prism.Contracts;

/// <summary>
/// One property value extracted from an Excel row, including duplicate-column source values.
/// </summary>
public sealed record ExcelPropertyValue(
    string PropertyName,
    IReadOnlyList<string> SourceValues,
    IReadOnlyList<ExcelCellAddress> SourceLocations);
