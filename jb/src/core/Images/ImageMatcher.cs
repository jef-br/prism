namespace Prism.Core;

/// <summary>
/// Waterfall matching orchestrator for the Matched pipeline stage.
/// Runs numeric (brackets 1–2), string (bracket 3), semantic combined (bracket 4), and
/// CLIP label enrichment in sequence. Matched images are removed from subsequent brackets.
/// Remaining images are KO'd in bracket 5.
/// </summary>
internal sealed class ImageMatcher {
    private readonly MatchingConfig matchingConfig;
    private readonly NumericMatcher numericMatcher;
    private readonly StringMatcher stringMatcher;
    private readonly ClipLabelEnricher clipLabelEnricher;
    private readonly SemanticMatcher semanticMatcher;
    private readonly FilenameToCellMatcher filenameToCellMatcher;
    private readonly SiblingPropagator siblingPropagator;

    private ImageMatcher( MatchingConfig matchingConfig, TranslationConfig translationConfig, string familyIdColumnName ) {
        this.matchingConfig = matchingConfig;
        numericMatcher = new NumericMatcher(
            familyIdColumnName,
            matchingConfig.MinNumericTokenLength,
            matchingConfig.IndexDigitRunsAllColumns,
            matchingConfig.MinSubstringRescueLength);
        stringMatcher = new StringMatcher(
            translationConfig,
            matchingConfig.Bracket3MinDistinctTokens,
            matchingConfig.IdentifierTokenMinLength,
            matchingConfig.IndexExcelTokenBigrams);
        clipLabelEnricher = new ClipLabelEnricher();
        filenameToCellMatcher = new FilenameToCellMatcher();
        siblingPropagator = new SiblingPropagator();
        semanticMatcher = new SemanticMatcher(
            numericMatcher,
            stringMatcher,
            clipLabelEnricher,
            matchingConfig.SemanticThreshold,
            matchingConfig.SemanticWeight);
    }

    /// <summary>
    /// Entry point called by the Matching service.
    /// Loads configs, runs the waterfall, and writes MatchEvidence to every lambda record.
    /// </summary>
    /// <param name="records">LAMBDA records to match against the family catalogue.</param>
    /// <param name="families">Family records resolved from the Internal Excel Model.</param>
    /// <returns>Number of records KO'd because no FamilyID match was found.</returns>
    internal static int Run( List<ImageRecord_LAMBDA> records, IReadOnlyList<FamilyIDRecord> families ) {
        string matchingConfigPath = LoadConfigPath(
            "MatchingConfig.json",
            "MatchingConfig.json not found in the config directory next to Prism_Config.json.");

        string translationConfigPath = LoadConfigPath(
            "TranslationDictionary.json",
            "TranslationDictionary.json not found in the config directory next to Prism_Config.json.");

        string excelConfigPath = LoadConfigPath(
            "ExcelConfig.json",
            "ExcelConfig.json not found in the config directory next to Prism_Config.json.");

        MatchingConfig matchingConfig = ConfigCache.GetOrLoad(
            () => MatchingConfig.Load(matchingConfigPath), matchingConfigPath);
        TranslationConfig translationConfig = ConfigCache.GetOrLoad(
            () => TranslationConfig.Load(translationConfigPath), translationConfigPath);
        ExcelConfig excelConfig = ConfigCache.GetOrLoad(
            () => ExcelConfig.Load(excelConfigPath), excelConfigPath);

        string? prismConfigPath = PrismConfigLocator.FindPrismConfigPath();
        if (prismConfigPath is null)
            throw new PrismConfigurationException("Prism_Config.json not found — cannot load convergence weight.");

        PrismConfiguration prismConfig = ConfigCache.GetOrLoad(
            () => PrismConfiguration.LoadPrismConfig(prismConfigPath), prismConfigPath);

        ImageMatcher matcher = new(matchingConfig, translationConfig, excelConfig.RecordPrimaryKey);
        return matcher.RunWaterfall(records, families, prismConfig.Weight_MatchingSignalsConverging);
    }

    //  Waterfall 

