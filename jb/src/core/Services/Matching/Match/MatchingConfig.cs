using System.Text.Json;

namespace Prism.Services.Matching;

/// <summary>
/// Typed loader for MatchingConfig.json — matcher strategies, field names, weights, and thresholds.
/// </summary>
public sealed record MatchingConfig
{
    /// <summary>All configured matching rules.</summary>
    public required IReadOnlyList<MatchingRule> Rules { get; init; }

    /// <summary>
    /// Minimum combined evidence score for Bracket 4 semantic matching to accept an assignment.
    /// Computed as the average of CLIP, numeric, and string signals scaled to [0, 1].
    /// </summary>
    public required double SemanticThreshold { get; init; }

    /// <summary>Weight applied to each semantic signal when computing MatchEvidence.FinalScore for Bracket 4.</summary>
    public required double SemanticWeight { get; init; }

    /// <summary>
    /// Minimum distinct filename tokens the winning family must have matched for a Bracket 3 string
    /// assignment. 1 preserves the historical behavior; 2 rejects single-common-token matches
    /// (e.g. one shared color word), trading recall for precision — Brackets 4–5 may still rescue.
    /// </summary>
    public required int Bracket3MinDistinctTokens { get; init; }

    /// <summary>
    /// Minimum digit count for a filename token or family digit target to act as standalone numeric
    /// evidence. 1 preserves the historical behavior; 5 stops shot suffixes (_01) and short RefCo
    /// digit fragments (e.g. "MGGE073" → "073") from producing false Bracket 1 ties. Shorter tokens
    /// may still participate in Bracket 2 concatenations whose combined length meets the threshold.
    /// </summary>
    public required int MinNumericTokenLength { get; init; }

    /// <summary>
    /// When true, the numeric digit index additionally covers every digit run (and capped whole-value
    /// digit string) of every family column — not just the configured numeric rule fields. Lets
    /// filenames match identifiers embedded in compound cells (e.g. label "MAN-Posy Green-1010930-60105").
    /// </summary>
    public required bool IndexDigitRunsAllColumns { get; init; }

    /// <summary>
    /// Minimum digit count for the numeric substring rescue pass (accepts the unique family whose
    /// digit target contains the filename token). 0 disables the pass.
    /// </summary>
    public required int MinSubstringRescueLength { get; init; }

    /// <summary>
    /// Minimum length for an identifier-grade filename token (contains both letters and digits,
    /// occurs in exactly one family) to accept a Bracket 3 match on its own, bypassing
    /// Bracket3MinDistinctTokens. 0 disables the bypass.
    /// </summary>
    public required int IdentifierTokenMinLength { get; init; }

    /// <summary>
    /// When true, the string token index also contains concatenations of adjacent cell tokens in
    /// both orders ("palm"+"blue" → "palmblue"/"bluepalm") and filename tokens are additionally
    /// split at letter↔digit boundaries, so glued compound tokens can match.
    /// </summary>
    public required bool IndexExcelTokenBigrams { get; init; }

    /// <summary>
    /// When true, a final bracket propagates a matched FamilyID to unmatched images whose rare
    /// filename token set points to exactly one matched sibling image (and to no image matched to
    /// a different family).
    /// </summary>
    public required bool EnableSiblingPropagation { get; init; }

    /// <summary>
    /// When true, images with a meaningless filename (1.jpg, DSCN2365.jpg, IMG_10005.png) borrow
    /// their folder's name for matching when the folder is meaningful — its siblings form a per-item
    /// pattern and one of its tokens appears in the Excel data. Format folders (HD, Web, packshot,
    /// 800 x 1200) are never borrowed.
    /// </summary>
    public required bool EnableFolderNameEnrichment { get; init; }

    /// <summary>
    /// Minimum token length (both sides) for the Bracket 3 categorical edit-distance fallback to
    /// consider a fuzzy match. Guards short 2-3 letter words from accidentally matching an unrelated
    /// short word.
    /// </summary>
    public required int FuzzyMinTokenLength { get; init; }

    /// <summary>
    /// Maximum Levenshtein edit distance for the Bracket 3 categorical fuzzy fallback — bounded
    /// tolerance for typo/regional spelling variants (e.g. "gray"/"grey").
    /// </summary>
    public required int FuzzyMaxEditDistance { get; init; }

    /// <summary>
    /// Evidence score assigned to a categorical fuzzy match — between the synonym score (0.85) and
    /// exact score (1.0).
    /// </summary>
    public required double FuzzyMatchScore { get; init; }

    /// <summary>Rules that drive numeric token matching (familyID, EAN).</summary>
    public IReadOnlyList<MatchingRule> NumericRules =>
        Rules.Where(r => r.Type.Equals("numeric", StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>Rules that drive CLIP label evidence (ProductColor, ProductType, etc.).</summary>
    public IReadOnlyList<MatchingRule> LabelRules =>
        Rules.Where(r => r.Strategy.Equals("ClipLabelEnricher", StringComparison.OrdinalIgnoreCase)).ToList();

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
        try
        {
            return JsonSerializer.Deserialize<MatchingConfig>(json, JsonOptions)
                ?? throw new PrismConfigurationException("MatchingConfig.json could not be parsed.");
        }
        catch (JsonException ex)
        {
            throw new PrismConfigurationException($"Cannot load MatchingConfig.json: {ex.Message}", ex);
        }
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
    public bool AppliesToFeature(string feature)
    {
        if (string.IsNullOrWhiteSpace(ClipFeature))
            return true;

        foreach (string allowed in ClipFeature.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (allowed.Equals(feature, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
