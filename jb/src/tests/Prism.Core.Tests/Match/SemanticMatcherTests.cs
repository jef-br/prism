using Xunit;

namespace PrismCoreTests.Match;

/// <summary>
/// Unit tests for <see cref="SemanticMatcher"/> — Bracket 4 (combined CLIP + numeric + string).
/// </summary>
public class SemanticMatcherTests
{
    //  Happy path

    [Fact]
    public void TryMatch_ExactlyOneCandidateSurvivesClipFilter_ReturnsEvidence()
    {
        // FAM001 carries a ProductType of "tote" that matches the image's influential CLIP tag;
        // FAM002 carries a contradicting ProductType, so only FAM001 survives Step 1.
        SemanticMatcher matcher = MakeMatcher(semanticThreshold: 0.4);
        FamilyIDRecord famA = FamilyWithProperty("FAM001", "ProductType", "tote", ExcelColumnClassification.Categorical);
        FamilyIDRecord famB = FamilyWithProperty("FAM002", "ProductType", "backpack", ExcelColumnClassification.Categorical);
        ImageRecord_LAMBDA record = MakeLambda("bag-photo.jpg", influentialLabel: "tote");

        MatchEvidence? evidence = matcher.TryMatch(record, [famA, famB], NoNumericRules, ProductTypeLabelRule);

        Assert.NotNull(evidence);
        Assert.Equal("FAM001", evidence!.FinalFamilyId);
        Assert.False(evidence.IsKo);
        Assert.Equal("SemanticMatcher.Bracket4", evidence.AcceptedMatcherName);
    }

    //  No CLIP tags at all

    [Fact]
    public void TryMatch_NoInfluentialTags_WithProductTypeRuleConfigured_ReturnsNull()
    {
        // A ProductType label rule is configured and FAM001 carries a ProductType column, but the
        // image has no influential tags — the per-dimension gate skips the CLIP filter (nothing to
        // contradict), and the sole-survivor guard then refuses to assign with no CLIP, numeric, or
        // string signal tying the image to the family.
        SemanticMatcher matcher = MakeMatcher(semanticThreshold: 0.1);
        FamilyIDRecord family = FamilyWithProperty("FAM001", "ProductType", "tote", ExcelColumnClassification.Categorical);
        ImageRecord_LAMBDA record = MakeLambda("bag-photo.jpg"); // Tags.Influential stays empty

        MatchEvidence? evidence = matcher.TryMatch(record, [family], NoNumericRules, ProductTypeLabelRule);

        Assert.Null(evidence);
    }

    //  String tie between two candidates

    [Fact]
    public void TryMatch_StringTokenTieBetweenTwoCandidates_ReturnsNull()
    {
        // No label rules at all → CLIP filters are no-ops (Step 1/2 return candidates unchanged).
        // No numeric rules → numeric reduction is a no-op. Both families carry the same categorical
        // "ivory" token, so string scoring produces an equal top match count → tie → null.
        SemanticMatcher matcher = MakeMatcher(semanticThreshold: 0.1);
        FamilyIDRecord famA = FamilyWithProperty("FAM_A", "color", "ivory", ExcelColumnClassification.Categorical);
        FamilyIDRecord famB = FamilyWithProperty("FAM_B", "color", "ivory", ExcelColumnClassification.Categorical);
        ImageRecord_LAMBDA record = MakeLambda("ivory-dress.jpg");

        MatchEvidence? evidence = matcher.TryMatch(record, [famA, famB], NoNumericRules, NoLabelRules);

        Assert.Null(evidence);
    }

    //  Below semantic threshold

    [Fact]
    public void TryMatch_CombinedScoreBelowHighSemanticThreshold_ReturnsNull()
    {
        // Same scenario as the happy path (single CLIP-filtered survivor, no string/numeric signal),
        // but semanticThreshold is set above the achievable combined score (0.5) so acceptance fails
        // at the Step 5 threshold check rather than at the candidate-reduction steps.
        SemanticMatcher matcher = MakeMatcher(semanticThreshold: 0.95);
        FamilyIDRecord famA = FamilyWithProperty("FAM001", "ProductType", "tote", ExcelColumnClassification.Categorical);
        FamilyIDRecord famB = FamilyWithProperty("FAM002", "ProductType", "backpack", ExcelColumnClassification.Categorical);
        ImageRecord_LAMBDA record = MakeLambda("bag-photo.jpg", influentialLabel: "tote");

        MatchEvidence? evidence = matcher.TryMatch(record, [famA, famB], NoNumericRules, ProductTypeLabelRule);

        Assert.Null(evidence);
    }

