namespace Prism.Services.Matching;

/// <summary>
/// One rule in MatchingConfig.json, mapping an Excel field to a matching strategy.
/// </summary>
public sealed record MatchingRule {
    /// <summary>The Excel column name this rule targets, or "ALL" for label-overlap rules.</summary>
    public string ExcelField { get; init; } = string.Empty;

    /// <summary>Rule type: "numeric", "string", or "image_labels".</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Matcher strategy name: "NumericalMatcher", "ClipLabelEnricher".</summary>
    public string Strategy { get; init; } = string.Empty;

    /// <summary>Evidence weight in [0, 1] — higher means stronger influence on confidence.</summary>
    public double Weight { get; init; } = 1.0;

    /// <summary>
    /// Maximum allowable TCD (Tokenized Concatenation Distance) for Bracket 2 in-order matching.
    /// </summary>
    public double MaxDistance { get; init; } = 1.0;

    /// <summary>
    /// Maximum TCD for Bracket 2 permuted (any token subset, any order) matching.
    /// When 0 (default), permuted matching is disabled for this rule.
    /// </summary>
    public double MaxDistancePermuted { get; init; } = 0.0;

    /// <summary>Maximum number of candidates retained per image.</summary>
    public string Candidates { get; init; } = "3";

    /// <summary>Minimum label overlap count for ALL image_labels rules.</summary>
    public int Overlap { get; init; } = 0;

    /// <summary>
    /// Comma-separated ImageFeature ids whose CLIP tags this rule may match (e.g. "product-color").
    /// Empty means every influential tag applies (legacy behavior).
    /// </summary>
    public string ClipFeature { get; init; } = string.Empty;

    /// <summary>True when a CLIP tag of <paramref name="feature"/> is eligible for this rule.</summary>
    public bool AppliesToFeature(string feature) {
        if (string.IsNullOrWhiteSpace(this.ClipFeature))
            return true;

        foreach (string allowed in this.ClipFeature.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            if (allowed.Equals(feature, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
