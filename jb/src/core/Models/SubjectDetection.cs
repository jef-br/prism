namespace Prism.Contracts;

/// <summary>
/// Subject-isolation result produced upstream (Classify/preprocessing, or ingress from a real alpha
/// channel) and consumed by the Transformed stage. This is the single persisted contract behind a
/// swappable producer (classical-CV detector today, a segmentation model later) — see the
/// ISubjectDetector seam. It carries the subject bounding box, an optional pixel mask, per-edge
/// intersect flags derived by the detector, and hard-shadow evidence that steers transform behavior.
/// All members are mutable so the record round-trips across the matching→transform service boundary
/// via System.Text.Json.
/// </summary>
public class SubjectDetection {
    public BoundingBox Box { get; set; }

    // Single-channel subject mask (0/255) encoded as PNG; null for a box-only producer. PNG so a
    // binary mask compresses well over the HTTP boundary and round-trips as base64.
    public byte[]? MaskPng { get; set; }

    public bool IntersectsTop { get; set; }
    public bool IntersectsBottom { get; set; }
    public bool IntersectsLeft { get; set; }
    public bool IntersectsRight { get; set; }

    // True when the detector found candidate hard-shadow edges (thin, texture-only, chroma-unsupported
    // regions). Feeds the shadow-accounting toggle in the Transformed stage.
    public bool HasHardShadowEvidence { get; set; }

    // Detector confidence in [0,1].
    public double Confidence { get; set; }

    // True when the detector found no subject and fell back to the whole frame. The Transformed stage
    // ignores the box in this case and keeps its legacy salient bbox.
    public bool IsWholeFrameFallback { get; set; }

    // Producer provenance, e.g. "alpha", "classical-cv", "yolo26s-seg".
    public string Producer { get; set; } = string.Empty;

    public int TouchedEdgeCount =>
        (this.IntersectsTop ? 1 : 0) + (this.IntersectsBottom ? 1 : 0)
        + (this.IntersectsLeft ? 1 : 0) + (this.IntersectsRight ? 1 : 0);
}