    private int RunWaterfall(
        List<ImageRecord_LAMBDA> allRecords,
        IReadOnlyList<FamilyIDRecord> families,
        double convergenceWeight ) {
        IReadOnlyList<MatchingRule> numericRules = matchingConfig.NumericRules;
        IReadOnlyList<MatchingRule> labelRules = matchingConfig.LabelRules;

        // Keyed by InitialFullName; holds tied candidates passed over in Brackets 1–2.
        Dictionary<string, List<CandidateSummary>> rejectedNearTies = new(StringComparer.OrdinalIgnoreCase);

        // Keyed by InitialFullName; accumulates every FamilyID an image was a candidate for across all brackets.
        Dictionary<string, HashSet<string>> crossBracketCandidates = new(StringComparer.OrdinalIgnoreCase);

        List<ImageRecord_LAMBDA> unmatched = allRecords.Where(r => !r.IsKo).ToList();

        // Bracket 1: single numeric token, TCD = 0
        unmatched = RunBracket1(unmatched, families, numericRules, rejectedNearTies, crossBracketCandidates);

        // Bracket 2: multi-token numeric concatenation, TCD ≤ maxDistance
        unmatched = RunBracket2(unmatched, families, numericRules, rejectedNearTies, crossBracketCandidates);

        // Bracket 2-Intersect: per-token candidate sets intersect to exactly one FamilyID
        unmatched = RunBracket2Intersect(unmatched, families, numericRules, rejectedNearTies);

        // Bracket 3: string tokens, exactly-1-FamilyID
        unmatched = RunBracket3(unmatched, allRecords, families, rejectedNearTies, crossBracketCandidates);

        // Bracket 4: semantic combined (CLIP + numeric + string) for 0-image families.
        // Skip entirely when no image in the batch carries CLIP classification signal at all.
        // Safe because MatchingConfig.json always defines ProductType + ProductColor ClipLabelEnricher
        // rules (verified in jb/src/core/config/MatchingConfig.json) — ClipLabelEnricher.BuildEvidence
        // returns [] whenever Influential.Length == 0, so FilterByClipProductType/Color already reject
        // every candidate for an untagged record today. If those two rules are ever both removed from
        // MatchingConfig.json, re-verify this gate's safety.
        bool hasClassificationSignal = allRecords.Any(r => r.Tags.Influential.Length > 0);
        unmatched = hasClassificationSignal
            ? RunBracket4(unmatched, allRecords, families, numericRules, labelRules, rejectedNearTies)
            : unmatched;

        // Bracket 5: image filename named verbatim in an Excel cell (exact, unique)
        unmatched = RunBracket5FilenameToCell(unmatched, families, rejectedNearTies);

        // Substring rescue: unique family whose digit target contains a long filename token
        unmatched = RunSubstringRescue(unmatched, families, numericRules, rejectedNearTies);

        // Sibling propagation: inherit the FamilyID of the unique matched sibling image
        if (matchingConfig.EnableSiblingPropagation)
            unmatched = siblingPropagator.Run(unmatched, allRecords);

        // Add CLIP label evidence to already-matched records (no new assignments)
        AddClipLabelEvidence(allRecords, families, labelRules);

        // Bracket 6 cleanup: KO any image still without a FamilyID assignment
        int koAdded = KoUnmatched(unmatched, crossBracketCandidates, families);

        // Bracket 7: finalize clustering (single-pass waterfall means no structural ties)
        FinalizeMatches(allRecords, convergenceWeight);

        return koAdded;
    }

    //  Bracket 1 

    /// <summary>
    /// Runs NumericMatcher single-token bracket. Returns images not yet matched.
    /// Records tied candidates in <paramref name="rejectedNearTies"/> for later attachment.
    /// </summary>
    private List<ImageRecord_LAMBDA> RunBracket1(
        List<ImageRecord_LAMBDA> candidates,
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules,
        Dictionary<string, List<CandidateSummary>> rejectedNearTies,
        Dictionary<string, HashSet<string>> crossBracketCandidates ) {
        List<ImageRecord_LAMBDA> stillUnmatched = [];
        double numericWeight = numericRules.Count > 0 ? numericRules[0].Weight : 1.0;

        foreach (ImageRecord_LAMBDA record in candidates) {
            string key = record.InitialFullName ?? string.Empty;
            (MatchEvidence? evidence, List<CandidateSummary> tiedCandidates) =
                numericMatcher.TryMatchBracket1WithTies(record, families, numericRules);

            if (tiedCandidates.Count > 1) {
                rejectedNearTies[key] = tiedCandidates;
                AccumulateCandidates(crossBracketCandidates, key, tiedCandidates);
            }

            if (evidence is not null) {
                record.MatchEvidence = evidence with {
                    ThresholdStatus = evidence.FinalScore >= matchingConfig.SemanticThreshold,
                    RejectedNearTieEvidence = GetRejectedTies(rejectedNearTies, key),
                    MatcherWeights = [new MatcherContribution("NumericMatcher.Bracket1", numericWeight, evidence.FinalScore)]
                };
            }
            else {
                stillUnmatched.Add(record);
            }
        }

        return stillUnmatched;
    }

