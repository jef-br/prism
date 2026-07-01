using Xunit;

namespace PrismCoreTests.Match;

/// <summary>
/// Unit tests for <see cref="StringMatcher"/> — Bracket 3 (string token, exactly-1-FamilyID).
/// </summary>
public class StringMatcherTests
{
    //  Bracket 3: happy path 

    [Fact]
    public void TryMatch_FilenameTokenMatchesOneFamily_ReturnsEvidence()
    {
        StringMatcher      matcher = new(EmptyTranslation);
        FamilyIDRecord       family  = FamilyWithProperty("FAM001", "color", "blue", ExcelColumnClassification.Categorical);
        ImageRecord_LAMBDA record  = MakeLambda("blue-shirt.jpg");

        MatchEvidence? evidence = matcher.TryMatch(record, [family]);

        Assert.NotNull(evidence);
        Assert.Equal("FAM001", evidence!.FinalFamilyId);
        Assert.False(evidence.IsKo);
        Assert.Equal("StringMatcher.Bracket3", evidence.AcceptedMatcherName);
        Assert.NotEmpty(evidence.StringTokenEvidence);
    }

    //  Bracket 3: no match 

    [Fact]
    public void TryMatch_FilenameTokenMatchesNoFamily_ReturnsNull()
    {
        StringMatcher      matcher = new(EmptyTranslation);
        FamilyIDRecord       family  = FamilyWithProperty("FAM001", "color", "blue", ExcelColumnClassification.Categorical);
        ImageRecord_LAMBDA record  = MakeLambda("red-shirt.jpg");

        MatchEvidence? evidence = matcher.TryMatch(record, [family]);

        Assert.Null(evidence);
    }

    //  Bracket 3: tie (multi-FamilyID candidacy) 

    [Fact]
    public void TryMatch_FilenameTokenMatchesTwoFamilies_ReturnsNull()
    {
        StringMatcher      matcher = new(EmptyTranslation);
        FamilyIDRecord       famA    = FamilyWithProperty("FAM001", "color", "blue", ExcelColumnClassification.Categorical);
        FamilyIDRecord       famB    = FamilyWithProperty("FAM002", "color", "blue", ExcelColumnClassification.Categorical);
        ImageRecord_LAMBDA record  = MakeLambda("blue-shirt.jpg");

        MatchEvidence? evidence = matcher.TryMatch(record, [famA, famB]);

        Assert.Null(evidence); // tie: both families have evidence for "blue"
    }

    //  Bracket 3: all-digit filename 

    [Fact]
    public void TryMatch_FilenameHasOnlyDigits_ReturnsNull()
    {
        StringMatcher      matcher = new(EmptyTranslation);
        FamilyIDRecord       family  = FamilyWithProperty("FAM001", "color", "12345", ExcelColumnClassification.Categorical);
        ImageRecord_LAMBDA record  = MakeLambda("12345.jpg");

        // Digit tokens are excluded from StringMatcher token extraction
        MatchEvidence? evidence = matcher.TryMatch(record, [family]);

        Assert.Null(evidence);
    }

    //  Bracket 3: synonym resolution 

    [Fact]
    public void TryMatch_SynonymInFilenameMatchesFamily_ReturnsEvidence()
    {
        TranslationConfig withSynonyms = new()
        {
            SynonymGroups =
            [
                new SynonymGroup { Id = "g1", Domain = "color", Terms = ["blue", "blau"] }
            ],
            StopWords = new StopWordConfig { General = [], Domain = [] }
        };

        StringMatcher      matcher = new(withSynonyms);
        FamilyIDRecord       family  = FamilyWithProperty("FAM001", "color", "blue", ExcelColumnClassification.Categorical);
        ImageRecord_LAMBDA record  = MakeLambda("blau-shirt.jpg"); // German synonym for blue

        MatchEvidence? evidence = matcher.TryMatch(record, [family]);

        Assert.NotNull(evidence);
        Assert.Equal("FAM001", evidence!.FinalFamilyId);
        Assert.Equal("StringMatcher.Bracket3", evidence.AcceptedMatcherName);
    }

    //  Bracket 3: strict-winner tie resolution

