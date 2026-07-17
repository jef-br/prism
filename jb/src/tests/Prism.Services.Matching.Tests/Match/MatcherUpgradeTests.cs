using Xunit;

namespace PrismCoreTests.Match;

/// <summary>
/// Unit tests for the matching-rate upgrades: NumericMatcher min-token-length guard, all-columns
/// digit-run index, token intersection, substring rescue; StringMatcher bigrams, identifier-grade
/// single token, short-digit tiebreak; SiblingPropagator; OrphanRowJoiner. Each test encodes one
/// real failure mode observed in the dataset analysis (INPUTMA24, FILA94, WOODWIC12, HEROAUT3,
/// CiMini, MEPAL4).
/// </summary>
public class MatcherUpgradeTests
{
    private static readonly MatchingRule FamilyIdRule = new()
    {
        ExcelField  = "familyID",
        Type        = "numeric",
        Strategy    = "NumericalMatcher",
        Weight      = 1.0,
        MaxDistance = 1.478,
        Candidates  = "3"
    };

    private static readonly MatchingRule RefCoRule = new()
    {
        ExcelField  = "RefCo",
        Type        = "numeric",
        Strategy    = "NumericalMatcher",
        Weight      = 1.0,
        MaxDistance = 1.478,
        Candidates  = "3"
    };

    private static readonly IReadOnlyList<MatchingRule> Rules = [FamilyIdRule, RefCoRule];

    private static readonly TranslationConfig EmptyTranslation = new()
    {
        SynonymGroups = [],
        StopWords     = new StopWordConfig { General = [], Domain = [] }
    };

    // Production-equivalent tuning values (MatchingConfig.json is required-only now — tests supply
    // their own explicit fixture values rather than relying on constructor defaults). Individual
    // tests override the specific value under test via named arguments.
    private const int DefaultBracket3MinDistinctTokens = 1;
    private const int DefaultIdentifierTokenMinLength = 0;
    private const bool DefaultIndexExcelTokenBigrams = false;
    private const int DefaultFuzzyMinTokenLength = 4;
    private const int DefaultFuzzyMaxEditDistance = 1;
    private const double DefaultFuzzyMatchScore = 0.75;

    //  M1: min-token-length guard (INPUTMA24 false ties)

    [Fact]
    public void Bracket1_ShortRefCoDigitsDoNotTieAgainstFamilyIdToken()
    {
        // Family B's RefCo "MGGE073" yields digits "073"; the shot suffix "_073" must not drag it
        // into a tie with the exact 8-digit FamilyID match on family A.
        NumericMatcher matcher = new("familyID", minNumericTokenLength: 5);
        FamilyIDRecord famA = new("94671115");
        FamilyIDRecord famB = MakeFamily("94671189", ("RefCo", "MGGE073", ExcelColumnClassification.Mixed));

        MatchEvidence? evidence = matcher.TryMatchBracket1(MakeLambda("94671115_073.jpg"), [famA, famB], Rules);

        Assert.NotNull(evidence);
        Assert.Equal("94671115", evidence!.FinalFamilyId);
    }

    [Fact]
    public void Bracket1_LegacyMinLengthOne_ShortTokensStillTie()
    {
        NumericMatcher matcher = new("familyID", minNumericTokenLength: 1);
        FamilyIDRecord famA = new("94671115");
        FamilyIDRecord famB = MakeFamily("94671189", ("RefCo", "MGGE073", ExcelColumnClassification.Mixed));

        MatchEvidence? evidence = matcher.TryMatchBracket1(MakeLambda("94671115_073.jpg"), [famA, famB], Rules);

        Assert.Null(evidence); // legacy behavior preserved: "073" ties famB against famA
    }

    //  M1: all-columns digit-run index (FILA94 label, INPUTMA27 SKU)

