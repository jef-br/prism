namespace Prism.Contracts;

/// <summary>
/// Processing options selected by the caller for one PRISM job.
/// </summary>
public sealed record PrismProcessingParameters
{
    /// <summary>
    /// Whether output images should be renamed with the matched FamilyID and det order.
    /// </summary>
    public bool Rename { get; init; } = true;

    /// <summary>
    /// Whether output images should pass through transformation.
    /// </summary>
    public bool Transform { get; init; } = true;

    /// <summary>
    /// Whether missing det slots may be generated when source quality allows it.
    /// </summary>
    public bool Generation { get; init; } = true;

    /// <summary>
    /// Requested output format.
    /// </summary>
    public string Format { get; init; } = "zip";

    /// <summary>
    /// Whether original image bytes should be returned outside the manifest.
    /// </summary>
    public bool ReturnOriginalImages { get; init; }

    /// <summary>
    /// When true, skips CLIP tag inference entirely. FeatureAnalysis still runs (required for ordering).
    /// Use for matching-focused test runs where classification output is not needed.
    /// </summary>
    public bool SkipClassification { get; init; }

    /// <summary>
    /// When true, Tx_CenterAndStretch removes detected human heads at the nose-to-lips boundary
    /// before centering. Has no effect when no face is detected.
    /// </summary>
    public bool Headcut { get; init; }
}
