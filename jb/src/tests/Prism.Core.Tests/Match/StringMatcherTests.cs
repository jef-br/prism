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