    [Fact]
    public void TryMatch_DistinctiveTokenBreaksCommonTokenTie_ReturnsStrictWinner()
    {
        // Both families share the common token "ivory"; only FAM_A also matches the distinctive "alba".
        // Strict-winner picks FAM_A (2 distinct matches) over FAM_B (1) instead of rejecting as a tie.
        FamilyIDRecord famA = new("FAM_A");
        famA.MergeProperty(new ExcelPropertyValue("name", ["alba"], []), ExcelColumnClassification.Categorical);
        famA.MergeProperty(new ExcelPropertyValue("color", ["ivory"], []), ExcelColumnClassification.Categorical);
        FamilyIDRecord famB = FamilyWithProperty("FAM_B", "color", "ivory", ExcelColumnClassification.Categorical);

        StringMatcher matcher = new(EmptyTranslation);
        ImageRecord_LAMBDA record = MakeLambda("alba_ivory_B.jpg");

        MatchEvidence? evidence = matcher.TryMatch(record, [famA, famB]);

        Assert.NotNull(evidence);
        Assert.Equal("FAM_A", evidence!.FinalFamilyId);
    }

    [Fact]
    public void TryMatch_EqualTopTokenCounts_ReturnsNull()
    {
        // Both families match only the common token "ivory" equally → no strict winner → null.
        FamilyIDRecord famA = FamilyWithProperty("FAM_A", "color", "ivory", ExcelColumnClassification.Categorical);
        FamilyIDRecord famB = FamilyWithProperty("FAM_B", "color", "ivory", ExcelColumnClassification.Categorical);

        StringMatcher matcher = new(EmptyTranslation);
        ImageRecord_LAMBDA record = MakeLambda("ivory-dress.jpg");

        Assert.Null(matcher.TryMatch(record, [famA, famB]));
    }

    //  Bracket 4 support: ScoreCandidatesByStringTokens (indexed rewrite)

    [Fact]
    public void ScoreCandidatesByStringTokens_SimpleCase_ReturnsExpectedMatchCountAndEvidence()
    {
        StringMatcher matcher = new(EmptyTranslation);
        FamilyIDRecord family = FamilyWithProperty("FAM001", "color", "blue", ExcelColumnClassification.Categorical);

        var scored = matcher.ScoreCandidatesByStringTokens("blue-shirt.jpg", [family], [family]);

        Assert.Single(scored);
        Assert.Equal("FAM001", scored[0].Family.FamilyID);
        Assert.Equal(1, scored[0].MatchCount);
        Assert.Single(scored[0].Evidence);
        Assert.Equal("blue", scored[0].Evidence[0].FilenameToken, ignoreCase: true);
    }

    [Fact]
    public void ScoreCandidatesByStringTokens_OrdersResultsByMatchCountDescending()
    {
        // FAM_A matches both "alba" and "ivory" (2 tokens); FAM_B matches only "ivory" (1 token).
        FamilyIDRecord famA = new("FAM_A");
        famA.MergeProperty(new ExcelPropertyValue("name", ["alba"], []), ExcelColumnClassification.Categorical);
        famA.MergeProperty(new ExcelPropertyValue("color", ["ivory"], []), ExcelColumnClassification.Categorical);
        FamilyIDRecord famB = FamilyWithProperty("FAM_B", "color", "ivory", ExcelColumnClassification.Categorical);

        StringMatcher matcher = new(EmptyTranslation);
        var scored = matcher.ScoreCandidatesByStringTokens("alba_ivory.jpg", [famA, famB], [famA, famB]);

        Assert.Equal(2, scored.Count);
        Assert.Equal("FAM_A", scored[0].Family.FamilyID);
        Assert.Equal(2, scored[0].MatchCount);
        Assert.Equal("FAM_B", scored[1].Family.FamilyID);
        Assert.Equal(1, scored[1].MatchCount);
    }

