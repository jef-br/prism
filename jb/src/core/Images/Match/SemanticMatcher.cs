namespace Prism.Core;

/// <summary>
/// Bracket 4 matcher: combined CLIP + numeric + string semantic attempt for images unmatched by Brackets 1–3.
/// Candidate pool is restricted to FamilyIDs with zero images assigned in earlier brackets.
/// Accepts an assignment only when exactly one candidate survives all filters and the combined evidence
/// score meets SemanticThreshold.
/// </summary>
internal sealed class SemanticMatcher
{
    private readonly NumericMatcher    numericMatcher;
    private readonly StringMatcher     stringMatcher;
    private readonly ClipLabelEnricher clipLabelEnricher;
    private readonly double            semanticThreshold;
    private readonly double            semanticWeight;

    internal SemanticMatcher(
        NumericMatcher    numericMatcher,
        StringMatcher     stringMatcher,
        ClipLabelEnricher clipLabelEnricher,
        double            semanticThreshold,
        double            semanticWeight)
    {
        this.numericMatcher    = numericMatcher;
        this.stringMatcher     = stringMatcher;
        this.clipLabelEnricher = clipLabelEnricher;
        this.semanticThreshold = semanticThreshold;
        this.semanticWeight    = semanticWeight;
    }

    /// <summary>
    /// Attempts Bracket 4 semantic matching for a single unmatched image against a pre-filtered
    /// list of FamilyIDRecords that have received zero images in earlier brackets.
    /// </summary>
    /// <returns>Accepted MatchEvidence when exactly one candidate survives; null otherwise.</returns>
    internal MatchEvidence? TryMatch(
        ImageRecord_LAMBDA          record,
        IReadOnlyList<FamilyIDRecord> unassignedFamilies,
        IReadOnlyList<MatchingRule>   numericRules,
        IReadOnlyList<MatchingRule>   labelRules)
    {
        string filename = record.InitialFullName ?? string.Empty;
        string imageId  = Path.GetFileNameWithoutExtension(filename);

        List<FamilyIDRecord> candidates = [..unassignedFamilies];

        // Step 1: CLIP ProductType hard filter
        (candidates, bool typeFilterApplied) = FilterByClipProductType(record, candidates, labelRules);
        if (candidates.Count == 0) return null;

        // Step 2: CLIP ProductColor hard filter (conditional — only when some candidates carry color)
        (candidates, bool colorFilterApplied) = FilterByClipProductColor(record, candidates, labelRules);
        if (candidates.Count == 0) return null;

        // With per-dimension gating, "survived the CLIP filters" only means something when a filter
        // actually ran — track it so an unfiltered sole survivor is not mistaken for CLIP evidence.
        bool clipApplied = typeFilterApplied || colorFilterApplied;

        // Step 3: Numeric token candidate reduction
        bool hadMultipleBeforeNumeric = candidates.Count > 1;
        candidates = [..numericMatcher.ReduceCandidatesByNumericTokens(filename, candidates, numericRules)];
        bool numericReduced = hadMultipleBeforeNumeric && candidates.Count == 1;

        if (candidates.Count == 0) return null;

        // Step 4: String token scoring — keep only the candidate(s) with the most matching tokens.
        // indexScope is unassignedFamilies (the stable superset for this whole Bracket 4 run), so the
        // inverted token index is built/cached once per bracket run, not once per image.
        var scored = stringMatcher.ScoreCandidatesByStringTokens(filename, candidates, unassignedFamilies);

        FamilyIDRecord winner;
        List<TokenEvidenceItem> stringEvidence;
        int totalImageTokens;

        if (scored.Count > 0)
        {
            int topCount = scored[0].MatchCount;
            var topCandidates = scored.Where(s => s.MatchCount == topCount).ToList();

            if (topCandidates.Count > 1) return null; // string tie → no assignment

            (winner, _, stringEvidence) = topCandidates[0];
            totalImageTokens = stringEvidence.Select(e => e.FilenameToken).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                               + scored.Count; // rough total; precision here is cosmetic
        }
        else if (candidates.Count == 1)
        {
            // A sole survivor is only acceptable when some signal actually narrowed the pool —
            // an image with no CLIP tags and no numeric reduction has no evidence tying it to the
            // last unassigned family, however alone that family is.
            if (!clipApplied && !numericReduced) return null;

            winner        = candidates[0];
            stringEvidence = [];
            totalImageTokens = 0;
        }
        else
        {
            return null; // multiple candidates, no string signal to break the tie
        }

        // Step 5: Compute combined score and check threshold
        double clipSignal    = clipApplied ? 1.0 : 0.5; // 0.5 = no CLIP filter ran (neutral)
        double numericSignal = numericReduced ? 1.0 : 0.5; // 0.5 = no numeric reduction (neutral)
        double stringSignal  = totalImageTokens > 0
            ? Math.Min(1.0, (double)stringEvidence.Count / totalImageTokens)
            : (stringEvidence.Count > 0 ? 0.5 : 0.0);

        double combinedScore = (clipSignal + numericSignal + stringSignal) / 3.0;

        if (combinedScore < semanticThreshold) return null;

        double finalScore = combinedScore * semanticWeight;

        IReadOnlyList<LabelEvidenceItem> clipEvidence =
            clipLabelEnricher.BuildEvidence(record, [winner], labelRules);

        return new MatchEvidence
        {
            ImageId                    = imageId,
            SourceFilename             = filename,
            FinalFamilyId              = winner.FamilyID,
            FinalScore                 = finalScore,
            IsKo                       = false,
            AcceptedMatcherName        = "SemanticMatcher.Bracket4",
            TopCandidates              = [new CandidateSummary(winner.FamilyID, finalScore, "SemanticMatcher.Bracket4")],
            StringTokenEvidence        = stringEvidence,
            ClassificationLabelEvidence = clipEvidence,
            ImageNgpSummary            = record.SelectedPhenotype is null ? null : $"phenotype={record.SelectedPhenotype}",
            SafeExplanation            = $"Bracket4: semantic signals (CLIP+numeric+string) narrowed to family {winner.FamilyID} (score={combinedScore:F3})."
        };
    }

