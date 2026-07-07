namespace Prism.Core;

/// <summary>
/// Everything the Export step needs to assemble the <see cref="BatchManifest"/> and optional ZIP,
/// gathered explicitly by the orchestrator from the final LAMBDA collection plus the accumulated
/// counts. Replaces the manifest-summary fields that used to be read off <c>PipelineContext</c>.
/// </summary>
public sealed record ExportRequest
{
    /// <summary>PRISM-owned job identifier.</summary>
    public required Guid JobID { get; init; }

    /// <summary>Final, fully-enriched LAMBDA documents — one per normalized image.</summary>
    public required IReadOnlyList<ImageRecord_LAMBDA> LambdaRecords { get; init; }

    /// <summary>Normalized images, used to resolve OK/KO artifact bytes by InitialFullName.</summary>
    public required IReadOnlyList<ImageRecord_INPUT> NormalizedImages { get; init; }

    /// <summary>Temp path of the first Excel file to include in the ZIP, when present.</summary>
    public string? FirstExcelTempPath { get; init; }

    /// <summary>Requested output format ("zip" or "json").</summary>
    public required string Format { get; init; }

    /// <summary>
    /// When false (default), Export compacts each family's det indices to a contiguous 0..n-1 range
    /// (gaps closed, relative order preserved). When true, det indices are left as the Order stage
    /// assigned them. Mirrors Output.DET-ORDER-GAPS-ALLOWED in Prism_Config.json.
    /// </summary>
    public bool DetOrderGapsAllowed { get; init; }

    /// <summary>Original accepted image count for the manifest summary.</summary>
    public int ImageCount { get; init; }

    /// <summary>Accepted Excel count for the manifest summary.</summary>
    public int ExcelCount { get; init; }

    /// <summary>Accepted zip count for the manifest summary.</summary>
    public int ZipCount { get; init; }

    /// <summary>Total images successfully renamed.</summary>
    public int OkRenamedCount { get; init; }

    /// <summary>Total KO records across import and matching.</summary>
    public int KoRecordCount { get; init; }

    /// <summary>Non-KO images that were transformed.</summary>
    public int OkTransformedCount { get; init; }

    /// <summary>Generated synthetic images created by the Generate step.</summary>
    public int GeneratedCount { get; init; }

    /// <summary>Safe warnings accumulated across the pipeline.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
