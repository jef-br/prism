namespace Prism.Services.Matching;

/// <summary>
/// Waterfall matching orchestrator for the Matched pipeline stage.
/// Runs numeric (brackets 1–2), string (bracket 3), then semantic combined (bracket 4) plus its
/// three continuation passes (filename-to-cell, substring rescue, sibling propagation) for whatever
/// Bracket 4 alone didn't resolve. Matched images are removed from subsequent brackets. Remaining
/// images are KO'd once the waterfall completes.
/// </summary>
internal sealed class ImageMatcher {
    // Converges() requires at least this many of the 3 independent evidence types (numeric token,
    // string token, CLIP classification label) to agree before ApplyConvergenceBonus applies the
    // convergence-confidence bonus.
    private const int MinAgreeingEvidenceTypes = 2;

    private readonly MatchingConfig matchingConfig;
    private readonly NumericMatcher numericMatcher;
    private readonly StringMatcher stringMatcher;
    private readonly ClipLabelEnricher clipLabelEnricher;
    private readonly SemanticMatcher semanticMatcher;
    private readonly FilenameToCellMatcher filenameToCellMatcher;
    private readonly SiblingPropagator siblingPropagator;
    private readonly FolderNameEnricher folderNameEnricher;

    private ImageMatcher(MatchingConfig matchingConfig, TranslationConfig translationConfig, string familyIdColumnName) {
        this.matchingConfig = matchingConfig;
        this.numericMatcher = new NumericMatcher(familyIdColumnName, matchingConfig.Match.NumericMatcher);
        this.stringMatcher = new StringMatcher(translationConfig, matchingConfig.Match.StringMatcher);
        this.clipLabelEnricher = new ClipLabelEnricher();
        this.filenameToCellMatcher = new FilenameToCellMatcher();
        this.siblingPropagator = new SiblingPropagator(matchingConfig.Match.SiblingPropagator);
        this.folderNameEnricher = new FolderNameEnricher(matchingConfig.Match.FolderNameEnricher);
        this.semanticMatcher = new SemanticMatcher(
            this.numericMatcher,
            this.stringMatcher,
            this.clipLabelEnricher,
            matchingConfig.Match.Shared.SemanticThreshold,
            matchingConfig.Match.Shared.SemanticWeight);
    }

    /// <summary>
    /// Entry point called by the Matching service.
    /// Loads configs, runs the waterfall, and writes MatchEvidence to every lambda record.
    /// </summary>
    /// <param name="records">LAMBDA records to match against the family catalogue.</param>
    /// <param name="families">Family records resolved from the Internal Excel Model.</param>
    /// <returns>Number of records KO'd because no FamilyID match was found.</returns>
    internal static int Run(List<ImageRecord_LAMBDA> records, IReadOnlyList<FamilyIDRecord> families) {
        MatchingConfig matchingConfig = MatchingConfig.Load(ConfigLoader.RequireFile("MatchingConfig.json"));
        TranslationConfig translationConfig = TranslationConfig.Load(ConfigLoader.RequireFile("TranslationDictionary.json"));
        ExcelConfig excelConfig = ExcelConfig.Load(ConfigLoader.RequireFile("ExcelConfig.json"));
        PrismConfiguration prismConfig = PrismConfiguration.LoadPrismConfig(ConfigLoader.RequireFile(PrismConfiguration.FileName));

        ImageMatcher matcher = new(matchingConfig, translationConfig, excelConfig.RecordPrimaryKey);
        return matcher.RunWaterfall(records, families, prismConfig.Weight_MatchingSignalsConverging);
    }

    //  Waterfall 

