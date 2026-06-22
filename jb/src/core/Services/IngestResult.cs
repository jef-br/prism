namespace Prism.Core;

/// <summary>
/// What the Ingest service hands forward: every input image normalized to a flat JPEG on the local
/// job folder, the FamilyRecords parsed from Excel, and the counts/warnings the manifest will need.
/// Replaces the import-related fields that used to live on <c>PipelineContext</c>.
/// </summary>
public sealed record IngestResult
{
    /// <summary>PRISM-owned job identifier, carried forward so downstream services can emit progress
    /// and persist per-image documents without re-reading the original request.</summary>
    public required Guid JobID { get; init; }

    /// <summary>Caller-selected processing parameters, carried forward so the Generate and Transform
    /// steps can read their enable flags and Export can read the output format.</summary>
    public required PrismProcessingParameters Parameters { get; init; }

    /// <summary>Normalized image records written to the local job folder, ready for matching.</summary>
    public required IReadOnlyList<ImageRecord_INPUT> NormalizedImages { get; init; }

    /// <summary>Family records built from the Internal Excel Model.</summary>
    public required IReadOnlyList<FamilyIDRecord> FamilyRecords { get; init; }

    /// <summary>Absolute path to the local job temp folder that holds all artifacts for this job.</summary>
    public required string JobTempFolder { get; init; }

    /// <summary>Original accepted image count (pre-normalization) — the manifest's ImageCount.</summary>
    public int OriginalImageCount { get; init; }

    /// <summary>Original accepted Excel count — the manifest's ExcelCount.</summary>
    public int OriginalExcelCount { get; init; }

    /// <summary>Original accepted zip count — the manifest's ZipCount.</summary>
    public int OriginalZipCount { get; init; }

    /// <summary>Temp path of the first Excel file, copied into the ZIP export when present.</summary>
    public string? FirstExcelTempPath { get; init; }

    /// <summary>KO records produced during import (image + zip member failures).</summary>
    public int KoRecordCount { get; init; }

    /// <summary>Safe warnings accumulated during import (e.g. Excel KO diagnostics).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
