using Xunit;

namespace PrismCoreTests.Match;

/// <summary>
/// Unit tests for the matching-rate upgrades: NumericMatcher min-token-length guard, all-columns
/// digit-run index, token intersection, substring rescue; StringMatcher bigrams, identifier-grade
/// single token, short-digit tiebreak; SiblingPropagator; OrphanRowJoiner. Each test encodes one
/// real failure mode observed in the dataset analysis (INPUTMA24, FILA94, WOODWIC12, HEROAUT3,
/// CiMini, MEPAL4).
/// </summary>
public class MatcherUpgradeTests {
    private static readonly MatchingRule FamilyIdRule = new() {
        ExcelField = "familyID",
        Type = "numeric",
        Strategy = "NumericalMatcher",
        Weight = 1.0,
        MaxDistance = 1.478,
        Candidates = "3"
    };

    private static readonly MatchingRule RefCoRule = new() {
        ExcelField = "RefCo",
        Type = "numeric",
        Strategy = "NumericalMatcher",
        Weight = 1.0,
        MaxDistance = 1.478,
        Candidates = "3"
    };

    private static readonly IReadOnlyList<MatchingRule> Rules = [FamilyIdRule, RefCoRule];

    private static readonly TranslationConfig EmptyTranslation = new() {
        SynonymGroups = [],
        StopWords = new StopWordConfig { General = [], Domain = [] }
    };

    // Production-equivalent tuning values (MatchingConfig.json is required-only now — tests supply
    // their own explicit fixture values rather than relying on constructor defaults). Individual
    // tests override the specific value under test via the relevant Config property.
    private const int DefaultIdentifierTokenMinLength = 0;
    private const bool DefaultIndexExcelTokenBigrams = false;
    private const int DefaultFuzzyMinTokenLength = 4;
    private const int DefaultFuzzyMaxEditDistance = 1;
    private const double DefaultFuzzyMatchScore = 0.75;
    private const double DefaultSubstringRescueConfidence = 0.9;
    private const double DefaultMaxDistanceFallback = 1.478;

    private static NumericMatcher.Config MakeNumericCfg(int minNumericTokenLength, bool indexDigitRunsAllColumns, int minSubstringRescueLength) => new() {
        MinNumericTokenLength = minNumericTokenLength,
        IndexDigitRunsAllColumns = indexDigitRunsAllColumns,
        MinSubstringRescueLength = minSubstringRescueLength,
        SubstringRescueConfidence = DefaultSubstringRescueConfidence,
        DefaultMaxDistanceFallback = DefaultMaxDistanceFallback
    };

    private static StringMatcher.Config MakeStringCfg(int bracket3MinDistinctTokens, int identifierTokenMinLength, bool indexExcelTokenBigrams) => new() {
        Bracket3MinDistinctTokens = bracket3MinDistinctTokens,
        IdentifierTokenMinLength = identifierTokenMinLength,
        IndexExcelTokenBigrams = indexExcelTokenBigrams,
        FuzzyMinTokenLength = DefaultFuzzyMinTokenLength,
        FuzzyMaxEditDistance = DefaultFuzzyMaxEditDistance,
        FuzzyMatchScore = DefaultFuzzyMatchScore,
        NonExactTokenMatchConfidence = 0.85
    };

    // Production-equivalent SiblingPropagator tuning (MatchingConfig.json's match.siblingPropagator).
    private static readonly SiblingPropagator.Config SiblingPropagatorCfg = new() {
        CommonTokenRatio = 0.5,
        CommonTokenFloor = 10,
        SiblingPropagationConfidence = 0.9,
        MinCommonTokens = 2,
        ReferenceGradeTokenLength = 5
    };

    // Production-equivalent FolderNameEnricher tuning (MatchingConfig.json's match.folderNameEnricher).
    private static readonly FolderNameEnricher.Config FolderNameEnricherCfg = new() {
        CameraPrefixes = ["dscn", "dsc", "img", "image", "photo", "pic", "picture", "scan", "p", "capture", "shot"],
        NoiseFolderTokens =
        [
            "hd", "ld", "sd", "web", "print", "packshot", "packshots", "hero", "heroes", "detail", "details",
            "front", "back", "side", "top", "model", "onmodel", "ghost", "flat", "still", "lifestyle",
            "thumb", "thumbs", "thumbnail", "thumbnails", "small", "medium", "large", "xl", "hires", "highres",
            "lowres", "raw", "final", "finals", "edit", "edited", "retouch", "retouched", "images", "image",
            "photos", "photo", "pictures", "picture", "visuals", "visual", "media", "jpg", "jpeg", "png",
            "rgb", "cmyk", "dpi", "px", "new", "old", "copy", "temp", "tmp"
        ],
        MinBareNumberLength = 5,
        MinTokenLengthFloor = 2,
        MinPerItemSiblings = 2,
        MinMeaningfulTokenLength = 3
    };

