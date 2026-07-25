using System.Text.Json.Serialization;

namespace Prism.Services.Matching;

/// <summary>
/// A single condition within a phenotype rule, as loaded from <c>ImageRoles.json</c>.
/// Either a direct feature comparison (when <see cref="Feature"/> is set)
/// or an OR group (when <see cref="AnyOf"/> is set).
/// </summary>
public sealed class FeatureCondition {
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
    public bool IsAnyOfGroup => this.AnyOf is { Length: > 0 };
}