    //  String scoring with multiple candidates and overlapping matches

    [Fact]
    public void TryMatch_MultipleOverlappingCandidates_RanksCorrectly()
    {
        // Three families, two of which have overlapping partial matches: FAM_A matches both
        // "tote" (ProductType via CLIP filter) and "leather" (from filename); FAM_B matches
        // only "tote" but NOT "leather"; FAM_C is filtered out by CLIP (contradicting ProductType).
        // String scoring should rank FAM_A higher (2 tokens) vs FAM_B (1 token), making FAM_A the
        // clear winner, not a tie.
        SemanticMatcher matcher = MakeMatcher(semanticThreshold: 0.3);
        FamilyIDRecord famA = new("FAM_A");
        famA.MergeProperty(new ExcelPropertyValue("ProductType", ["tote"], []), ExcelColumnClassification.Categorical);
        famA.MergeProperty(new ExcelPropertyValue("material", ["leather"], []), ExcelColumnClassification.Categorical);

        FamilyIDRecord famB = new("FAM_B");
        famB.MergeProperty(new ExcelPropertyValue("ProductType", ["tote"], []), ExcelColumnClassification.Categorical);

        FamilyIDRecord famC = FamilyWithProperty("FAM_C", "ProductType", "backpack", ExcelColumnClassification.Categorical);

        ImageRecord_LAMBDA record = MakeLambda("tote-leather-bag.jpg", influentialLabel: "tote");

        MatchEvidence? evidence = matcher.TryMatch(record, [famA, famB, famC], NoNumericRules, ProductTypeLabelRule);

        Assert.NotNull(evidence);
        Assert.Equal("FAM_A", evidence!.FinalFamilyId);
        Assert.Equal("SemanticMatcher.Bracket4", evidence.AcceptedMatcherName);
        Assert.Equal(2, evidence.StringTokenEvidence.Count);
    }

    //  Helpers

    private static readonly TranslationConfig EmptyTranslation = new()
    {
        SynonymGroups = [],
        StopWords = new StopWordConfig { General = [], Domain = [] }
    };

    private static readonly IReadOnlyList<MatchingRule> NoNumericRules = [];
    private static readonly IReadOnlyList<MatchingRule> NoLabelRules = [];

    private static readonly IReadOnlyList<MatchingRule> ProductTypeLabelRule =
    [
        new MatchingRule { ExcelField = "ProductType", Type = "string", Strategy = "ClipLabelEnricher", Weight = 0.8 }
    ];

    private static SemanticMatcher MakeMatcher(double semanticThreshold) =>
        new(new NumericMatcher("FamilyID"), new StringMatcher(EmptyTranslation), new ClipLabelEnricher(), semanticThreshold, 0.15);

    private static ImageRecord_LAMBDA MakeLambda(string filename, string? influentialLabel = null)
    {
        ImageRecord_LAMBDA record = new() { InitialFullName = filename };
        if (influentialLabel is not null)
        {
            // Mirror what ClassificationService.ApplyTokens produces: the prompt sentence in Label,
            // the resolved feature value in Value — ClipLabelEnricher matches on Value only.
            record.Tags = new TagCollection
            {
                Influential =
                [
                    new ClassificationToken
                    {
                        Label      = $"a photo of a {influentialLabel}",
                        Feature    = "product-type-label",
                        Value      = influentialLabel,
                        Confidence = 0.95
                    }
                ]
            };
        }
        return record;
    }

    private static FamilyIDRecord FamilyWithProperty(
        string familyId,
        string propName,
        string propValue,
        ExcelColumnClassification classification)
    {
        FamilyIDRecord family = new(familyId);
        family.MergeProperty(
            new ExcelPropertyValue(propName, [propValue], []),
            classification);
        return family;
    }
}
