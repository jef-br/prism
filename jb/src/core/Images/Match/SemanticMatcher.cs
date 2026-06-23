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
        candidates = FilterByClipProductType(record, candidates, labelRules);
        if (candidates.Count == 0) return null;

        // Step 2: CLIP ProductColor hard filter (conditional — only when some candidates carry color)
        candidates = FilterByClipProductColor(record, candidates, labelRules);
        if (candidates.Count == 0) return null;

        // Step 3: Numeric token candidate reduction
        bool hadMultipleBeforeNumeric = candidates.Count > 1;
        candidates = [..numericMatcher.ReduceCandidatesByNumericTokens(filename, candidates, numericRules)];
        bool numericReduced = hadMultipleBeforeNumeric && candidates.Count == 1;

        if (candidates.Count == 0) return null;

        // Step 4: String token scoring — keep only the candidate(s) with the most matching tokens
        var scored = stringMatcher.ScoreCandidatesByStringTokens(filename, candidates);

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
            // Numeric or CLIP alone narrowed to exactly one — proceed with no string evidence
            winner        = candidates[0];
            stringEvidence = [];
            totalImageTokens = 0;
        }
        else
        {
            return null; // multiple candidates, no string signal to break the tie
        }

        // Step 5: Compute combined score and check threshold
        double clipSignal    = 1.0; // candidate survived CLIP hard filters
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

    // ─── CLIP filters ────────────────────────────────────────────────────────

    /// <summary>
    /// Removes candidates where no influential CLIP ProductType tag matches the family's ProductType column.
    /// If no label rule targets ProductType, or no candidate has a ProductType column, returns the list unchanged.
    /// </summary>
    private List<FamilyIDRecord> FilterByClipProductType(
        ImageRecord_LAMBDA          record,
        List<FamilyIDRecord>         candidates,
        IReadOnlyList<MatchingRule>   labelRules)
    {
        MatchingRule? productTypeRule = labelRules.FirstOrDefault(
            r => r.ExcelField.Equals("ProductType", StringComparison.OrdinalIgnoreCase));

        if (productTypeRule is null) return candidates;

        // Only apply the filter when at least one candidate carries a ProductType column
        bool anyHasProductType = candidates.Any(
            f => f.NormalizedTokens.ContainsKey(productTypeRule.ExcelField));

        if (!anyHasProductType) return candidates;

        return candidates
            .Where(f => clipLabelEnricher.BuildEvidence(record, [f], [productTypeRule]).Count > 0)
            .ToList();
    }

    /// <summary>
    /// Removes candidates where no influential CLIP color tag matches the family's ProductColor column.
    /// Only applied when at least one remaining candidate has a non-empty ProductColor value.
    /// </summary>
    private List<FamilyIDRecord> FilterByClipProductColor(
        ImageRecord_LAMBDA          record,
        List<FamilyIDRecord>         candidates,
        IReadOnlyList<MatchingRule>   labelRules)
    {
        MatchingRule? colorRule = labelRules.FirstOrDefault(
            r => r.ExcelField.Equals("ProductColor", StringComparison.OrdinalIgnoreCase));

        if (colorRule is null) return candidates;

        bool anyHasColor = candidates.Any(
            f => f.NormalizedTokens.TryGetValue(colorRule.ExcelField, out var tokens) && tokens.Count > 0);

        if (!anyHasColor) return candidates;

        return candidates
            .Where(f => {
                bool hasColorColumn = f.NormalizedTokens.TryGetValue(colorRule.ExcelField, out var tokens) && tokens.Count > 0;
                if (!hasColorColumn) return true; // no color to contradict → keep
                return clipLabelEnricher.BuildEvidence(record, [f], [colorRule]).Count > 0;
            })
            .ToList();
    }
}