    /// <summary>Returns the rejected near-tie list for <paramref name="key"/>, or an empty list.</summary>
    private static IReadOnlyList<CandidateSummary> GetRejectedTies(
        Dictionary<string, List<CandidateSummary>> rejectedNearTies, string key ) =>
        rejectedNearTies.TryGetValue(key, out List<CandidateSummary>? ties) ? ties : [];

    //  Bracket 2 

    /// <summary>
    /// Runs NumericMatcher multi-token bracket. Returns images not yet matched.
    /// Records tied candidates in <paramref name="rejectedNearTies"/> for later attachment.
    /// </summary>
    private List<ImageRecord_LAMBDA> RunBracket2(
        List<ImageRecord_LAMBDA> candidates,
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules,
        Dictionary<string, List<CandidateSummary>> rejectedNearTies,
        Dictionary<string, HashSet<string>> crossBracketCandidates ) {
        List<ImageRecord_LAMBDA> stillUnmatched = [];
        double numericWeight = numericRules.Count > 0 ? numericRules[0].Weight : 1.0;

        foreach (ImageRecord_LAMBDA record in candidates) {
            string key = record.InitialFullName ?? string.Empty;
            (MatchEvidence? evidence, List<CandidateSummary> tiedCandidates) =
                numericMatcher.TryMatchBracket2WithTies(record, families, numericRules);

            if (tiedCandidates.Count > 1) {
                rejectedNearTies[key] = tiedCandidates;
                AccumulateCandidates(crossBracketCandidates, key, tiedCandidates);
            }

            if (evidence is not null) {
                record.MatchEvidence = evidence with {
                    ThresholdStatus = evidence.FinalScore >= matchingConfig.SemanticThreshold,
                    RejectedNearTieEvidence = GetRejectedTies(rejectedNearTies, key),
                    MatcherWeights = [new MatcherContribution("NumericMatcher.Bracket2", numericWeight, evidence.FinalScore)]
                };
            }
            else {
                stillUnmatched.Add(record);
            }
        }

        return stillUnmatched;
    }

    //  Bracket 2-Intersect

    /// <summary>
    /// Runs NumericMatcher token-intersection bracket: tokens that individually tie across several
    /// families can jointly narrow to exactly one. Returns images not yet matched.
    /// </summary>
    private List<ImageRecord_LAMBDA> RunBracket2Intersect(
        List<ImageRecord_LAMBDA> candidates,
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules,
        Dictionary<string, List<CandidateSummary>> rejectedNearTies ) {
        List<ImageRecord_LAMBDA> stillUnmatched = [];
        double numericWeight = numericRules.Count > 0 ? numericRules[0].Weight : 1.0;

        foreach (ImageRecord_LAMBDA record in candidates) {
            string key = record.InitialFullName ?? string.Empty;
            MatchEvidence? evidence = numericMatcher.TryMatchByTokenIntersection(record, families, numericRules);

            if (evidence is not null) {
                record.MatchEvidence = evidence with {
                    ThresholdStatus = evidence.FinalScore >= matchingConfig.SemanticThreshold,
                    RejectedNearTieEvidence = GetRejectedTies(rejectedNearTies, key),
                    MatcherWeights = [new MatcherContribution("NumericMatcher.Bracket2-Intersect", numericWeight, evidence.FinalScore)]
                };
            }
            else {
                stillUnmatched.Add(record);
            }
        }

        return stillUnmatched;
    }

    //  Bracket 3