    [Fact]
    public void Bracket1_DigitRunInsideNonRuleColumn_Matches()
    {
        // The article number lives inside a compound label cell, not in any numeric-rule column.
        NumericMatcher matcher = new("familyID", minNumericTokenLength: 5, indexDigitRunsAllColumns: true);
        FamilyIDRecord family = MakeFamily("98226704", ("label", "MAN-Posy Green-1010930-60105", ExcelColumnClassification.Mixed));

        MatchEvidence? evidence = matcher.TryMatchBracket1(MakeLambda("1010930_A_02.png"), [family], Rules);

        Assert.NotNull(evidence);
        Assert.Equal("98226704", evidence!.FinalFamilyId);
    }

    //  M1: token intersection (FILA94 article+color pair)

    [Fact]
    public void TokenIntersection_SharedColorCodePlusArticleRun_NarrowsToOneFamily()
    {
        // "60105" (color) appears in both labels; "1010930" (article) appears in both too — but
        // only one family carries the pair in one label. Intersection of per-token hit sets must
        // resolve what single-token lookups cannot.
        NumericMatcher matcher = new("familyID", minNumericTokenLength: 5, indexDigitRunsAllColumns: true);
        FamilyIDRecord famA = MakeFamily("98226704", ("label", "MAN-Posy Green-1010930-60105", ExcelColumnClassification.Mixed));
        FamilyIDRecord famB = MakeFamily("98226705", ("label", "WOMAN-Posy Green-1010931-60105", ExcelColumnClassification.Mixed));
        FamilyIDRecord famC = MakeFamily("98226706", ("label", "MAN-White-1010930-10001", ExcelColumnClassification.Mixed));

        ImageRecord_LAMBDA record = MakeLambda("1010930_60105_A_01.png");

        Assert.Null(matcher.TryMatchBracket1(record, [famA, famB, famC], Rules)); // both tokens tie alone

        (MatchEvidence? evidence, _) = matcher.TryMatchByTokenIntersection(record, [famA, famB, famC], Rules);

        Assert.NotNull(evidence);
        Assert.Equal("98226704", evidence!.FinalFamilyId);
        Assert.Equal("NumericMatcher.Bracket2-Intersect", evidence.AcceptedMatcherName);
    }

    //  M1: substring rescue (INPUTMA24 EAN-embedded reference)

    [Fact]
    public void SubstringRescue_TokenInsideEan_MatchesUniqueFamily()
    {
        NumericMatcher matcher = new("familyID", minNumericTokenLength: 5, minSubstringRescueLength: 7);
        FamilyIDRecord famA = MakeFamily("94671120", ("EAN", "8446271023117", ExcelColumnClassification.Numerical));
        FamilyIDRecord famB = MakeFamily("94671121", ("EAN", "8435747805700", ExcelColumnClassification.Numerical));

        MatchingRule eanRule = RefCoRule with { ExcelField = "EAN" };
        (MatchEvidence? evidence, _) = matcher.TryMatchBySubstringRescue(
            MakeLambda("46271023.jpg"), [famA, famB], [FamilyIdRule, eanRule]);

        Assert.NotNull(evidence);
        Assert.Equal("94671120", evidence!.FinalFamilyId);
        Assert.Equal("NumericMatcher.SubstringRescue", evidence.AcceptedMatcherName);
    }

    [Fact]
    public void SubstringRescue_Disabled_ReturnsNull()
    {
        NumericMatcher matcher = new("familyID", minNumericTokenLength: 5, minSubstringRescueLength: 0);
        FamilyIDRecord family = MakeFamily("94671120", ("EAN", "8446271023117", ExcelColumnClassification.Numerical));

        MatchingRule eanRule = RefCoRule with { ExcelField = "EAN" };
        Assert.Null(matcher.TryMatchBySubstringRescue(MakeLambda("46271023.jpg"), [family], [FamilyIdRule, eanRule]).Evidence);
    }

    //  M2: Excel bigrams + filename boundary split (HEROAUT3 glued color)

