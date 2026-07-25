namespace Prism.Lib.Excel;

/// <summary>
/// Severity of a safe Excel processing diagnostic.
/// </summary>
public enum ExcelDiagnosticSeverity {
    /// <summary>
    /// Informational note.
    /// </summary>
    Info,

    /// <summary>
    /// Non-fatal warning.
    /// </summary>
    Warning,

    /// <summary>
    /// KO item or worksheet diagnostic.
    /// </summary>
    Error
}
