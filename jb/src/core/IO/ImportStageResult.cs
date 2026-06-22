namespace Prism.Core;

/// <summary>
/// Structured result returned by <see cref="Importer"/> after the Imported stage completes.
/// Carries the normalized image records, the Internal Excel Model family records, and any
/// KO records produced during import.
/// </summary>
public sealed record ImportStageResult
{
    /// <summary>
    /// Successfully imported and normalized image records ready for classification.
    /// </summary>
    public IReadOnlyList<ImageRecord_INPUT> NormalizedImages { get; init; } = [];

    /// <summary>
    /// Family records built from all accepted Excel workbooks via the Internal Excel Model.
    /// </summary>
    public IReadOnlyList<FamilyIDRecord> FamilyRecords { get; init; } = [];

    /// <summary>
    /// Excel processing diagnostics emitted during IEM construction.
    /// </summary>
    public IReadOnlyList<ExcelProcessingDiagnostic> ExcelDiagnostics { get; init; } = [];

    /// <summary>
    /// KO records for images that could not be imported or normalized.
    /// These are projected into the manifest and do not stop the batch.
    /// </summary>
    public IReadOnlyList<ImportKoRecord> ImageKoRecords { get; init; } = [];

    /// <summary>
    /// Zip KO records for zip members that could not be extracted or decoded.
    /// </summary>
    public IReadOnlyList<ZipMemberKoRecord> ZipKoRecords { get; init; } = [];

    /// <summary>
    /// Absolute path to the job temp folder used during import. Cleaned up after export.
    /// </summary>
    public string JobTempFolder { get; init; } = string.Empty;
}