    [Fact]
    public void Bracket3_GluedFilenameToken_MatchesAdjacentExcelTokens()
    {
        // Filename glues "palm blue" into "palmblue"; the Excel cell holds them adjacent.
        StringMatcher matcher = new(EmptyTranslation, bracket3MinDistinctTokens: 2, identifierTokenMinLength: DefaultIdentifierTokenMinLength, indexExcelTokenBigrams: true,
            fuzzyMinTokenLength: DefaultFuzzyMinTokenLength, fuzzyMaxEditDistance: DefaultFuzzyMaxEditDistance, fuzzyMatchScore: DefaultFuzzyMatchScore);
        FamilyIDRecord famA = MakeFamily("94612975", ("reference", "ANASTASIA AB-PALM BLUE", ExcelColumnClassification.Mixed));
        FamilyIDRecord famB = MakeFamily("94612976", ("reference", "ARIZONA CC-BLUE LEAF", ExcelColumnClassification.Mixed));

        MatchEvidence? evidence = matcher.TryMatch(MakeLambda("Anastasia_palmblue_2.jpg"), [famA, famB]);

        Assert.NotNull(evidence);
        Assert.Equal("94612975", evidence!.FinalFamilyId);
    }

    //  M2: identifier-grade single token (WOODWIC12 reference stems)

    [Fact]
    public void Bracket3_UniqueIdentifierToken_BypassesMinDistinctTokensGate()
    {
        StringMatcher matcher = new(EmptyTranslation, bracket3MinDistinctTokens: 2, identifierTokenMinLength: 4, indexExcelTokenBigrams: DefaultIndexExcelTokenBigrams,
            fuzzyMinTokenLength: DefaultFuzzyMinTokenLength, fuzzyMaxEditDistance: DefaultFuzzyMaxEditDistance, fuzzyMatchScore: DefaultFuzzyMatchScore);
        FamilyIDRecord famA = MakeFamily("98954095", ("reference", "1707527E", ExcelColumnClassification.Mixed));
        FamilyIDRecord famB = MakeFamily("98954100", ("reference", "2653556E", ExcelColumnClassification.Mixed));

        MatchEvidence? evidence = matcher.TryMatch(MakeLambda("1707527E.jpg"), [famA, famB]);

        Assert.NotNull(evidence);
        Assert.Equal("98954095", evidence!.FinalFamilyId);
    }

    [Fact]
    public void Bracket3_IdentifierBypassDisabled_GateStillRejects()
    {
        StringMatcher matcher = new(EmptyTranslation, bracket3MinDistinctTokens: 2, identifierTokenMinLength: 0, indexExcelTokenBigrams: DefaultIndexExcelTokenBigrams,
            fuzzyMinTokenLength: DefaultFuzzyMinTokenLength, fuzzyMaxEditDistance: DefaultFuzzyMaxEditDistance, fuzzyMatchScore: DefaultFuzzyMatchScore);
        FamilyIDRecord famA = MakeFamily("98954095", ("reference", "1707527E", ExcelColumnClassification.Mixed));
        FamilyIDRecord famB = MakeFamily("98954100", ("reference", "2653556E", ExcelColumnClassification.Mixed));

        Assert.Null(matcher.TryMatch(MakeLambda("1707527E.jpg"), [famA, famB]));
    }

    //  M2: short-digit tiebreak (CiMini color-code discrimination)

    [Fact]
    public void Bracket3_TopTie_ShortDigitTokenDiscriminates()
    {
        // Both families are cardigans in magenta; only famA carries color code "76".
        StringMatcher matcher = new(EmptyTranslation, bracket3MinDistinctTokens: 2, identifierTokenMinLength: DefaultIdentifierTokenMinLength, indexExcelTokenBigrams: DefaultIndexExcelTokenBigrams,
            fuzzyMinTokenLength: DefaultFuzzyMinTokenLength, fuzzyMaxEditDistance: DefaultFuzzyMaxEditDistance, fuzzyMatchScore: DefaultFuzzyMatchScore);
        FamilyIDRecord famA = MakeFamily("90861052",
            ("type", "CARDIGAN MAGENTA", ExcelColumnClassification.Categorical),
            ("RefCo", "24211507-76", ExcelColumnClassification.Mixed));
        FamilyIDRecord famB = MakeFamily("90861053",
            ("type", "CARDIGAN MAGENTA", ExcelColumnClassification.Categorical),
            ("RefCo", "24211508-13", ExcelColumnClassification.Mixed));

        MatchEvidence? evidence = matcher.TryMatch(MakeLambda("CARDIGAN_MAGENTA_76_A.jpg"), [famA, famB]);

        Assert.NotNull(evidence);
        Assert.Equal("90861052", evidence!.FinalFamilyId);
    }

