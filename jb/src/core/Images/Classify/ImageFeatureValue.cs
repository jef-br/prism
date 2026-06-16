/// <summary>
/// A single measured image attribute value with provenance and confidence.
/// Feature ids follow <c>jb/docs/ImageNGP/ImageFeatures.md</c>.
/// </summary>
public sealed record ImageFeatureValue
{
    /// <summary>
    /// Measured value as a string. Use <c>"UNKNOWN"</c> when the detector could not reach
    /// a reliable conclusion — never default to false or an arbitrary value.
    /// Boolean features use lowercase <c>"true"</c> / <c>"false"</c>.
    /// </summary>
    public string Value { get; init; } = "UNKNOWN";

    /// <summary>
    /// Detector confidence in [0.0–1.0]. Use 1.0 for geometric invariants
    /// (e.g. border intersection derived from pixel bounds).
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// System that produced this measurement:
    /// <c>"geometry"</c>, <c>"imagesharp"</c>, <c>"onnx"</c>, <c>"heuristic"</c>.
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// True when <see cref="Value"/> is <c>"UNKNOWN"</c> (case-insensitive).
    /// Unknown features do not qualify an image for any phenotype role.
    /// </summary>
    public bool IsUnknown => string.Equals(Value, "UNKNOWN", StringComparison.OrdinalIgnoreCase);
}
