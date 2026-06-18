using System.Text.Json.Serialization;

/// <summary>
/// A single phenotype rule loaded from <c>ImageRoles.json</c>.
/// An image matches this rule when every condition in <see cref="Required"/> is met.
/// Phenotype assignment is always a hard assignment — no soft probability vectors.
/// </summary>
public sealed class PhenotypeRule
{
    /// <summary>Phenotype id (kebab-case), matching <c>imagePhenotypes.md</c>.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Required feature conditions. All must be met for the image to match this phenotype.
    /// A feature with value UNKNOWN does not satisfy any condition.
    /// </summary>
    [JsonPropertyName("required")]
    public FeatureCondition[] Required { get; init; } = [];
}
