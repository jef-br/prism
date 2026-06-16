using System.Text.Json.Serialization;

/// <summary>
/// A single condition within a phenotype rule, as loaded from <c>ImageRoles.json</c>.
/// Either a direct feature comparison (when <see cref="Feature"/> is set)
/// or an OR group (when <see cref="AnyOf"/> is set).
/// </summary>
public sealed class FeatureCondition
{
    /// <summary>Feature id to evaluate (kebab-case, matching ImageFeatures.md).</summary>
    [JsonPropertyName("feature")]
    public string? Feature { get; init; }

    /// <summary>Required value — case-insensitive equality match.</summary>
    [JsonPropertyName("equals")]
    public string? EqualTo { get; init; }

    /// <summary>Required values — image qualifies when its value matches any member.</summary>
    [JsonPropertyName("in")]
    public string[]? In { get; init; }

    /// <summary>Minimum numeric value (inclusive). Feature value must parse as a number.</summary>
    [JsonPropertyName("min")]
    public double? Min { get; init; }

    /// <summary>Maximum numeric value (inclusive). Feature value must parse as a number.</summary>
    [JsonPropertyName("max")]
    public double? Max { get; init; }

    /// <summary>
    /// OR group: at least one child condition must be met.
    /// When present, <see cref="Feature"/> and all comparators are ignored.
    /// </summary>
    [JsonPropertyName("anyOf")]
    public FeatureCondition[]? AnyOf { get; init; }

    /// <summary>True when this condition is an OR group rather than a direct feature comparison.</summary>
    [JsonIgnore]
    public bool IsAnyOfGroup => AnyOf is { Length: > 0 };
}

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

/// <summary>
/// Top-level structure of <c>ImageRoles.json</c>.
/// </summary>
public sealed class ImageRolesConfig
{
    /// <summary>
    /// Ordered list of phenotype rules. Evaluation stops at the first matching rule.
    /// Order in this list determines priority: more specific phenotypes come first.
    /// </summary>
    [JsonPropertyName("phenotypes")]
    public PhenotypeRule[] Phenotypes { get; init; } = [];
}