    private int RunWaterfall(
        List<ImageRecord_LAMBDA> allRecords,
        IReadOnlyList<FamilyIDRecord> families,
        double convergenceWeight) {
        IReadOnlyList<MatchingRule> numericRules = this.matchingConfig.NumericRules;
        IReadOnlyList<MatchingRule> labelRules = this.matchingConfig.LabelRules;

        // Keyed by InitialFullName; holds tied candidates passed over in Brackets 1–2.
        Dictionary<string, List<CandidateSummary>> rejectedNearTies = new(StringComparer.OrdinalIgnoreCase);

        // Keyed by InitialFullName; accumulates every FamilyID an image was a candidate for across all brackets.
        Dictionary<string, HashSet<string>> crossBracketCandidates = new(StringComparer.OrdinalIgnoreCase);

        // Give meaningless filenames a matchable name from their folder before any bracket runs.
        if (this.matchingConfig.Match.Shared.EnableFolderNameEnrichment)
            this.folderNameEnricher.Enrich(allRecords, families);

        List<ImageRecord_LAMBDA> unmatched = allRecords.Where(r => !r.IsKo).ToList();

        // Bracket 1: single numeric token, TCD = 0
        unmatched = this.RunBracket1(unmatched, families, numericRules, rejectedNearTies, crossBracketCandidates);

        // Bracket 2: multi-token numeric concatenation, TCD ≤ maxDistance
        unmatched = this.RunBracket2(unmatched, families, numericRules, rejectedNearTies, crossBracketCandidates);

        // Bracket 2-Intersect: per-token candidate sets intersect to exactly one FamilyID
        unmatched = this.RunBracket2Intersect(unmatched, families, numericRules, rejectedNearTies, crossBracketCandidates);

        // Bracket 3: string tokens, exactly-1-FamilyID
        unmatched = this.RunBracket3(unmatched, allRecords, families, rejectedNearTies, crossBracketCandidates);

        // Bracket 4: semantic combined (CLIP + numeric + string) for 0-image families.
        // Skip entirely when no image in the batch carries CLIP classification signal at all.
        // Safe because MatchingConfig.json always defines ProductType + ProductColor ClipLabelEnricher
        // rules (verified in jb/src/core/config/MatchingConfig.json) — ClipLabelEnricher.BuildEvidence
        // returns [] whenever Influential.Length == 0, so FilterByClipProductType/Color already reject
        // every candidate for an untagged record today. If those two rules are ever both removed from
        // MatchingConfig.json, re-verify this gate's safety.
        bool hasClassificationSignal = allRecords.Any(r => r.Tags.Influential.Length > 0);
        unmatched = hasClassificationSignal ? this.RunBracket4(unmatched, allRecords, families, numericRules, labelRules, rejectedNearTies, crossBracketCandidates) : unmatched;

        // Bracket 4 continued — filename named verbatim in an Excel cell (exact, unique). Not a
        // bracket of its own: same "0-image families" remit as Bracket 4, just a different signal.
        unmatched = this.RunBracket5FilenameToCell(unmatched, families, rejectedNearTies, crossBracketCandidates);

        // Bracket 4 continued — substring rescue: unique family whose digit target contains a long
        // filename token.
        unmatched = this.RunSubstringRescue(unmatched, families, numericRules, rejectedNearTies, crossBracketCandidates);

        // Bracket 4 continued — sibling propagation: inherit the FamilyID of the unique matched
        // sibling image.
        if (this.matchingConfig.Match.Shared.EnableSiblingPropagation)
            unmatched = this.siblingPropagator.Run(unmatched, allRecords);

        // Add CLIP label evidence to already-matched records (no new assignments)
        this.AddClipLabelEvidence(allRecords, families, labelRules);

        // Cleanup (not a bracket): KO any image still without a FamilyID assignment
        int koAdded = KoUnmatched(unmatched, crossBracketCandidates, families);

        // Finalize (not a bracket): apply the convergence bonus. Single-pass waterfall means no
        // structural ties survive to this point.
        ApplyConvergenceBonus(allRecords, convergenceWeight);

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
        Dictionary<string, HashSet<string>> crossBracketCandidates) {
        List<ImageRecord_LAMBDA> stillUnmatched = [];
        double numericWeight = numericRules.Count > 0 ? numericRules[0].Weight : 1.0;

        foreach (ImageRecord_LAMBDA record in candidates) {
            string key = record.InitialFullName ?? string.Empty;
            (MatchEvidence? evidence, List<CandidateSummary> tiedCandidates) =
                this.numericMatcher.TryMatchBracket1WithTies(record, families, numericRules);

            if (tiedCandidates.Count > 1) {
                rejectedNearTies[key] = tiedCandidates;
                AccumulateCandidates(crossBracketCandidates, key, tiedCandidates);
            }

            if (evidence is not null) {
                record.MatchEvidence = evidence with {
                    ThresholdStatus = evidence.FinalScore >= this.matchingConfig.Match.Shared.SemanticThreshold,
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
        Dictionary<string, List<CandidateSummary>> rejectedNearTies, string key) =>
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
        Dictionary<string, HashSet<string>> crossBracketCandidates) {
        List<ImageRecord_LAMBDA> stillUnmatched = [];
        double numericWeight = numericRules.Count > 0 ? numericRules[0].Weight : 1.0;

        foreach (ImageRecord_LAMBDA record in candidates) {
            string key = record.InitialFullName ?? string.Empty;
            (MatchEvidence? evidence, List<CandidateSummary> tiedCandidates) =
                this.numericMatcher.TryMatchBracket2WithTies(record, families, numericRules);

            if (tiedCandidates.Count > 1) {
                rejectedNearTies[key] = tiedCandidates;
                AccumulateCandidates(crossBracketCandidates, key, tiedCandidates);
            }

            if (evidence is not null) {
                record.MatchEvidence = evidence with {
                    ThresholdStatus = evidence.FinalScore >= this.matchingConfig.Match.Shared.SemanticThreshold,
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
        Dictionary<string, List<CandidateSummary>> rejectedNearTies,
        Dictionary<string, HashSet<string>> crossBracketCandidates) {
        List<ImageRecord_LAMBDA> stillUnmatched = [];
        double numericWeight = numericRules.Count > 0 ? numericRules[0].Weight : 1.0;

        foreach (ImageRecord_LAMBDA record in candidates) {
            string key = record.InitialFullName ?? string.Empty;
            (MatchEvidence? evidence, List<CandidateSummary> tiedCandidates) =
                this.numericMatcher.TryMatchByTokenIntersection(record, families, numericRules);

            if (tiedCandidates.Count > 1)
                AccumulateCandidates(crossBracketCandidates, key, tiedCandidates);

            if (evidence is not null) {
                record.MatchEvidence = evidence with {
                    ThresholdStatus = evidence.FinalScore >= this.matchingConfig.Match.Shared.SemanticThreshold,
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
        Dictionary<string, HashSet<string>> crossBracketCandidates) {
        List<ImageRecord_LAMBDA> stillUnmatched = [];

        // (FamilyId, Phenotype) -> count of already-matched, non-KO records with that phenotype in
        // that family. Seeded once from allRecords, then incremented as this bracket accepts matches,
        // so each candidate's duplicate check is an O(1) lookup instead of an O(allRecords) scan.
        Dictionary<(string FamilyId, string Phenotype), int> phenotypeCounts = BuildPhenotypeCounts(allRecords);

        foreach (ImageRecord_LAMBDA record in candidates) {
            string key = record.InitialFullName ?? string.Empty;
            MatchEvidence? evidence = this.stringMatcher.TryMatch(record, families);

            if (evidence is not null && HasDuplicatePhenotypeInFamily(
                    evidence.FinalFamilyId, record.SelectedPhenotype, phenotypeCounts)) {
                if (evidence.FinalFamilyId is not null)
                    AccumulateCandidates(crossBracketCandidates, key, evidence.FinalFamilyId);
                evidence = null;
            }

            if (evidence is not null) {
                record.MatchEvidence = evidence with {
                    ThresholdStatus = evidence.FinalScore >= this.matchingConfig.Match.Shared.SemanticThreshold,
                    RejectedNearTieEvidence = GetRejectedTies(rejectedNearTies, key),
                    MatcherWeights = [new MatcherContribution("StringMatcher.Bracket3", 1.0, evidence.FinalScore)]
                };

                if (evidence.FinalFamilyId is not null && record.SelectedPhenotype is not null)
                    IncrementPhenotypeCount(phenotypeCounts, evidence.FinalFamilyId, record.SelectedPhenotype);
            }
            else {
                stillUnmatched.Add(record);
            }
        }

        return stillUnmatched;
    }

    /// <summary>
    /// Builds the initial (FamilyId, Phenotype) → count map from every already-matched, non-KO record
    /// with a non-null <see cref="ImageRecord_LAMBDA.SelectedPhenotype"/>. Keys are upper-invariant to
    /// preserve the original case-insensitive comparison semantics.
    /// </summary>
    private static Dictionary<(string FamilyId, string Phenotype), int> BuildPhenotypeCounts(List<ImageRecord_LAMBDA> allRecords) {
        Dictionary<(string, string), int> counts = [];
        foreach (ImageRecord_LAMBDA r in allRecords) {
            if (r.IsKo || r.MatchEvidence?.FinalFamilyId is not string familyId || r.SelectedPhenotype is not string phenotype)
                continue;
            IncrementPhenotypeCount(counts, familyId, phenotype);
        }
        return counts;
    }

    private static void IncrementPhenotypeCount(Dictionary<(string FamilyId, string Phenotype), int> counts, string familyId, string phenotype) {
        (string, string) countKey = (familyId.ToUpperInvariant(), phenotype.ToUpperInvariant());
        counts[countKey] = counts.GetValueOrDefault(countKey) + 1;
    }

    /// <summary>
    /// Returns true when <paramref name="familyId"/> already has a matched record with the same
    /// non-null <paramref name="phenotype"/>, per <paramref name="phenotypeCounts"/>.
    /// </summary>
    private static bool HasDuplicatePhenotypeInFamily(
        string? familyId,
        string? phenotype,
        Dictionary<(string FamilyId, string Phenotype), int> phenotypeCounts) {
        if (familyId is null || phenotype is null)
            return false;

        return phenotypeCounts.GetValueOrDefault((familyId.ToUpperInvariant(), phenotype.ToUpperInvariant())) > 0;
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
        Dictionary<string, List<CandidateSummary>> rejectedNearTies,
        Dictionary<string, HashSet<string>> crossBracketCandidates) {
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
            (MatchEvidence? evidence, List<CandidateSummary> tiedCandidates) = this.semanticMatcher.TryMatch(
                record, unassignedFamilies, numericRules, labelRules);

            if (tiedCandidates.Count > 1)
                AccumulateCandidates(crossBracketCandidates, key, tiedCandidates);

            if (evidence is not null) {
                record.MatchEvidence = evidence with {
                    ThresholdStatus = evidence.FinalScore >= this.matchingConfig.Match.Shared.SemanticThreshold,
                    RejectedNearTieEvidence = GetRejectedTies(rejectedNearTies, key),
                    MatcherWeights =
                    [
                        new MatcherContribution("SemanticMatcher.Bracket4", this.matchingConfig.Match.Shared.SemanticWeight, evidence.FinalScore)
                    ]
                };
            }
            else {
                stillUnmatched.Add(record);
            }
        }

        return stillUnmatched;
    }

    //  Bracket 4 continued — filename present in an Excel cell

    /// <summary>
    /// Runs FilenameToCellMatcher: assigns an image to the unique FamilyID whose Excel row names
    /// that exact image file in any cell. Matches against all families (a family may already hold
    /// other images). Returns images still unmatched after this bracket.
    /// </summary>
    private List<ImageRecord_LAMBDA> RunBracket5FilenameToCell(
        List<ImageRecord_LAMBDA> candidates,
        IReadOnlyList<FamilyIDRecord> families,
        Dictionary<string, List<CandidateSummary>> rejectedNearTies,
        Dictionary<string, HashSet<string>> crossBracketCandidates) {
        List<ImageRecord_LAMBDA> stillUnmatched = [];

        foreach (ImageRecord_LAMBDA record in candidates) {
            string key = record.InitialFullName ?? string.Empty;
            (MatchEvidence? evidence, List<CandidateSummary> tiedCandidates) = this.filenameToCellMatcher.TryMatch(record, families);

            if (tiedCandidates.Count > 1)
                AccumulateCandidates(crossBracketCandidates, key, tiedCandidates);

            if (evidence is not null) {
                record.MatchEvidence = evidence with {
                    ThresholdStatus = evidence.FinalScore >= this.matchingConfig.Match.Shared.SemanticThreshold,
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

    //  Bracket 4 continued — substring rescue

    /// <summary>
    /// Runs NumericMatcher substring rescue: accepts the unique family one of whose digit targets
    /// contains a long filename token. Returns images still unmatched after this pass.
    /// </summary>
    private List<ImageRecord_LAMBDA> RunSubstringRescue(
        List<ImageRecord_LAMBDA> candidates,
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules,
        Dictionary<string, List<CandidateSummary>> rejectedNearTies,
        Dictionary<string, HashSet<string>> crossBracketCandidates) {
        List<ImageRecord_LAMBDA> stillUnmatched = [];

        foreach (ImageRecord_LAMBDA record in candidates) {
            string key = record.InitialFullName ?? string.Empty;
            (MatchEvidence? evidence, List<CandidateSummary> tiedCandidates) =
                this.numericMatcher.TryMatchBySubstringRescue(record, families, numericRules);

            if (tiedCandidates.Count > 1)
                AccumulateCandidates(crossBracketCandidates, key, tiedCandidates);

            if (evidence is not null) {
                record.MatchEvidence = evidence with {
                    ThresholdStatus = evidence.FinalScore >= this.matchingConfig.Match.Shared.SemanticThreshold,
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
        IReadOnlyList<MatchingRule> labelRules) {
        if (labelRules.Count == 0)
            return;

        foreach (ImageRecord_LAMBDA record in allRecords) {
            if (record.IsKo || record.MatchEvidence?.FinalFamilyId is null)
                continue;

            IReadOnlyList<LabelEvidenceItem> clipEvidence =
                this.clipLabelEnricher.BuildEvidence(record, families, labelRules);

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

    //  Cleanup

    /// <summary>
    /// KOs any image that was not matched by Brackets 1–4 (including Bracket 4's continuation passes).
    /// Images that were candidates for 2+ FamilyIDs receive <c>MATCHES_MULTIPLE_FAMILYIDS</c>;
    /// images whose stem carries a well-formed FamilyID that the catalogue simply does not contain
    /// receive <c>NOT_IN_CATALOG</c>; images with no signal at all receive <c>MATCH_NOT_FOUND</c>.
    /// </summary>
    /// <returns>Number of records KO'd.</returns>
    private static int KoUnmatched(List<ImageRecord_LAMBDA> unmatched, IReadOnlyDictionary<string, HashSet<string>> crossBracketCandidates, IReadOnlyList<FamilyIDRecord> families) {
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
    private static bool StemCarriesUnknownFamilyId(string imageId, HashSet<string> knownFamilyIds) {
        foreach (System.Text.RegularExpressions.Match run in System.Text.RegularExpressions.Regex.Matches(imageId, @"\d+")) {
            if (run.Value.Length == 8 && !knownFamilyIds.Contains(run.Value))
                return true;
        }

        return false;
    }

    /// <summary>Adds all FamilyIds from <paramref name="candidates"/> to the cross-bracket accumulator for <paramref name="key"/>.</summary>
    private static void AccumulateCandidates(Dictionary<string, HashSet<string>> accumulator, string key, IEnumerable<CandidateSummary> candidates) {
        if (!accumulator.TryGetValue(key, out HashSet<string>? set))
            accumulator[key] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CandidateSummary c in candidates) set.Add(c.FamilyId);
    }

    /// <summary>Adds a single <paramref name="familyId"/> to the cross-bracket accumulator for <paramref name="key"/>.</summary>
    private static void AccumulateCandidates(Dictionary<string, HashSet<string>> accumulator, string key, string familyId) {
        if (!accumulator.TryGetValue(key, out HashSet<string>? set))
            accumulator[key] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        set.Add(familyId);
    }

    //  Finalize

    /// <summary>
    /// Applies the convergence bonus to matched records whose evidence spans at least two distinct
    /// signal types (NumericToken, StringToken, ClassificationLabel).
    /// T-700 reads record.MatchEvidence.FinalFamilyId to build det-order clusters.
    /// </summary>
    private static void ApplyConvergenceBonus(List<ImageRecord_LAMBDA> allRecords, double convergenceWeight) {
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
    private static bool Converges(MatchEvidence me) {
        int signalCount = 0;
        if (me.NumericTokenEvidence.Count > 0) signalCount++;
        if (me.StringTokenEvidence.Count > 0) signalCount++;
        if (me.ClassificationLabelEvidence.Count > 0) signalCount++;
        return signalCount >= MinAgreeingEvidenceTypes;
    }

}