    //  M1: min-token-length guard (INPUTMA24 false ties)

    [Fact]
    public void Bracket1_ShortRefCoDigitsDoNotTieAgainstFamilyIdToken() {
        // Family B's RefCo "MGGE073" yields digits "073"; the shot suffix "_073" must not drag it
        // into a tie with the exact 8-digit FamilyID match on family A.
        NumericMatcher matcher = new("familyID", MakeNumericCfg(minNumericTokenLength: 5, indexDigitRunsAllColumns: false, minSubstringRescueLength: 0));
        FamilyIDRecord famA = new("94671115");
        FamilyIDRecord famB = MakeFamily("94671189", ("RefCo", "MGGE073", ExcelColumnClassification.Mixed));

        MatchEvidence? evidence = matcher.TryMatchBracket1(MakeLambda("94671115_073.jpg"), [famA, famB], Rules);

        Assert.NotNull(evidence);
        Assert.Equal("94671115", evidence!.FinalFamilyId);
    }

    [Fact]
    public void Bracket1_LegacyMinLengthOne_ShortTokensStillTie() {
        NumericMatcher matcher = new("familyID", MakeNumericCfg(minNumericTokenLength: 1, indexDigitRunsAllColumns: false, minSubstringRescueLength: 0));
        FamilyIDRecord famA = new("94671115");
        FamilyIDRecord famB = MakeFamily("94671189", ("RefCo", "MGGE073", ExcelColumnClassification.Mixed));

        MatchEvidence? evidence = matcher.TryMatchBracket1(MakeLambda("94671115_073.jpg"), [famA, famB], Rules);

        Assert.Null(evidence); // legacy behavior preserved: "073" ties famB against famA
    }

    //  M1: all-columns digit-run index (FILA94 label, INPUTMA27 SKU)

    [Fact]
    public void Bracket1_DigitRunInsideNonRuleColumn_Matches() {
        // The article number lives inside a compound label cell, not in any numeric-rule column.
        NumericMatcher matcher = new("familyID", MakeNumericCfg(minNumericTokenLength: 5, indexDigitRunsAllColumns: true, minSubstringRescueLength: 0));
        FamilyIDRecord family = MakeFamily("98226704", ("label", "MAN-Posy Green-1010930-60105", ExcelColumnClassification.Mixed));

        MatchEvidence? evidence = matcher.TryMatchBracket1(MakeLambda("1010930_A_02.png"), [family], Rules);

        Assert.NotNull(evidence);
        Assert.Equal("98226704", evidence!.FinalFamilyId);
    }

    //  M1: token intersection (FILA94 article+color pair)