    //  M3: sibling propagation (CiMini keyless shot of a matched product)

    [Fact]
    public void SiblingPropagator_KeylessShot_InheritsSiblingFamily()
    {
        SiblingPropagator propagator = new();

        ImageRecord_LAMBDA matched = MakeLambda("24211507_CARDIGAN_76_MAGENTA_B.jpg");
        matched.MatchEvidence = new MatchEvidence {
            ImageId = "24211507_CARDIGAN_76_MAGENTA_B",
            FinalFamilyId = "90861052",
            AcceptedMatcherName = "NumericMatcher.Bracket2",
            IsKo = false
        };

        ImageRecord_LAMBDA keyless = MakeLambda("CARDIGAN_MAGENTA76_A.jpg");
        List<ImageRecord_LAMBDA> allRecords = [matched, keyless, MakeLambda("Pareo Exotica.jpg")];

        List<ImageRecord_LAMBDA> stillUnmatched = propagator.Run([keyless], allRecords);

        Assert.DoesNotContain(keyless, stillUnmatched);
        Assert.Equal("90861052", keyless.MatchEvidence?.FinalFamilyId);
        Assert.Equal("SiblingPropagator", keyless.MatchEvidence?.AcceptedMatcherName);
    }

    [Fact]
    public void SiblingPropagator_ConflictingSiblings_DoesNotPropagate()
    {
        // Two different products both have magenta cardigan shots that reduce to {cardigan, magenta}.
        // The exact profile is owned by two families, so it is NOT a safe key, and a third keyless
        // shot with that same profile has no way to choose — it stays unmatched.
        SiblingPropagator propagator = new();

        ImageRecord_LAMBDA matchedA = MakeLambda("11111111_CARDIGAN_MAGENTA_A.jpg");
        matchedA.MatchEvidence = new MatchEvidence { ImageId = "a", FinalFamilyId = "11111111", IsKo = false };
        ImageRecord_LAMBDA matchedB = MakeLambda("22222222_CARDIGAN_MAGENTA_B.jpg");
        matchedB.MatchEvidence = new MatchEvidence { ImageId = "b", FinalFamilyId = "22222222", IsKo = false };

        ImageRecord_LAMBDA keyless = MakeLambda("CARDIGAN_MAGENTA_C.jpg");
        List<ImageRecord_LAMBDA> allRecords = [matchedA, matchedB, keyless];

        List<ImageRecord_LAMBDA> stillUnmatched = propagator.Run([keyless], allRecords);

        Assert.Contains(keyless, stillUnmatched);
        Assert.Null(keyless.MatchEvidence);
    }

    [Fact]
    public void SiblingPropagator_ThirdShotOfOneProduct_JoinsDespiteOverlapWithAnother()
    {
        // One product (90861052) has two matched magenta-cardigan shots. A third shot, CARDIGAN_MAGENTA76_C,
        // reduces to the same {cardigan, magenta} profile. A DIFFERENT product (90861099) has one matched
        // cardigan shot that only loosely overlaps. The exact profile {cardigan,magenta} is owned by exactly
        // one family, so the third shot joins that family instead of being refused.
        SiblingPropagator propagator = new();

        ImageRecord_LAMBDA shotA = MakeLambda("24211507_CARDIGAN_76_MAGENTA_A.jpg");
        shotA.MatchEvidence = new MatchEvidence { ImageId = "a", FinalFamilyId = "90861052", IsKo = false };
        ImageRecord_LAMBDA shotB = MakeLambda("24211507_CARDIGAN_76_MAGENTA_B.jpg");
        shotB.MatchEvidence = new MatchEvidence { ImageId = "b", FinalFamilyId = "90861052", IsKo = false };
        ImageRecord_LAMBDA otherCardigan = MakeLambda("99999999_CARDIGAN_BLACK_A.jpg");
        otherCardigan.MatchEvidence = new MatchEvidence { ImageId = "o", FinalFamilyId = "90861099", IsKo = false };

        ImageRecord_LAMBDA shotC = MakeLambda("CARDIGAN_MAGENTA76_C.jpg");
        List<ImageRecord_LAMBDA> allRecords = [shotA, shotB, otherCardigan, shotC];

        List<ImageRecord_LAMBDA> stillUnmatched = propagator.Run([shotC], allRecords);

        Assert.DoesNotContain(shotC, stillUnmatched);
        Assert.Equal("90861052", shotC.MatchEvidence?.FinalFamilyId);
    }