    /// <summary>
    /// Runs StringMatcher exactly-1-FamilyID bracket. Returns images not yet matched.
    /// Rejects an otherwise-valid string match when the target FamilyID already has a
    /// non-KO record with the same SelectedPhenotype (PRISM-match.md: no duplicate image type
    /// per family from Bracket 3).
    /// </summary>
    private List<ImageRecord_LAMBDA> RunBracket3(
        List<ImageRecord_LAMBDA> candidates,
        List<ImageRecord_LAMBDA> allRecords,
        IReadOnlyList<FamilyIDRecord> families,
        Dictionary<string, List<CandidateSummary>> rejectedNearTies,
        Dictionary<string, HashSet<string>> crossBracketCandidates ) {
        List<ImageRecord_LAMBDA> stillUnmatched = [];

        foreach (ImageRecord_LAMBDA record in candidates) {
            string key = record.InitialFullName ?? string.Empty;
            MatchEvidence? evidence = stringMatcher.TryMatch(record, families);

            if (evidence is not null && HasDuplicatePhenotypeInFamily(
                    evidence.FinalFamilyId, record.SelectedPhenotype, record, allRecords)) {
                if (evidence.FinalFamilyId is not null)
                    AccumulateCandidates(crossBracketCandidates, key, evidence.FinalFamilyId);
                evidence = null;
            }

            if (evidence is not null) {
                record.MatchEvidence = evidence with {
                    ThresholdStatus = evidence.FinalScore >= matchingConfig.SemanticThreshold,
                    RejectedNearTieEvidence = GetRejectedTies(rejectedNearTies, key),
                    MatcherWeights = [new MatcherContribution("StringMatcher.Bracket3", 1.0, evidence.FinalScore)]
                };
            }
            else {
                stillUnmatched.Add(record);
            }
        }

        return stillUnmatched;
    }

