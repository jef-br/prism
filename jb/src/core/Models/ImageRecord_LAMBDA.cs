/*
Represents one canonical image through the definitive route:
imported, classified, matched, ordered, renamed, generated, transformed, exported.
*/

/// <summary>
/// Represents one canonical image as it travels through all eight PRISM pipeline stages.
/// Produced by the Classified stage; extended by each subsequent stage.
/// </summary>
public class ImageRecord_LAMBDA : ImageRecord_Base
{
    // -------------------------------------------------------------------------
    // Classification outputs — populated by the Classified stage
    // -------------------------------------------------------------------------

    /// <summary>
    /// All measured ImageFeatures for this image.
    /// Set by <c>ImageFeatureAnalyzer</c> and the CLIP classifier.
    /// </summary>
    public ImageFeatureSnapshot Features { get; set; } = new();

    /// <summary>
    /// CLIP classification tokens attached during the Classified stage.
    /// Influential tokens drove the phenotype assignment; Trivial tokens are below-threshold.
    /// </summary>
    public TagCollection Tags { get; set; } = new();

    /// <summary>
    /// The hard-assigned phenotype id, or null when no phenotype rule matched.
    /// Null images are handled by deterministic fallback in the Ordered stage.
    /// </summary>
    public string? SelectedPhenotype { get; set; }

    /// <summary>
    /// All phenotype ids whose conditions were met, in evaluation order.
    /// The first entry equals <see cref="SelectedPhenotype"/> when non-empty.
    /// </summary>
    public string[] CandidatePhenotypes { get; set; } = [];

    // -------------------------------------------------------------------------
    // Matching outputs — populated by the Matched stage
    // -------------------------------------------------------------------------

    /// <summary>
    /// Bounded matching decision produced by the Matched stage.
    /// Null until the Matched stage completes for this record.
    /// </summary>
    public MatchEvidence? MatchEvidence { get; set; }

    // -------------------------------------------------------------------------
    // KO tracking — set by any stage when the image is rejected
    // -------------------------------------------------------------------------

    /// <summary>True when this image has been rejected by any stage.</summary>
    public bool IsKo { get; set; }

    /// <summary>Machine-readable rejection code (e.g. "VISUAL_DUPLICATE").</summary>
    public string? KoReasonCode { get; set; }

    /// <summary>Human-readable safe rejection message for the manifest.</summary>
    public string? KoSafeMessage { get; set; }
}

/// <summary>
/// CLIP classification token collection attached to an image during the Classified stage.
/// </summary>
public sealed record TagCollection
{
    /// <summary>Tokens whose cosine similarity score exceeded the influential threshold.</summary>
    public ClassificationToken[] Influential { get; init; } = [];

    /// <summary>Tokens below the influential threshold, retained for diagnostics.</summary>
    public ClassificationToken[] Trivial { get; init; } = [];
}