    //  Folder-name enrichment

    [Fact]
    public void FolderNameEnricher_MeaninglessFileInMeaningfulFolder_BorrowsFolderName()
    {
        // Filenames are meaningless (1.jpg, 2.jpg); the folders are one-per-product and a folder token
        // (the reference SH23005) appears in the Excel data. The folder name is borrowed for matching.
        FolderNameEnricher enricher = new();
        FamilyIDRecord famA = MakeFamily("98765432", ("reference", "earphones zenith SH23005 pro", ExcelColumnClassification.Mixed));
        FamilyIDRecord famB = MakeFamily("98765433", ("reference", "earphones apex SH23006 pro", ExcelColumnClassification.Mixed));

        ImageRecord_LAMBDA img = MakeLambda("C:/drop/earphones_zenith_SH23005/1.jpg");
        ImageRecord_LAMBDA sibling = MakeLambda("C:/drop/earphones_apex_SH23006/1.jpg");
        List<ImageRecord_LAMBDA> records = [img, sibling];

        enricher.Enrich(records, [famA, famB]);

        Assert.Equal("earphones_zenith_SH23005 1.jpg", img.MatchingAlias);
        Assert.Contains("SH23005", img.MatchingName);

        // And the borrowed name now matches: StringMatcher finds the unique family via the folder tokens.
        StringMatcher matcher = new(EmptyTranslation, bracket3MinDistinctTokens: 2, identifierTokenMinLength: 4, indexExcelTokenBigrams: true,
            fuzzyMinTokenLength: DefaultFuzzyMinTokenLength, fuzzyMaxEditDistance: DefaultFuzzyMaxEditDistance, fuzzyMatchScore: DefaultFuzzyMatchScore);
        MatchEvidence? evidence = matcher.TryMatch(img, [famA, famB]);
        Assert.Equal("98765432", evidence?.FinalFamilyId);
    }

    [Fact]
    public void FolderNameEnricher_FormatFolder_IsNotBorrowed()
    {
        // Folders describe format/size, not the product — nothing is borrowed.
        FolderNameEnricher enricher = new();
        FamilyIDRecord fam = MakeFamily("98765432", ("reference", "SH23005", ExcelColumnClassification.Mixed));

        ImageRecord_LAMBDA imgHd = MakeLambda("C:/drop/HD/1.jpg");
        ImageRecord_LAMBDA imgWeb = MakeLambda("C:/drop/Web/1.jpg");
        ImageRecord_LAMBDA imgDim = MakeLambda("C:/drop/800 x 1200/1.jpg");
        List<ImageRecord_LAMBDA> records = [imgHd, imgWeb, imgDim];

        enricher.Enrich(records, [fam]);

        Assert.Null(imgHd.MatchingAlias);
        Assert.Null(imgWeb.MatchingAlias);
        Assert.Null(imgDim.MatchingAlias);
    }

    [Fact]
    public void FolderNameEnricher_MeaningfulFilename_IsLeftAlone()
    {
        // The filename already carries a product word — the folder is never borrowed, even if meaningful.
        FolderNameEnricher enricher = new();
        FamilyIDRecord fam = MakeFamily("98765432", ("reference", "zenith SH23005", ExcelColumnClassification.Mixed), ("model", "anastasia", ExcelColumnClassification.Categorical));

        ImageRecord_LAMBDA img = MakeLambda("C:/drop/zenith_SH23005/Anastasia_front.jpg");
        ImageRecord_LAMBDA sibling = MakeLambda("C:/drop/apex_SH23006/Betty_front.jpg");

        enricher.Enrich(img is null ? [] : [img, sibling], [fam]);

        Assert.Null(img.MatchingAlias);
    }

