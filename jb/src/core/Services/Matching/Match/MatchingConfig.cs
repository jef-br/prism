using System.Text.Json;

namespace Prism.Services.Matching;

/// <summary>
/// Typed loader for MatchingConfig.json — matcher strategies, field names, weights, and thresholds.
/// </summary>
internal sealed record MatchingConfig
{
    /// <summary>The top-level "match" section — shared values plus one section per matcher.</summary>
    public required MatchSection Match { get; init; }

    /// <summary>Rules that drive numeric token matching (familyID, EAN).</summary>
    public IReadOnlyList<MatchingRule> NumericRules =>
        this.Match.Shared.Rules.Where(r => r.Type.Equals("numeric", StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>Rules that drive CLIP label evidence (ProductColor, ProductType, etc.).</summary>
    public IReadOnlyList<MatchingRule> LabelRules =>
        this.Match.Shared.Rules.Where(r => r.Strategy.Equals("ClipLabelEnricher", StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>
    /// Loads matching configuration from MatchingConfig.json.
    /// </summary>
    /// <param name="configPath">Absolute path to MatchingConfig.json.</param>
    /// <returns>The parsed configuration.</returns>
    internal static MatchingConfig Load(string configPath)
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

    /// <summary>The "match" section of MatchingConfig.json: shared values plus one section per matcher.</summary>
    internal sealed record MatchSection
    {
        /// <summary>Cross-matcher values (thresholds, weights, feature toggles, rule list).</summary>
        public required SharedSection Shared { get; init; }

        /// <summary>NumericMatcher's own tunables.</summary>
        public required NumericMatcher.Config NumericMatcher { get; init; }

        /// <summary>StringMatcher's own tunables.</summary>
        public required StringMatcher.Config StringMatcher { get; init; }

        /// <summary>SiblingPropagator's own tunables.</summary>
        public required SiblingPropagator.Config SiblingPropagator { get; init; }

        /// <summary>FolderNameEnricher's own tunables.</summary>
        public required FolderNameEnricher.Config FolderNameEnricher { get; init; }

        /// <summary>Genuinely cross-matcher values that do not belong to a single matcher.</summary>
        internal sealed record SharedSection
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
        }
    }
}
