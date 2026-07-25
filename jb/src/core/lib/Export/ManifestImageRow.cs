namespace Prism.Contracts;

/// <summary>
/// One row in the batch manifest — one entry per image processed by the pipeline.
/// </summary>
public sealed record ManifestImageRow {
    /// <summary>Original import filename (InitialFullName).</summary>
    public string SourceReference { get; init; } = string.Empty;

    /// <summary>Output filename in the form <c>{Family}_det{DetOrder}.jpg</c>. Null for KO images.</summary>
    public string? FinalFileName { get; init; }

    /// <summary>"Ok" or "Ko".</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Machine-readable rejection code. Null for Ok images.</summary>
    public string? KoReasonCode { get; init; }

    /// <summary>Human-readable safe rejection message. Null for Ok images.</summary>
    public string? KoSafeMessage { get; init; }

    /// <summary>Matched FamilyID. Null when no family was resolved (KO at or before Matched stage).</summary>
    public string? FamilyId { get; init; }

    /// <summary>Name of the matcher bracket that assigned the FamilyID. Null for KO images.</summary>
    public string? MatchedBy { get; init; }

    /// <summary>Zero-based det-slot index. Null for KO images.</summary>
    public int? DetOrder { get; init; }

    /// <summary>Name of the transformer strategy applied. Null when not transformed.</summary>
    public string? TransformerType { get; init; }

    /// <summary>TransformationStatus enum name. Null when not transformed.</summary>
    public string? TransformationStatus { get; init; }
}
