namespace Prism.Core;

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
    /// Salient object bounding box in pixel coordinates, detected during preprocessing.
    /// Null when detection failed or produced an area below the minimum threshold.
    /// </summary>
    public BoundingBox? BoundingBox { get; set; }

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
    // Ordering outputs — populated by the Ordered stage
    // -------------------------------------------------------------------------

    /// <summary>
    /// Det-slot assignment and tie-breaking evidence produced by the Ordered stage.
    /// Null until the Ordered stage completes for this record.
    /// </summary>
    public OrderEvidence? OrderEvidence { get; set; }

    /// <summary>
    /// Product type identifier resolved by the Ordered stage (e.g. <c>"clothing-tops"</c>).
    /// Used by the Transformed stage to apply product-type-specific det-slot exclusion rules.
    /// Null until the Ordered stage completes for this record.
    /// </summary>
    public string? ProductTypeId { get; set; }

    // -------------------------------------------------------------------------
    // KO tracking — set by any stage when the image is rejected
    // -------------------------------------------------------------------------

    /// <summary>True when this image has been rejected by any stage.</summary>
    public bool IsKo { get; set; }

    // -------------------------------------------------------------------------
    // Generation outputs — populated by the Generated stage
    // -------------------------------------------------------------------------

    /// <summary>Outcome of the generation route decision for this image's family.</summary>
    public GenerationRouteState GenerationRouteState { get; set; } = GenerationRouteState.NotEvaluated;

    /// <summary>Generated child records created from this image as the hero source.</summary>
    public IReadOnlyList<ImageRecord_GENERATED> GeneratedChildren { get; set; } = [];

    // -------------------------------------------------------------------------
    // Transformation outputs — populated by the Transformed stage
    // -------------------------------------------------------------------------

    /// <summary>
    /// Preprocessed and transformed image bytes, held in memory from the Transform stage
    /// until the Export stage writes them once. Null when the image was KO'd or transform
    /// is disabled (Exporter falls back to NormalizedJpgPath in those cases).
    /// </summary>
    public byte[]? ProcessedBytes { get; set; }

    /// <summary>
    /// Outcome of the transform routing and pixel processing for this image.
    /// Null until the Transformed stage evaluates this record.
    /// </summary>
    public ImageTransformationResult? TransformationResult { get; set; }

    // -------------------------------------------------------------------------
    // Export outputs — populated by the Exported stage
    // -------------------------------------------------------------------------

    /// <summary>
    /// Export metadata attached by the Exported stage for non-KO images.
    /// Null for KO images or until the Exported stage completes.
    /// </summary>
    public ImageRecord_OUTPUT? OutputRecord { get; set; }
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