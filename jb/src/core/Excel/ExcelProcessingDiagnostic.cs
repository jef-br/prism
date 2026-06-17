/// <summary>
/// Safe diagnostic emitted for worksheet and row issues during Excel parsing.
/// </summary>
public sealed record ExcelProcessingDiagnostic(
    ExcelDiagnosticSeverity Severity,
    string ReasonCode,
    string Message,
    string SourceFile,
    string WorksheetName,
    int? RowNumber,
    string? ColumnName,
    string? ItemID)
{
    /// <summary>
    /// Creates a worksheet-level KO diagnostic.
    /// </summary>
    /// <param name="reasonCode">Stable reason code.</param>
    /// <param name="message">Safe diagnostic message.</param>
    /// <param name="worksheet">Source worksheet.</param>
    /// <returns>A worksheet-level diagnostic.</returns>
    public static ExcelProcessingDiagnostic WorksheetKo(string reasonCode, string message, ExcelWorksheet worksheet)
    {
        return new ExcelProcessingDiagnostic(
            ExcelDiagnosticSeverity.Error,
            reasonCode,
            message,
            worksheet.SourceFile,
            worksheet.Name,
            null,
            null,
            null);
    }

    /// <summary>
    /// Creates a worksheet-level warning diagnostic.
    /// </summary>
    /// <param name="reasonCode">Stable reason code.</param>
    /// <param name="message">Safe diagnostic message.</param>
    /// <param name="worksheet">Source worksheet.</param>
    /// <param name="columnName">Optional source column name.</param>
    /// <returns>A worksheet-level diagnostic.</returns>
    public static ExcelProcessingDiagnostic WorksheetWarning(
        string reasonCode,
        string message,
        ExcelWorksheet worksheet,
        string? columnName = null)
    {
        return new ExcelProcessingDiagnostic(
            ExcelDiagnosticSeverity.Warning,
            reasonCode,
            message,
            worksheet.SourceFile,
            worksheet.Name,
            null,
            columnName,
            null);
    }

    /// <summary>
    /// Creates a row-level KO diagnostic.
    /// </summary>
    /// <param name="reasonCode">Stable reason code.</param>
    /// <param name="message">Safe diagnostic message.</param>
    /// <param name="worksheet">Source worksheet.</param>
    /// <param name="zeroBasedRowIndex">Zero-based row index.</param>
    /// <param name="itemID">Problematic row primary-key value when available.</param>
    /// <returns>A row-level diagnostic.</returns>
    public static ExcelProcessingDiagnostic RowKo(
        string reasonCode,
        string message,
        ExcelWorksheet worksheet,
        int zeroBasedRowIndex,
        string? itemID)
    {
        return new ExcelProcessingDiagnostic(
            ExcelDiagnosticSeverity.Error,
            reasonCode,
            message,
            worksheet.SourceFile,
            worksheet.Name,
            zeroBasedRowIndex + 1,
            null,
            itemID);
    }
}