    [Fact]
    public void FolderNameEnricher_FolderTokenNotInExcel_IsNotBorrowed()
    {
        // The folder is a per-product pattern, but none of its tokens appear in the Excel — no borrow.
        FolderNameEnricher enricher = new();
        FamilyIDRecord fam = MakeFamily("98765432", ("reference", "totally unrelated", ExcelColumnClassification.Mixed));

        ImageRecord_LAMBDA img = MakeLambda("C:/drop/mystery_widget_QQ111/1.jpg");
        ImageRecord_LAMBDA sibling = MakeLambda("C:/drop/mystery_gadget_QQ222/1.jpg");

        enricher.Enrich([img, sibling], [fam]);

        Assert.Null(img.MatchingAlias);
    }

    //  Phase E: orphan row join (MEPAL4 catalog → bundle Ref)

    [Fact]
    public void OrphanRowJoiner_ArticleNumberInsideBundleRef_JoinsRowToFamily()
    {
        InternalExcelModel model = new();
        model.AddOrMergeFamilyRow(
            "98985645",
            [new ExcelPropertyValue("ref", ["106297094700|106297094701-[Comp]"], [])],
            new Dictionary<string, ExcelColumnClassification> { ["ref"] = ExcelColumnClassification.Mixed });

        OrphanRow orphan = new(
            "catalog.xlsx",
            "Products",
            2,
            [
                new ExcelPropertyValue("articlenumber", ["106297094700"], []),
                new ExcelPropertyValue("color", ["Rose"], [])
            ],
            new Dictionary<string, ExcelColumnClassification> {
                ["articlenumber"] = ExcelColumnClassification.Numerical,
                ["color"] = ExcelColumnClassification.Categorical
            });

        List<ExcelProcessingDiagnostic> diagnostics = [];
        int joined = OrphanRowJoiner.Join(model, [orphan], diagnostics);

        Assert.Equal(1, joined);
        FamilyIDRecord family = model.RecordsByFamilyID["98985645"];
        Assert.True(family.CanonicalProperties.ContainsKey("color"));
        Assert.Equal("Rose", family.CanonicalProperties["color"]);
        Assert.Contains(diagnostics, d => d.ReasonCode == "excel.orphan_rows_joined");
    }

    [Fact]
    public void OrphanRowJoiner_KeySharedByTwoFamilies_DoesNotJoin()
    {
        InternalExcelModel model = new();
        Dictionary<string, ExcelColumnClassification> refClassification = new() { ["ref"] = ExcelColumnClassification.Mixed };
        model.AddOrMergeFamilyRow("98985645", [new ExcelPropertyValue("ref", ["106297094700"], [])], refClassification);
        model.AddOrMergeFamilyRow("98985646", [new ExcelPropertyValue("ref", ["106297094700-B"], [])], refClassification);

        OrphanRow orphan = new(
            "catalog.xlsx", "Products", 2,
            [new ExcelPropertyValue("articlenumber", ["106297094700"], [])],
            new Dictionary<string, ExcelColumnClassification> { ["articlenumber"] = ExcelColumnClassification.Numerical });

        List<ExcelProcessingDiagnostic> diagnostics = [];
        int joined = OrphanRowJoiner.Join(model, [orphan], diagnostics);

        Assert.Equal(0, joined); // ambiguous key — row stays orphaned
    }

    //  Helpers

    private static ImageRecord_LAMBDA MakeLambda(string filename) =>
        new() { InitialFullName = filename };

    private static FamilyIDRecord MakeFamily(string familyId, params (string Name, string Value, ExcelColumnClassification Classification)[] properties)
    {
        FamilyIDRecord family = new(familyId);
        foreach ((string name, string value, ExcelColumnClassification classification) in properties)
            family.MergeProperty(new ExcelPropertyValue(name, [value], []), classification);
        return family;
    }
}