    [Fact]
    public void TokenIntersection_SharedColorCodePlusArticleRun_NarrowsToOneFamily() {
        // "60105" (color) appears in both labels; "1010930" (article) appears in both too — but
        // only one family carries the pair in one label. Intersection of per-token hit sets must
        // resolve what single-token lookups cannot.
        NumericMatcher matcher = new("familyID", MakeNumericCfg(minNumericTokenLength: 5, indexDigitRunsAllColumns: true, minSubstringRescueLength: 0));
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
    public void SubstringRescue_TokenInsideEan_MatchesUniqueFamily() {
        NumericMatcher matcher = new("familyID", MakeNumericCfg(minNumericTokenLength: 5, indexDigitRunsAllColumns: false, minSubstringRescueLength: 7));
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
    public void SubstringRescue_ShotNumberWeldedOntoReference_RefusesInsteadOfPickingSideOfTie() {
        // T-5090: "87186790" is a prefix of two EANs. The shot suffix "_1"/"_2" welds onto it
        // ("871867901"/"871867902"), which happens to resolve uniquely to whichever EAN's 9th digit
        // matches — but the honest 8-digit reference "87186790" alone is ambiguous between both
        // families. Evaluating every rescue token (not stopping at the first unique hit) must surface
        // that contradiction and refuse, not silently pick a side.
        NumericMatcher matcher = new("familyID", MakeNumericCfg(minNumericTokenLength: 5, indexDigitRunsAllColumns: false, minSubstringRescueLength: 7));
        FamilyIDRecord famA = MakeFamily("99984905", ("EAN", "8718679018555", ExcelColumnClassification.Numerical));
        FamilyIDRecord famB = MakeFamily("99985047", ("EAN", "8718679021272", ExcelColumnClassification.Numerical));
        MatchingRule eanRule = RefCoRule with { ExcelField = "EAN" };

        (MatchEvidence? evidence1, List<CandidateSummary> tied1) = matcher.TryMatchBySubstringRescue(
            MakeLambda("87186790_1.jpg"), [famA, famB], [FamilyIdRule, eanRule]);
        (MatchEvidence? evidence2, List<CandidateSummary> tied2) = matcher.TryMatchBySubstringRescue(
            MakeLambda("87186790_2.jpg"), [famA, famB], [FamilyIdRule, eanRule]);

        Assert.Null(evidence1);
        Assert.Null(evidence2);
        Assert.Contains(tied1, c => c.FamilyId == "99984905");
        Assert.Contains(tied1, c => c.FamilyId == "99985047");
        Assert.Contains(tied2, c => c.FamilyId == "99984905");
        Assert.Contains(tied2, c => c.FamilyId == "99985047");
    }

    [Fact]
    public void SubstringRescue_Disabled_ReturnsNull() {
        NumericMatcher matcher = new("familyID", MakeNumericCfg(minNumericTokenLength: 5, indexDigitRunsAllColumns: false, minSubstringRescueLength: 0));
        FamilyIDRecord family = MakeFamily("94671120", ("EAN", "8446271023117", ExcelColumnClassification.Numerical));

        MatchingRule eanRule = RefCoRule with { ExcelField = "EAN" };
        Assert.Null(matcher.TryMatchBySubstringRescue(MakeLambda("46271023.jpg"), [family], [FamilyIdRule, eanRule]).Evidence);
    }

    //  M2: Excel bigrams + filename boundary split (HEROAUT3 glued color)

    [Fact]
    public void Bracket3_GluedFilenameToken_MatchesAdjacentExcelTokens() {
        // Filename glues "palm blue" into "palmblue"; the Excel cell holds them adjacent.
        StringMatcher matcher = new(EmptyTranslation, MakeStringCfg(bracket3MinDistinctTokens: 2, identifierTokenMinLength: DefaultIdentifierTokenMinLength, indexExcelTokenBigrams: true));
        FamilyIDRecord famA = MakeFamily("94612975", ("reference", "ANASTASIA AB-PALM BLUE", ExcelColumnClassification.Mixed));
        FamilyIDRecord famB = MakeFamily("94612976", ("reference", "ARIZONA CC-BLUE LEAF", ExcelColumnClassification.Mixed));

        MatchEvidence? evidence = matcher.TryMatch(MakeLambda("Anastasia_palmblue_2.jpg"), [famA, famB]);

        Assert.NotNull(evidence);
        Assert.Equal("94612975", evidence!.FinalFamilyId);
    }

    //  M2: identifier-grade single token (WOODWIC12 reference stems)

    [Fact]
    public void Bracket3_UniqueIdentifierToken_BypassesMinDistinctTokensGate() {
        StringMatcher matcher = new(EmptyTranslation, MakeStringCfg(bracket3MinDistinctTokens: 2, identifierTokenMinLength: 4, indexExcelTokenBigrams: DefaultIndexExcelTokenBigrams));
        FamilyIDRecord famA = MakeFamily("98954095", ("reference", "1707527E", ExcelColumnClassification.Mixed));
        FamilyIDRecord famB = MakeFamily("98954100", ("reference", "2653556E", ExcelColumnClassification.Mixed));

        MatchEvidence? evidence = matcher.TryMatch(MakeLambda("1707527E.jpg"), [famA, famB]);

        Assert.NotNull(evidence);
        Assert.Equal("98954095", evidence!.FinalFamilyId);
    }

    [Fact]
    public void Bracket3_IdentifierBypassDisabled_GateStillRejects() {
        StringMatcher matcher = new(EmptyTranslation, MakeStringCfg(bracket3MinDistinctTokens: 2, identifierTokenMinLength: 0, indexExcelTokenBigrams: DefaultIndexExcelTokenBigrams));
        FamilyIDRecord famA = MakeFamily("98954095", ("reference", "1707527E", ExcelColumnClassification.Mixed));
        FamilyIDRecord famB = MakeFamily("98954100", ("reference", "2653556E", ExcelColumnClassification.Mixed));

        Assert.Null(matcher.TryMatch(MakeLambda("1707527E.jpg"), [famA, famB]));
    }

    //  M2: unresolvable identifier token disqualifies a brand+colour-only match (T-5100, CiMini OMB-E180-BV)

    [Fact]
    public void Bracket3_FilenameReferenceNamesNoFamily_RefusesEvenWhenGenericTokensMatch() {
        // "OMB-E180-BV" tokenizes to omb/e180/bv. omb (brand) and bv (colour) both hit famA — a
        // different, similarly-branded/coloured product — but "e180" (the actual reference) is in no
        // row at all. 2-of-3 tokens would otherwise clear bracket3MinDistinctTokens=2 and hand the
        // image to famA; the unresolvable identifier-grade token must refuse the match instead.
        StringMatcher matcher = new(EmptyTranslation, MakeStringCfg(bracket3MinDistinctTokens: 2, identifierTokenMinLength: 4, indexExcelTokenBigrams: DefaultIndexExcelTokenBigrams));
        FamilyIDRecord famA = MakeFamily("98636303",
            ("brand", "OMB", ExcelColumnClassification.Categorical),
            ("colour", "BV", ExcelColumnClassification.Categorical),
            ("reference", "E166", ExcelColumnClassification.Mixed));

        Assert.Null(matcher.TryMatch(MakeLambda("OMB-E180-BV_1.jpg"), [famA]));
    }

    [Fact]
    public void Bracket3_FilenameReferenceMatchesItsFamily_StillMatches() {
        // Control case: same brand+colour shape, but the reference IS in the catalogue (E166, not
        // E180) — must still match normally. Guards against the T-5100 fix over-refusing.
        StringMatcher matcher = new(EmptyTranslation, MakeStringCfg(bracket3MinDistinctTokens: 2, identifierTokenMinLength: 4, indexExcelTokenBigrams: DefaultIndexExcelTokenBigrams));
        FamilyIDRecord famA = MakeFamily("98636303",
            ("brand", "OMB", ExcelColumnClassification.Categorical),
            ("colour", "BV", ExcelColumnClassification.Categorical),
            ("reference", "E166", ExcelColumnClassification.Mixed));

        MatchEvidence? evidence = matcher.TryMatch(MakeLambda("OMB-E166-BV_1.jpg"), [famA]);

        Assert.NotNull(evidence);
        Assert.Equal("98636303", evidence!.FinalFamilyId);
    }

    //  M2: short-digit tiebreak (CiMini color-code discrimination)

    [Fact]
    public void Bracket3_TopTie_ShortDigitTokenDiscriminates() {
        // Both families are cardigans in magenta; only famA carries color code "76".
        StringMatcher matcher = new(EmptyTranslation, MakeStringCfg(bracket3MinDistinctTokens: 2, identifierTokenMinLength: DefaultIdentifierTokenMinLength, indexExcelTokenBigrams: DefaultIndexExcelTokenBigrams));
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
    public void SiblingPropagator_KeylessShot_InheritsSiblingFamily() {
        SiblingPropagator propagator = new(SiblingPropagatorCfg);

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
    public void SiblingPropagator_ShortLetterDigitToken_KeptWholeNotTreatedAsShotNumber() {
        // T-5100/T-5210: "OMB-E180-BV" and "OMB-E166-BV" must NOT be treated as siblings. Naively
        // splitting "e180"/"e166" at the letter-digit boundary yields "e" (too short, dropped) and
        // "180"/"166" (3 digits, discarded by ShotSuffixPattern as a bare shot number) — both profiles
        // then collapse to the same {omb, bv}, and SiblingPropagator would independently re-derive the
        // exact match Bracket 3 (StringMatcher's HasUnresolvableIdentifierToken) correctly refuses.
        // Keeping "e180"/"e166" whole (short letter-prefix + digits = reference-code shape, not a
        // splittable word+digits pair like "magenta76") keeps the profiles distinct.
        SiblingPropagator propagator = new(SiblingPropagatorCfg);

        ImageRecord_LAMBDA matched = MakeLambda("OMB-E166-BV_1.jpg");
        matched.MatchEvidence = new MatchEvidence { ImageId = "a", FinalFamilyId = "98636303", IsKo = false };

        ImageRecord_LAMBDA unresolvable = MakeLambda("OMB-E180-BV_1.jpg");
        List<ImageRecord_LAMBDA> allRecords = [matched, unresolvable];

        List<ImageRecord_LAMBDA> stillUnmatched = propagator.Run([unresolvable], allRecords);

        Assert.Contains(unresolvable, stillUnmatched);
        Assert.Null(unresolvable.MatchEvidence);
    }

    [Fact]
    public void SiblingPropagator_ConflictingSiblings_DoesNotPropagate() {
        // Two different products both have magenta cardigan shots that reduce to {cardigan, magenta}.
        // The exact profile is owned by two families, so it is NOT a safe key, and a third keyless
        // shot with that same profile has no way to choose — it stays unmatched.
        SiblingPropagator propagator = new(SiblingPropagatorCfg);

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
    public void SiblingPropagator_ThirdShotOfOneProduct_JoinsDespiteOverlapWithAnother() {
        // One product (90861052) has two matched magenta-cardigan shots. A third shot, CARDIGAN_MAGENTA76_C,
        // reduces to the same {cardigan, magenta} profile. A DIFFERENT product (90861099) has one matched
        // cardigan shot that only loosely overlaps. The exact profile {cardigan,magenta} is owned by exactly
        // one family, so the third shot joins that family instead of being refused.
        SiblingPropagator propagator = new(SiblingPropagatorCfg);

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
    public void FolderNameEnricher_MeaninglessFileInMeaningfulFolder_BorrowsFolderName() {
        // Filenames are meaningless (1.jpg, 2.jpg); the folders are one-per-product and a folder token
        // (the reference SH23005) appears in the Excel data. The folder name is borrowed for matching.
        FolderNameEnricher enricher = new(FolderNameEnricherCfg);
        FamilyIDRecord famA = MakeFamily("98765432", ("reference", "earphones zenith SH23005 pro", ExcelColumnClassification.Mixed));
        FamilyIDRecord famB = MakeFamily("98765433", ("reference", "earphones apex SH23006 pro", ExcelColumnClassification.Mixed));

        ImageRecord_LAMBDA img = MakeLambda("C:/drop/earphones_zenith_SH23005/1.jpg");
        ImageRecord_LAMBDA sibling = MakeLambda("C:/drop/earphones_apex_SH23006/1.jpg");
        List<ImageRecord_LAMBDA> records = [img, sibling];

        enricher.Enrich(records, [famA, famB]);

        Assert.Equal("earphones_zenith_SH23005 1.jpg", img.MatchingAlias);
        Assert.Contains("SH23005", img.MatchingName);

        // And the borrowed name now matches: StringMatcher finds the unique family via the folder tokens.
        StringMatcher matcher = new(EmptyTranslation, MakeStringCfg(bracket3MinDistinctTokens: 2, identifierTokenMinLength: 4, indexExcelTokenBigrams: true));
        MatchEvidence? evidence = matcher.TryMatch(img, [famA, famB]);
        Assert.Equal("98765432", evidence?.FinalFamilyId);
    }

    [Fact]
    public void FolderNameEnricher_FormatFolder_IsNotBorrowed() {
        // Folders describe format/size, not the product — nothing is borrowed.
        FolderNameEnricher enricher = new(FolderNameEnricherCfg);
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
    public void FolderNameEnricher_MeaningfulFilename_IsLeftAlone() {
        // The filename already carries a product word — the folder is never borrowed, even if meaningful.
        FolderNameEnricher enricher = new(FolderNameEnricherCfg);
        FamilyIDRecord fam = MakeFamily("98765432", ("reference", "zenith SH23005", ExcelColumnClassification.Mixed), ("model", "anastasia", ExcelColumnClassification.Categorical));

        ImageRecord_LAMBDA img = MakeLambda("C:/drop/zenith_SH23005/Anastasia_front.jpg");
        ImageRecord_LAMBDA sibling = MakeLambda("C:/drop/apex_SH23006/Betty_front.jpg");

        enricher.Enrich(img is null ? [] : [img, sibling], [fam]);

        Assert.Null(img.MatchingAlias);
    }

    [Fact]
    public void FolderNameEnricher_FolderTokenNotInExcel_IsNotBorrowed() {
        // The folder is a per-product pattern, but none of its tokens appear in the Excel — no borrow.
        FolderNameEnricher enricher = new(FolderNameEnricherCfg);
        FamilyIDRecord fam = MakeFamily("98765432", ("reference", "totally unrelated", ExcelColumnClassification.Mixed));

        ImageRecord_LAMBDA img = MakeLambda("C:/drop/mystery_widget_QQ111/1.jpg");
        ImageRecord_LAMBDA sibling = MakeLambda("C:/drop/mystery_gadget_QQ222/1.jpg");

        enricher.Enrich([img, sibling], [fam]);

        Assert.Null(img.MatchingAlias);
    }

    //  T-5020 cause 3: mixed letter+digit run split (foldercontainsID99984905)

    [Fact]
    public void FolderNameEnricher_MixedRunSplitsAtLetterDigitBoundary_BorrowsFolderName() {
        // CiMini's real failure: the folder yields exactly one whole-run token
        // ("foldercontainsid99984905") that never appears in the Excel, while the FamilyID it should
        // borrow ("99984905") sits right there as the digit tail. Splitting the run in addition to
        // keeping it whole is what makes this folder meaningful.
        FolderNameEnricher enricher = new(FolderNameEnricherCfg);
        FamilyIDRecord fam = new("99984905");
        FamilyIDRecord famSibling = new("99984906");

        ImageRecord_LAMBDA img = MakeLambda("C:/drop/foldercontainsID99984905/1.jpg");
        ImageRecord_LAMBDA sibling = MakeLambda("C:/drop/foldercontainsID99984906/2.jpg");

        enricher.Enrich([img, sibling], [fam, famSibling]);

        Assert.Equal("foldercontainsID99984905 1.jpg", img.MatchingAlias);
    }

    [Fact]
    public void FolderNameEnricher_WholeRunSurvivesWhenDigitTailTooShortForBareNumberGate_BorrowsFolderName() {
        // Pins that the whole-run token (SH23005-style codes) still gets emitted after the split was
        // added, not replaced by it. A digit tail of "23005" (5 digits) would independently clear
        // MinBareNumberLength on its own, so it cannot prove the whole run is still reachable; a
        // shorter tail ("005", 3 digits, below the 5-digit floor) isolates it — only the whole run
        // "sh005" carries meaning here, the split digit piece is filtered out.
        FolderNameEnricher enricher = new(FolderNameEnricherCfg);
        FamilyIDRecord fam = MakeFamily("98765450", ("reference", "SH005", ExcelColumnClassification.Mixed));
        FamilyIDRecord famSibling = MakeFamily("98765451", ("reference", "SH006", ExcelColumnClassification.Mixed));

        ImageRecord_LAMBDA img = MakeLambda("C:/drop/SH005/1.jpg");
        ImageRecord_LAMBDA sibling = MakeLambda("C:/drop/SH006/2.jpg");

        enricher.Enrich([img, sibling], [fam, famSibling]);

        Assert.Equal("SH005 1.jpg", img.MatchingAlias);
    }

    [Fact]
    public void FolderNameEnricher_OnlyDigitTailInExcel_StillBorrowsFolderName() {
        // Generalizes the foldercontainsID case beyond CiMini's exact numbers: the letter prefix
        // ("batchinv") never appears anywhere in the Excel data, only the digit tail does. The split
        // piece — not the whole run — is the only reason this folder qualifies as meaningful.
        FolderNameEnricher enricher = new(FolderNameEnricherCfg);
        FamilyIDRecord fam = new("87654321");
        FamilyIDRecord famSibling = new("87654322");

        ImageRecord_LAMBDA img = MakeLambda("C:/drop/batchINV87654321/1.jpg");
        ImageRecord_LAMBDA sibling = MakeLambda("C:/drop/batchINV87654322/2.jpg");

        enricher.Enrich([img, sibling], [fam, famSibling]);

        Assert.Equal("batchINV87654321 1.jpg", img.MatchingAlias);
    }

    //  Phase E: orphan row join (MEPAL4 catalog → bundle Ref)

    [Fact]
    public void OrphanRowJoiner_ArticleNumberInsideBundleRef_JoinsRowToFamily() {
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
    public void OrphanRowJoiner_KeySharedByTwoFamilies_DoesNotJoin() {
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

    private static FamilyIDRecord MakeFamily(string familyId, params (string Name, string Value, ExcelColumnClassification Classification)[] properties) {
        FamilyIDRecord family = new(familyId);
        foreach ((string name, string value, ExcelColumnClassification classification) in properties)
            family.MergeProperty(new ExcelPropertyValue(name, [value], []), classification);
        return family;
    }
}
