using System.Text.Json;

namespace Prism.Core;

/// <summary>
/// Typed loader for MatchingConfig.json — matcher strategies, field names, weights, and thresholds.
/// </summary>
public sealed record MatchingConfig
{
    /// <summary>All configured matching rules.</summary>
    public IReadOnlyList<MatchingRule> Rules { get; init; } = [];

    /// <summary>Rules that drive numeric token matching (familyID, EAN).</summary>
    public IReadOnlyList<MatchingRule> NumericRules =>
        Rules.Where(r => r.Type.Equals("numeric", StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>Rules that drive classification label evidence (ProductColor, ProductType, etc.).</summary>
    public IReadOnlyList<MatchingRule> LabelRules =>
        Rules.Where(r => r.Strategy.Equals("ImageLabelingMatcher", StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>
    /// Loads matching configuration from MatchingConfig.json.
    /// </summary>
    /// <param name="configPath">Absolute path to MatchingConfig.json.</param>
    /// <returns>The parsed configuration.</returns>
    public static MatchingConfig Load(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
            throw new ArgumentException("Matching config path is required.", nameof(configPath));

        if (!File.Exists(configPath))
            throw new FileNotFoundException("MatchingConfig.json was not found.", configPath);

        string json = File.ReadAllText(configPath);
        MatchingConfig? config = JsonSerializer.Deserialize<MatchingConfig>(json, JsonOptions);

        return config ?? throw new InvalidOperationException("MatchingConfig.json could not be parsed.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

/// <summary>
/// One rule in MatchingConfig.json, mapping an Excel field to a matching strategy.
/// </summary>
public sealed record MatchingRule
{
    /// <summary>The Excel column name this rule targets, or "ALL" for label-overlap rules.</summary>
    public string ExcelField { get; init; } = string.Empty;

    /// <summary>Rule type: "numeric", "string", or "image_labels".</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Matcher strategy name: "NumericalMatcher", "ImageLabelingMatcher".</summary>
    public string Strategy { get; init; } = string.Empty;

    /// <summary>Evidence weight in [0, 1] — higher means stronger influence on confidence.</summary>
    public double Weight { get; init; } = 1.0;

    /// <summary>
    /// Maximum allowable TCD (Tokenized Concatenation Distance) for numeric bracket 2.
    /// </summary>
    public double MaxDistance { get; init; } = 1.0;

    /// <summary>Maximum number of candidates retained per image.</summary>
    public string Candidates { get; init; } = "3";

    /// <summary>Minimum label overlap count for ALL image_labels rules.</summary>
    public int Overlap { get; init; } = 0;
}