    /// <summary>
    /// Returns true when <paramref name="familyId"/> already has a non-KO matched record (other
    /// than <paramref name="self"/>) with the same non-null <paramref name="phenotype"/>.
    /// </summary>
    private static bool HasDuplicatePhenotypeInFamily(
        string? familyId,
        string? phenotype,
        ImageRecord_LAMBDA self,
        List<ImageRecord_LAMBDA> allRecords ) {
        if (familyId is null || phenotype is null)
            return false;

        return allRecords.Any(r =>
            !ReferenceEquals(r, self)
            && !r.IsKo
            && r.MatchEvidence?.FinalFamilyId is not null
            && string.Equals(r.MatchEvidence.FinalFamilyId, familyId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(r.SelectedPhenotype, phenotype, StringComparison.OrdinalIgnoreCase));
    }

    //  Bracket 4: semantic combined 

    /// <summary>
    /// Runs SemanticMatcher (CLIP + numeric + string) against FamilyIDs with 0 assigned images.
    /// Returns images still unmatched after this bracket.
    /// </summary>
    private List<ImageRecord_LAMBDA> RunBracket4(
        List<ImageRecord_LAMBDA> candidates,
        List<ImageRecord_LAMBDA> allRecords,
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules,
        IReadOnlyList<MatchingRule> labelRules,
        Dictionary<string, List<CandidateSummary>> rejectedNearTies ) {
        HashSet<string> assignedFamilyIds = allRecords
            .Where(r => !r.IsKo && r.MatchEvidence?.FinalFamilyId is not null)
            .Select(r => r.MatchEvidence!.FinalFamilyId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<FamilyIDRecord> unassignedFamilies = families
            .Where(f => !assignedFamilyIds.Contains(f.FamilyID))
            .ToList();

        if (unassignedFamilies.Count == 0)
            return candidates;

        List<ImageRecord_LAMBDA> stillUnmatched = [];

        foreach (ImageRecord_LAMBDA record in candidates) {
            string key = record.InitialFullName ?? string.Empty;
            MatchEvidence? evidence = semanticMatcher.TryMatch(
                record, unassignedFamilies, numericRules, labelRules);

            if (evidence is not null) {
                record.MatchEvidence = evidence with {
                    ThresholdStatus = evidence.FinalScore >= matchingConfig.SemanticThreshold,
                    RejectedNearTieEvidence = GetRejectedTies(rejectedNearTies, key),
                    MatcherWeights =
                    [
                        new MatcherContribution("SemanticMatcher.Bracket4", matchingConfig.SemanticWeight, evidence.FinalScore)
                    ]
                };
            }
            else {
                stillUnmatched.Add(record);
            }
        }

        return stillUnmatched;
    }

    //  Bracket 5: filename present in an Excel cell

    /// <summary>
    /// Runs FilenameToCellMatcher: assigns an image to the unique FamilyID whose Excel row names
    /// that exact image file in any cell. Matches against all families (a family may already hold
    /// other images). Returns images still unmatched after this bracket.
    /// </summary>
    private List<ImageRecord_LAMBDA> RunBracket5FilenameToCell(
        List<ImageRecord_LAMBDA> candidates,
        IReadOnlyList<FamilyIDRecord> families,
        Dictionary<string, List<CandidateSummary>> rejectedNearTies ) {
        List<ImageRecord_LAMBDA> stillUnmatched = [];

        foreach (ImageRecord_LAMBDA record in candidates) {
            string key = record.InitialFullName ?? string.Empty;
            MatchEvidence? evidence = filenameToCellMatcher.TryMatch(record, families);

            if (evidence is not null) {
                record.MatchEvidence = evidence with {
                    ThresholdStatus = evidence.FinalScore >= matchingConfig.SemanticThreshold,
                    RejectedNearTieEvidence = GetRejectedTies(rejectedNearTies, key),
                    MatcherWeights = [new MatcherContribution("FilenameToCellMatcher", 1.0, evidence.FinalScore)]
                };
            }
            else {
                stillUnmatched.Add(record);
            }
        }

        return stillUnmatched;
    }

    //  Substring rescue

    /// <summary>
    /// Runs NumericMatcher substring rescue: accepts the unique family one of whose digit targets
    /// contains a long filename token. Returns images still unmatched after this pass.
    /// </summary>
    private List<ImageRecord_LAMBDA> RunSubstringRescue(
        List<ImageRecord_LAMBDA> candidates,
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules,
        Dictionary<string, List<CandidateSummary>> rejectedNearTies ) {
        List<ImageRecord_LAMBDA> stillUnmatched = [];

        foreach (ImageRecord_LAMBDA record in candidates) {
            string key = record.InitialFullName ?? string.Empty;
            MatchEvidence? evidence = numericMatcher.TryMatchBySubstringRescue(record, families, numericRules);

            if (evidence is not null) {
                record.MatchEvidence = evidence with {
                    ThresholdStatus = evidence.FinalScore >= matchingConfig.SemanticThreshold,
                    RejectedNearTieEvidence = GetRejectedTies(rejectedNearTies, key),
                    MatcherWeights = [new MatcherContribution("NumericMatcher.SubstringRescue", 1.0, evidence.FinalScore)]
                };
            }
            else {
                stillUnmatched.Add(record);
            }
        }

        return stillUnmatched;
    }

    //  CLIP label enrichment

    /// <summary>
    /// Appends CLIP label evidence to the MatchEvidence of already-matched records.
    /// Never creates or overrides FamilyID assignments.
    /// </summary>
    private void AddClipLabelEvidence(
        List<ImageRecord_LAMBDA> allRecords,
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> labelRules ) {
        if (labelRules.Count == 0)
            return;

        foreach (ImageRecord_LAMBDA record in allRecords) {
            if (record.IsKo || record.MatchEvidence?.FinalFamilyId is null)
                continue;

            IReadOnlyList<LabelEvidenceItem> clipEvidence =
                clipLabelEnricher.BuildEvidence(record, families, labelRules);

            if (clipEvidence.Count == 0)
                continue;

            record.MatchEvidence = record.MatchEvidence with {
                ClassificationLabelEvidence =
                [
                    ..record.MatchEvidence.ClassificationLabelEvidence,
                    ..clipEvidence
                ]
            };
        }
    }

    //  Bracket 5 cleanup 

    /// <summary>
    /// KOs any image that was not matched by brackets 1–5.
    /// Images that were candidates for 2+ FamilyIDs receive <c>MATCHES_MULTIPLE_FAMILYIDS</c>;
    /// images whose stem carries a well-formed FamilyID that the catalogue simply does not contain
    /// receive <c>NOT_IN_CATALOG</c>; images with no signal at all receive <c>MATCH_NOT_FOUND</c>.
    /// </summary>
    /// <returns>Number of records KO'd.</returns>
    private static int KoUnmatched( List<ImageRecord_LAMBDA> unmatched, IReadOnlyDictionary<string, HashSet<string>> crossBracketCandidates, IReadOnlyList<FamilyIDRecord> families ) {
        HashSet<string> knownFamilyIds = families
            .Select(f => f.FamilyID)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (ImageRecord_LAMBDA record in unmatched) {
            string sourceFilename = record.InitialFullName ?? string.Empty;
            string imageId = Path.GetFileNameWithoutExtension(sourceFilename);

            bool multiFamily = crossBracketCandidates.TryGetValue(sourceFilename, out HashSet<string>? seen)
                               && seen.Count >= 2;
            bool outOfCatalog = !multiFamily && StemCarriesUnknownFamilyId(imageId, knownFamilyIds);

            string candidates = multiFamily ? string.Join(", ", seen!) : string.Empty;
            string reasonCode = multiFamily ? "MATCHES_MULTIPLE_FAMILYIDS" : outOfCatalog ? "NOT_IN_CATALOG" : "MATCH_NOT_FOUND";
            string safeMsg = multiFamily ? $"{candidates}" : $"{imageId}";
            string explanation = multiFamily
                ? $"'{imageId}' qualifies for {seen!.Count} FamilyIDs. Has to be exactly one."
                : outOfCatalog
                    ? $"'{imageId}' names a FamilyID that is not in the supplied Excel data."
                    : $"'{imageId}': no unique FamilyID match.";
            record.IsKo = true;
            record.KoReasonCode = reasonCode;
            record.KoSafeMessage = safeMsg;
            record.MatchEvidence = new MatchEvidence {
                ImageId = imageId,
                SourceFilename = sourceFilename,
                IsKo = true,
                KoReason = reasonCode,
                ThresholdStatus = false,
                SafeExplanation = explanation
            };
        }

        return unmatched.Count;
    }

    /// <summary>
    /// True when the stem contains an 8-digit run that looks like a PRISM FamilyID but is absent
    /// from the supplied families — the image belongs to a product outside this batch's catalogue.
    /// </summary>
    private static bool StemCarriesUnknownFamilyId( string imageId, HashSet<string> knownFamilyIds ) {
        foreach (System.Text.RegularExpressions.Match run in System.Text.RegularExpressions.Regex.Matches(imageId, @"\d+")) {
            if (run.Value.Length == 8 && !knownFamilyIds.Contains(run.Value))
                return true;
        }

        return false;
    }

    /// <summary>Adds all FamilyIds from <paramref name="candidates"/> to the cross-bracket accumulator for <paramref name="key"/>.</summary>
    private static void AccumulateCandidates( Dictionary<string, HashSet<string>> accumulator, string key, IEnumerable<CandidateSummary> candidates ) {
        if (!accumulator.TryGetValue(key, out HashSet<string>? set))
            accumulator[key] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CandidateSummary c in candidates) set.Add(c.FamilyId);
    }

    /// <summary>Adds a single <paramref name="familyId"/> to the cross-bracket accumulator for <paramref name="key"/>.</summary>
    private static void AccumulateCandidates( Dictionary<string, HashSet<string>> accumulator, string key, string familyId ) {
        if (!accumulator.TryGetValue(key, out HashSet<string>? set))
            accumulator[key] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        set.Add(familyId);
    }

    //  Bracket 6: finalize 

    /// <summary>
    /// Finalizes FamilyID clusters. Applies the convergence bonus to matched records whose
    /// evidence spans at least two distinct signal types (NumericToken, StringToken, ClassificationLabel).
    /// T-700 reads record.MatchEvidence.FinalFamilyId to build det-order clusters.
    /// </summary>
    private static void FinalizeMatches( List<ImageRecord_LAMBDA> allRecords, double convergenceWeight ) {
        foreach (ImageRecord_LAMBDA record in allRecords) {
            if (record.IsKo || record.MatchEvidence is null)
                continue;

            if (!Converges(record.MatchEvidence))
                continue;

            record.MatchEvidence = record.MatchEvidence with {
                FinalScore = Math.Min(1.0, record.MatchEvidence.FinalScore + convergenceWeight),
                SafeExplanation = record.MatchEvidence.SafeExplanation + $" [convergence bonus +{convergenceWeight:F2}]"
            };
        }
    }

    /// <summary>Returns true when the evidence contains at least two distinct signal types.</summary>
    private static bool Converges( MatchEvidence me ) {
        int signalCount = 0;
        if (me.NumericTokenEvidence.Count > 0) signalCount++;
        if (me.StringTokenEvidence.Count > 0) signalCount++;
        if (me.ClassificationLabelEvidence.Count > 0) signalCount++;
        return signalCount >= 2;
    }

    //  Config loading 

    private static string LoadConfigPath( string relativePath, string missingMessage ) {
        string? path = PrismConfigLocator.FindFolderLocalConfig(relativePath);
        if (path is null)
            throw new PrismConfigurationException(missingMessage);

        return path;
    }
}