namespace Prism.Core;

/// <summary>
/// Bounded matching decision and evidence for one image produced by the Matched stage.
/// Immutable; produced by <see cref="ImageMatcher"/> and embedded on <see cref="ImageRecord_LAMBDA"/>.
/// </summary>
public sealed record MatchEvidence
{
    // ─── Identity ─────────────────────────────────────────────────────────────

    /// <summary>Source image identifier (original filename stem).</summary>
    public string ImageId { get; init; } = string.Empty;

    /// <summary>Original full filename of the image.</summary>
    public string SourceFilename { get; init; } = string.Empty;

    // ─── Match outcome ────────────────────────────────────────────────────────

    /// <summary>Accepted FamilyID when matching succeeded; null when the image is KO.</summary>
    public string? FinalFamilyId { get; init; }

    /// <summary>Confidence score in [0, 1] for the accepted match. Zero when IsKo is true.</summary>
    public double FinalScore { get; init; }

    /// <summary>True when the image could not be matched to any FamilyID.</summary>
    public bool IsKo { get; init; }

    /// <summary>Machine-readable reason code when IsKo is true (e.g. MATCH_NOT_FOUND, MATCH_TIE).</summary>
    public string? KoReason { get; init; }

    // ─── Tie detection ────────────────────────────────────────────────────────

    /// <summary>True when the image was a candidate for multiple FamilyIDs that could not be resolved.</summary>
    public bool TieDetected { get; init; }

    /// <summary>All FamilyIDs that tied when TieDetected is true.</summary>
    public IReadOnlyList<string> TieFamilyIds { get; init; } = [];

    // ─── Matcher attribution ──────────────────────────────────────────────────

    /// <summary>Name of the matcher that produced the accepted match (e.g. NumericMatcher.Bracket1).</summary>
    public string? AcceptedMatcherName { get; init; }

    // ─── Candidate evidence ───────────────────────────────────────────────────

    /// <summary>Bounded list of top candidate FamilyIDs considered during matching.</summary>
    public IReadOnlyList<CandidateSummary> TopCandidates { get; init; } = [];

    // ─── Token evidence ───────────────────────────────────────────────────────

    /// <summary>Numeric token pairs that contributed evidence (filename token ↔ Excel family token).</summary>
    public IReadOnlyList<TokenEvidenceItem> NumericTokenEvidence { get; init; } = [];

    /// <summary>String token pairs that contributed evidence.</summary>
    public IReadOnlyList<TokenEvidenceItem> StringTokenEvidence { get; init; } = [];

    /// <summary>CLIP classification label evidence matched against MatchingConfig label rules.</summary>
    public IReadOnlyList<LabelEvidenceItem> ClassificationLabelEvidence { get; init; } = [];

    // ─── Image context ────────────────────────────────────────────────────────

    /// <summary>Phenotype and key feature summary for this image (e.g. "phenotype=hero_front").</summary>
    public string? ImageNgpSummary { get; init; }

    // ─── Human-readable output ────────────────────────────────────────────────

    /// <summary>Human-readable explanation of the matching decision. Contains no internal identifiers.</summary>
    public string SafeExplanation { get; init; } = string.Empty;
}

/// <summary>One candidate FamilyID considered during matching.</summary>
public sealed record CandidateSummary(
    string FamilyId,
    double Score,
    string MatcherName);

/// <summary>Paired token evidence linking one image token to one Excel family token.</summary>
public sealed record TokenEvidenceItem(
    string FilenameToken,
    string FamilyToken,
    string PropertyName,
    string FamilyId,
    double Score);

/// <summary>One CLIP classification label matched against a MatchingConfig label rule.</summary>
public sealed record LabelEvidenceItem(
    string Label,
    string PropertyName,
    string FamilyId,
    double Weight,
    double Confidence);
