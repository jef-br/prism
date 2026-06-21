namespace Prism.Core;

/// <summary>
/// Det-slot assignment decision and tie-breaking evidence produced by the Ordered stage.
/// Immutable; produced by <see cref="ImageOrderer"/> and embedded on <see cref="ImageRecord_LAMBDA"/>.
/// </summary>
public sealed record OrderEvidence
{
    /// <summary>Zero-based det slot index assigned to this image.</summary>
    public int AssignedDetSlot { get; init; }

    /// <summary>The winning phenotype id that qualified for the slot. Null when IsOverflow is true.</summary>
    public string? WinningPhenotype { get; init; }

    /// <summary>Zero-based rank of WinningPhenotype in the slot's preference list. -1 when IsOverflow is true.</summary>
    public int PhenotypeRankInSlot { get; init; }

    /// <summary>Number of non-UNKNOWN features in the image's feature snapshot. Used for NGP confidence tie-breaking.</summary>
    public int NgpConfidenceCount { get; init; }

    /// <summary>Which tie-breaker determined the winner: "ngp-confidence", "filename-hint", "source-index", or "none".</summary>
    public string TieBreakerWon { get; init; } = "none";

    /// <summary>True when the image had no qualifying phenotype for any configured slot and was assigned as overflow.</summary>
    public bool IsOverflow { get; init; }
}