    [Fact]
    public void ScoreCandidatesByStringTokens_IndexScopeLargerThanCandidates_OnlyScoresCandidatesSubset()
    {
        // Both FAM001 and FAM002 match the "blue" token, but only FAM001 is in the candidates subset.
        // indexScope (both families) proves the index is built from the superset, while the returned
        // results are still filtered down to the candidates subset only.
        FamilyIDRecord famA = FamilyWithProperty("FAM001", "color", "blue", ExcelColumnClassification.Categorical);
        FamilyIDRecord famB = FamilyWithProperty("FAM002", "color", "blue", ExcelColumnClassification.Categorical);

        StringMatcher matcher = new(EmptyTranslation);
        var scored = matcher.ScoreCandidatesByStringTokens("blue-shirt.jpg", [famA], [famA, famB]);

        Assert.Single(scored);
        Assert.Equal("FAM001", scored[0].Family.FamilyID);
    }

    [Fact]
    public void ScoreCandidatesByStringTokens_SynonymInFilename_ResolvesThroughIndexedPath()
    {
        TranslationConfig withSynonyms = new()
        {
            SynonymGroups =
            [
                new SynonymGroup { Id = "g1", Domain = "color", Terms = ["blue", "blau"] }
            ],
            StopWords = new StopWordConfig { General = [], Domain = [] }
        };

        StringMatcher matcher = new(withSynonyms);
        FamilyIDRecord family = FamilyWithProperty("FAM001", "color", "blue", ExcelColumnClassification.Categorical);

        var scored = matcher.ScoreCandidatesByStringTokens("blau-shirt.jpg", [family], [family]);

        Assert.Single(scored);
        Assert.Equal("FAM001", scored[0].Family.FamilyID);
        Assert.Equal(1, scored[0].MatchCount);
    }

    [Fact]
    public void ScoreCandidatesByStringTokens_NoTokenMatches_ReturnsEmpty()
    {
        StringMatcher matcher = new(EmptyTranslation);
        FamilyIDRecord family = FamilyWithProperty("FAM001", "color", "blue", ExcelColumnClassification.Categorical);

        var scored = matcher.ScoreCandidatesByStringTokens("red-shirt.jpg", [family], [family]);

        Assert.Empty(scored);
    }

    [Fact]
    public void ScoreCandidatesByStringTokens_ThreeFamiliesWithOverlappingTokenMatches_RanksCorrectly()
    {
        // Three families with partially overlapping token matches: FAM_A matches "blue" and "wool"
        // (2 tokens); FAM_B matches only "blue" (1 token); FAM_C matches "wool" (1 token).
        // Indexed scoring should rank FAM_A highest (2), then FAM_B and FAM_C tied at (1).
        // This verifies that the indexed path produces identical ranking to an exhaustive O(families×tokens)
        // scan, especially for close candidates.
        FamilyIDRecord famA = new("FAM_A");
        famA.MergeProperty(new ExcelPropertyValue("color", ["blue"], []), ExcelColumnClassification.Categorical);
        famA.MergeProperty(new ExcelPropertyValue("material", ["wool"], []), ExcelColumnClassification.Categorical);

        FamilyIDRecord famB = FamilyWithProperty("FAM_B", "color", "blue", ExcelColumnClassification.Categorical);
        FamilyIDRecord famC = FamilyWithProperty("FAM_C", "material", "wool", ExcelColumnClassification.Categorical);

        StringMatcher matcher = new(EmptyTranslation);
        var scored = matcher.ScoreCandidatesByStringTokens("blue_wool_shirt.jpg", [famA, famB, famC], [famA, famB, famC]);

        Assert.Equal(3, scored.Count);
        Assert.Equal("FAM_A", scored[0].Family.FamilyID);
        Assert.Equal(2, scored[0].MatchCount);
        Assert.Equal("FAM_B", scored[1].Family.FamilyID);
        Assert.Equal(1, scored[1].MatchCount);
        Assert.Equal("FAM_C", scored[2].Family.FamilyID);
        Assert.Equal(1, scored[2].MatchCount);
    }

    //  Helpers

    private static readonly TranslationConfig EmptyTranslation = new()
    {
        SynonymGroups = [],
        StopWords     = new StopWordConfig { General = [], Domain = [] }
    };

    private static ImageRecord_LAMBDA MakeLambda(string filename) =>
        new() { InitialFullName = filename };

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
