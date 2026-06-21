using Xunit;

namespace PrismCoreTests.Match;

/// <summary>
/// Unit tests for <see cref="NumericMatcher"/> — Bracket 1 (single-token exact) and Bracket 2 (multi-token TCD).
/// </summary>
public class NumericMatcherTests
{
    private static readonly MatchingRule FamilyIdRule = new()
    {
        ExcelField   = "familyID",
        Type         = "numeric",
        Strategy     = "NumericalMatcher",
        Weight       = 0.55,
        MaxDistance  = 1.0,
        Candidates   = "3"
    };

    private static readonly IReadOnlyList<MatchingRule> OneRule = [FamilyIdRule];

    // Matches FamilyIdRule.ExcelField: the matcher resolves this field from family.FamilyID directly.
    private const string FamilyIdColumn = "familyID";

    // ─── Bracket 1: happy path ─────────────────────────────────────────────────

    [Fact]
    public void Bracket1_SingleTokenExactMatchOneFamily_ReturnsEvidence()
    {
        NumericMatcher matcher  = new(FamilyIdColumn);
        FamilyRecord   family   = new("12345");
        ImageRecord_LAMBDA record = MakeLambda("photo_12345.jpg");

        MatchEvidence? evidence = matcher.TryMatchBracket1(record, [family], OneRule);

        Assert.NotNull(evidence);
        Assert.Equal("12345", evidence!.FinalFamilyId);
        Assert.False(evidence.IsKo);
        Assert.Equal("NumericMatcher.Bracket1", evidence.AcceptedMatcherName);
    }

    // ─── Bracket 1: no match ──────────────────────────────────────────────────

    [Fact]
    public void Bracket1_TokenDoesNotMatchAnyFamily_ReturnsNull()
    {
        NumericMatcher matcher = new(FamilyIdColumn);
        FamilyRecord   family  = new("12345");
        ImageRecord_LAMBDA record = MakeLambda("photo_99999.jpg");

        MatchEvidence? evidence = matcher.TryMatchBracket1(record, [family], OneRule);

        Assert.Null(evidence);
    }

    // ─── Bracket 1: tie ───────────────────────────────────────────────────────

    [Fact]
    public void Bracket1_TokenMatchesTwoFamilies_ReturnsNull()
    {
        NumericMatcher matcher  = new(FamilyIdColumn);
        FamilyRecord   famA     = new("12345");
        FamilyRecord   famB     = new("12345X");
        // Both resolve to digits-only "12345"
        // We need the same digit string but different FamilyIDs: use CanonicalProperties on one
        // Simpler: use EAN rule on famA and rely on familyID match for famB
        // Easiest: two families whose digits-only FamilyID is the same → FamilyID "12345" and a
        // family that stores "12345" in a different property.
        // Use a second rule that targets a CanonicalProperties field so two families produce token "123".
        MatchingRule eanRule = new()
        {
            ExcelField   = "EAN",
            Type         = "numeric",
            Strategy     = "NumericalMatcher",
            Weight       = 0.55,
            MaxDistance  = 1.0,
            Candidates   = "3"
        };
        FamilyRecord famX = new("FAMAX");
        famX.MergeProperty(new ExcelPropertyValue("EAN", ["12345"], []), ExcelColumnClassification.Numerical);

        FamilyRecord famY = new("12345"); // FamilyID itself is "12345"
        IReadOnlyList<MatchingRule> rules = [eanRule, FamilyIdRule];
        ImageRecord_LAMBDA record = MakeLambda("photo_12345.jpg");

        MatchEvidence? evidence = matcher.TryMatchBracket1(record, [famX, famY], rules);

        Assert.Null(evidence); // tie: both families produce digit "12345"
    }

    // ─── Bracket 1: multi-token filename, one matches ─────────────────────────

    [Fact]
    public void Bracket1_MultipleTokensOneMatchesTarget_ReturnsEvidence()
    {
        NumericMatcher     matcher = new(FamilyIdColumn);
        FamilyRecord       family  = new("12345");
        ImageRecord_LAMBDA record  = MakeLambda("photo_12345_v2.jpg");

        MatchEvidence? evidence = matcher.TryMatchBracket1(record, [family], OneRule);

        Assert.NotNull(evidence);
        Assert.Equal("12345", evidence!.FinalFamilyId);
    }

    // ─── Bracket 2: happy path ─────────────────────────────────────────────────

    [Fact]
    public void Bracket2_TwoTokensConcatenateToTarget_ReturnsEvidence()
    {
        NumericMatcher     matcher = new(FamilyIdColumn);
        FamilyRecord       family  = new("1234");
        // Equal-length two-token split ("12"+"34"="1234") gives TCD = 1.0 exactly,
        // which satisfies the strict > check against MaxDistance = 1.0.
        ImageRecord_LAMBDA record  = MakeLambda("photo_12_34.jpg");

        MatchEvidence? evidence = matcher.TryMatchBracket2(record, [family], OneRule);

        Assert.NotNull(evidence);
        Assert.Equal("1234", evidence!.FinalFamilyId);
        Assert.False(evidence.IsKo);
        Assert.Equal("NumericMatcher.Bracket2", evidence.AcceptedMatcherName);
    }

    // ─── Bracket 2: single token only → skip ─────────────────────────────────

    [Fact]
    public void Bracket2_SingleTokenInFilename_ReturnsNull()
    {
        NumericMatcher     matcher = new(FamilyIdColumn);
        FamilyRecord       family  = new("1234");
        ImageRecord_LAMBDA record  = MakeLambda("photo_1234.jpg");

        // Bracket 2 requires ≥ 2 tokens; single token returns null even when it would match Bracket 1
        MatchEvidence? evidence = matcher.TryMatchBracket2(record, [family], OneRule);

        Assert.Null(evidence);
    }

    // ─── Bracket 2: tie ───────────────────────────────────────────────────────

    [Fact]
    public void Bracket2_ConcatenationMatchesTwoFamilies_ReturnsNull()
    {
        NumericMatcher matcher = new(FamilyIdColumn);
        FamilyRecord   famA   = new("1234");  // FamilyID digits "1234"
        MatchingRule   eanRule = new()
        {
            ExcelField  = "EAN",
            Type        = "numeric",
            Strategy    = "NumericalMatcher",
            Weight      = 0.55,
            MaxDistance = 1.0,
            Candidates  = "3"
        };
        FamilyRecord famB = new("FAMB");
        famB.MergeProperty(new ExcelPropertyValue("EAN", ["1234"], []), ExcelColumnClassification.Numerical);

        IReadOnlyList<MatchingRule> rules  = [FamilyIdRule, eanRule];
        ImageRecord_LAMBDA         record = MakeLambda("photo_12_34.jpg");

        MatchEvidence? evidence = matcher.TryMatchBracket2(record, [famA, famB], rules);

        Assert.Null(evidence);
    }

    // ─── Bracket 2: no match ──────────────────────────────────────────────────

    [Fact]
    public void Bracket2_NoConcatenationMatchesAnyFamily_ReturnsNull()
    {
        NumericMatcher     matcher = new(FamilyIdColumn);
        FamilyRecord       family  = new("9999");
        ImageRecord_LAMBDA record  = MakeLambda("photo_12_34.jpg");

        MatchEvidence? evidence = matcher.TryMatchBracket2(record, [family], OneRule);

        Assert.Null(evidence);
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private static ImageRecord_LAMBDA MakeLambda(string filename) =>
        new() { InitialFullName = filename };
}
