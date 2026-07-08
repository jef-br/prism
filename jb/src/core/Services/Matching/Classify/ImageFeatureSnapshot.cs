using System.Text.Json.Serialization;

namespace Prism.Core;

/// <summary>
/// Collected ImageFeature measurements for one canonical image after the Classified stage.
/// Feature ids match <c>jb/docs/ImageNGP/ImageFeatures.md</c>.
/// Values are stored as strings for interoperability with the JSON-driven
/// <c>ImageRoles.json</c> phenotype rule evaluator. The snapshot round-trips through System.Text.Json
/// so it can be persisted as part of a LAMBDA document and reloaded by any downstream service.
/// </summary>
public sealed class ImageFeatureSnapshot
{
    private readonly Dictionary<string, ImageFeatureValue> features;

    /// <summary>Creates an empty snapshot.</summary>
    public ImageFeatureSnapshot()
        => features = new Dictionary<string, ImageFeatureValue>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Rehydrates a snapshot from a persisted LAMBDA document. Matches the <see cref="All"/> property
    /// by name so System.Text.Json restores every measured feature.
    /// </summary>
    [JsonConstructor]
    public ImageFeatureSnapshot(IReadOnlyDictionary<string, ImageFeatureValue>? all)
        => features = all is null
            ? new Dictionary<string, ImageFeatureValue>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, ImageFeatureValue>(all, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Records a measured feature value, overwriting any previous value for the same feature id.
    /// </summary>
    /// <param name="featureId">Feature id as defined in ImageFeatures.md (kebab-case).</param>
    /// <param name="value">Measured value string. Pass <c>"UNKNOWN"</c> when not determinable.</param>
    /// <param name="confidence">Detector confidence [0.0–1.0].</param>
    /// <param name="source">Measurement source identifier.</param>
    public void Set(string featureId, string value, double confidence, string source)
        => features[featureId] = new ImageFeatureValue { Value = value, Confidence = confidence, Source = source };

    /// <summary>
    /// Returns the value string for a feature, or <c>"UNKNOWN"</c> when the feature
    /// has not been measured. This is the contract used by the phenotype rule evaluator.
    /// </summary>
    public string GetValue(string featureId)
        => features.TryGetValue(featureId, out ImageFeatureValue? v) ? v.Value : "UNKNOWN";

    /// <summary>
    /// Returns true and populates <paramref name="value"/> when the feature has been set.
    /// </summary>
    public bool TryGet(string featureId, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ImageFeatureValue? value)
        => features.TryGetValue(featureId, out value);

    /// <summary>All measured features, keyed by feature id.</summary>
    public IReadOnlyDictionary<string, ImageFeatureValue> All => features;
}
