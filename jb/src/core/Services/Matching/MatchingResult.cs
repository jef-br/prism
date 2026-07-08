namespace Prism.Services.Matching;

/// <summary>
/// What the Matching service hands forward: the ImageRecord_LAMBDA collection it produced by
/// converting every normalized ImageRecord_INPUT (classify → match → order → rename), plus the
/// counts those stages accumulated. Carries <see cref="Ingest"/> forward so downstream services
/// and the Export step can still reach the normalized images and original input counts without a
/// shared mutable context.
/// </summary>
public sealed record MatchingResult
{
    /// <summary>The ingest output this matching pass consumed — carried forward for Export.</summary>
    public required IngestResult Ingest { get; init; }

    /// <summary>One LAMBDA document per normalized image, enriched with match/order/rename evidence.</summary>
    public required List<ImageRecord_LAMBDA> LambdaRecords { get; init; }

    /// <summary>Images successfully renamed (FamilyID_det#) by the Renamed step.</summary>
    public int OkRenamedCount { get; init; }

    /// <summary>KO records created during matching (classification, dedup, match, rename collisions).</summary>
    public int KoRecordCount { get; init; }

    /// <summary>Visual duplicates suppressed during classification.</summary>
    public int DuplicatesRemoved { get; init; }

    /// <summary>Images that received a hard phenotype assignment during classification.</summary>
    public int PhenotypeAssignedCount { get; init; }

    /// <summary>Safe warnings raised during matching (e.g. CLIP classification degraded for some images).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