    //  CLIP filters 

    /// <summary>
    /// Removes candidates where no influential CLIP ProductType tag matches the family's ProductType column.
    /// Applies only when a label rule targets ProductType, at least one candidate carries a ProductType
    /// column, AND the image actually has a tag the rule may consider — an untagged dimension passes
    /// candidates through unchanged instead of erasing them. Applied reports whether filtering ran.
    /// </summary>
    private (List<FamilyIDRecord> Candidates, bool Applied) FilterByClipProductType(
        ImageRecord_LAMBDA          record,
        List<FamilyIDRecord>         candidates,
        IReadOnlyList<MatchingRule>   labelRules)
    {
        MatchingRule? productTypeRule = labelRules.FirstOrDefault(
            r => r.ExcelField.Equals("ProductType", StringComparison.OrdinalIgnoreCase));

        if (productTypeRule is null) return (candidates, false);

        // Only apply the filter when at least one candidate carries a ProductType column
        bool anyHasProductType = candidates.Any(
            f => f.NormalizedTokens.ContainsKey(productTypeRule.ExcelField));

        if (!anyHasProductType) return (candidates, false);

        // No product-type signal on this image — nothing to contradict, keep all candidates.
        if (!ClipLabelEnricher.HasTagForRule(record, productTypeRule)) return (candidates, false);

        List<FamilyIDRecord> filtered = candidates
            .Where(f => clipLabelEnricher.BuildEvidence(record, [f], [productTypeRule]).Count > 0)
            .ToList();
        return (filtered, true);
    }

    /// <summary>
    /// Removes candidates where no influential CLIP color tag matches the family's ProductColor column.
    /// Only applied when at least one remaining candidate has a non-empty ProductColor value and the
    /// image carries a color tag the rule may consider. Applied reports whether filtering ran.
    /// </summary>
    private (List<FamilyIDRecord> Candidates, bool Applied) FilterByClipProductColor(
        ImageRecord_LAMBDA          record,
        List<FamilyIDRecord>         candidates,
        IReadOnlyList<MatchingRule>   labelRules)
    {
        MatchingRule? colorRule = labelRules.FirstOrDefault(
            r => r.ExcelField.Equals("ProductColor", StringComparison.OrdinalIgnoreCase));

        if (colorRule is null) return (candidates, false);

        bool anyHasColor = candidates.Any(
            f => f.NormalizedTokens.TryGetValue(colorRule.ExcelField, out var tokens) && tokens.Count > 0);

        if (!anyHasColor) return (candidates, false);

        // No color signal on this image — nothing to contradict, keep all candidates.
        if (!ClipLabelEnricher.HasTagForRule(record, colorRule)) return (candidates, false);

        List<FamilyIDRecord> filtered = candidates
            .Where(f => {
                bool hasColorColumn = f.NormalizedTokens.TryGetValue(colorRule.ExcelField, out var tokens) && tokens.Count > 0;
                if (!hasColorColumn) return true; // no color to contradict → keep
                return clipLabelEnricher.BuildEvidence(record, [f], [colorRule]).Count > 0;
            })
            .ToList();
        return (filtered, true);
    }
}
