using System.Text.RegularExpressions;

/// <summary>
/// Matches images to FamilyIDs using numeric token extraction and TCD scoring.
/// Bracket 1 (single-token, TCD = 0): one filename digit sequence exactly equals a family numeric value.
/// Bracket 2 (multi-token, TCD ≤ maxDistance): consecutive digit sequences concatenate to a family numeric value.
/// </summary>
internal sealed class NumericMatcher
{
    private static readonly Regex DigitSequencePattern = new(@"\d+", RegexOptions.Compiled);
    private static readonly Regex DigitsOnlyPattern    = new(@"\d",   RegexOptions.Compiled);

    // ─── Bracket 1 ────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts Bracket 1 matching: a single numeric token in the filename exactly equals a family numeric value.
    /// </summary>
    /// <returns>Accepted MatchEvidence when exactly one FamilyID matches; null otherwise.</returns>
    internal MatchEvidence? TryMatchBracket1(
        ImageRecord_LAMBDA record,
        IReadOnlyList<FamilyRecord> families,
        IReadOnlyList<MatchingRule> numericRules)
    {
        string filename      = record.InitialFullName ?? string.Empty;
        string[]   tokens    = ExtractNumericTokens(filename);
        string     sourceFilename = filename;
        string     imageId   = Path.GetFileNameWithoutExtension(filename);

        List<CandidateSummary> allMatches = [];

        foreach (string token in tokens)
        {
            foreach (FamilyRecord family in families)
            {
                foreach (MatchingRule rule in numericRules)
                {
                    string? target = GetNumericTarget(family, rule.ExcelField);
                    if (target is null || token != target)
                        continue;

                    allMatches.Add(new CandidateSummary(family.FamilyID, 1.0, "NumericMatcher.Bracket1"));
                }
            }
        }

        // Deduplicate by FamilyID
        List<CandidateSummary> uniqueMatches = allMatches
            .GroupBy(c => c.FamilyId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (uniqueMatches.Count == 0)
            return null;

        if (uniqueMatches.Count > 1)
        {
            // Tie within Bracket 1 — pass through to Bracket 2/3
            return null;
        }

        CandidateSummary winner = uniqueMatches[0];
        return new MatchEvidence
        {
            ImageId              = imageId,
            SourceFilename       = sourceFilename,
            FinalFamilyId        = winner.FamilyId,
            FinalScore           = 1.0,
            IsKo                 = false,
            AcceptedMatcherName  = winner.MatcherName,
            TopCandidates        = uniqueMatches,
            NumericTokenEvidence =
            [
                new TokenEvidenceItem(
                    FindMatchingToken(tokens, winner.FamilyId, families, numericRules),
                    GetNumericTarget(FindFamily(families, winner.FamilyId)!, numericRules[0].ExcelField) ?? winner.FamilyId,
                    FindMatchedRule(winner.FamilyId, families, numericRules)?.ExcelField ?? string.Empty,
                    winner.FamilyId,
                    1.0)
            ],
            ImageNgpSummary  = BuildNgpSummary(record),
            SafeExplanation  = $"Bracket1: single numeric token exactly matched family {winner.FamilyId}."
        };
    }

    // ─── Bracket 2 ────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts Bracket 2 matching: consecutive numeric tokens concatenated (in filename order) match a family
    /// numeric value with TCD ≤ maxDistance.
    /// </summary>
    /// <returns>Accepted MatchEvidence for the best match when exactly one FamilyID qualifies; null otherwise.</returns>
    internal MatchEvidence? TryMatchBracket2(
        ImageRecord_LAMBDA record,
        IReadOnlyList<FamilyRecord> families,
        IReadOnlyList<MatchingRule> numericRules)
    {
        string   filename      = record.InitialFullName ?? string.Empty;
        string[] tokens        = ExtractNumericTokens(filename);
        string   sourceFilename = filename;
        string   imageId       = Path.GetFileNameWithoutExtension(filename);

        if (tokens.Length < 2)
            return null;

        // Collect best TCD per FamilyID
        Dictionary<string, (double Tcd, string[] Subset, string PropertyName)> bestPerFamily =
            new(StringComparer.OrdinalIgnoreCase);

        for (int start = 0; start < tokens.Length; start++)
        {
            for (int length = 2; length <= tokens.Length - start; length++)
            {
                string[] subset       = tokens.Skip(start).Take(length).ToArray();
                string   concatenated = string.Concat(subset);

                foreach (FamilyRecord family in families)
                {
                    foreach (MatchingRule rule in numericRules)
                    {
                        string? target = GetNumericTarget(family, rule.ExcelField);
                        if (target is null || concatenated != target)
                            continue;

                        double tcd = TokenizedConcatenationDistance.Compute(subset, target);
                        if (double.IsPositiveInfinity(tcd) || tcd > rule.MaxDistance)
                            continue;

                        if (!bestPerFamily.TryGetValue(family.FamilyID, out var existing) || tcd < existing.Tcd)
                            bestPerFamily[family.FamilyID] = (tcd, subset, rule.ExcelField);
                    }
                }
            }
        }

        if (bestPerFamily.Count == 0)
            return null;

        if (bestPerFamily.Count > 1)
        {
            // Tie — pass through to Bracket 3
            return null;
        }

        KeyValuePair<string, (double Tcd, string[] Subset, string PropertyName)> match = bestPerFamily.First();
        string matcherName = "NumericMatcher.Bracket2";
        double confidence  = TokenizedConcatenationDistance.ConvertDistanceToConfidence(match.Value.Tcd) / 100.0;

        return new MatchEvidence
        {
            ImageId              = imageId,
            SourceFilename       = sourceFilename,
            FinalFamilyId        = match.Key,
            FinalScore           = confidence,
            IsKo                 = false,
            AcceptedMatcherName  = matcherName,
            TopCandidates        = [new CandidateSummary(match.Key, confidence, matcherName)],
            NumericTokenEvidence =
            [
                new TokenEvidenceItem(
                    string.Join("+", match.Value.Subset),
                    GetNumericTarget(FindFamily(families, match.Key)!, match.Value.PropertyName) ?? string.Empty,
                    match.Value.PropertyName,
                    match.Key,
                    confidence)
            ],
            ImageNgpSummary  = BuildNgpSummary(record),
            SafeExplanation  = $"Bracket2: tokens [{string.Join(", ", match.Value.Subset)}] concatenated to match family {match.Key} (TCD={match.Value.Tcd:F3})."
        };
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts all digit sequences from the filename stem, preserving left-to-right order.
    /// </summary>
    private static string[] ExtractNumericTokens(string filename)
    {
        string stem = Path.GetFileNameWithoutExtension(filename);
        return DigitSequencePattern.Matches(stem)
            .Select(m => m.Value)
            .ToArray();
    }

    /// <summary>
    /// Returns the pure-digit target value for a rule against a given FamilyRecord.
    /// FamilyID rule uses FamilyRecord.FamilyID directly; other rules use CanonicalProperties.
    /// </summary>
    private static string? GetNumericTarget(FamilyRecord family, string excelField)
    {
        string rawValue;

        if (excelField.Equals("familyID", StringComparison.OrdinalIgnoreCase) ||
            excelField.Equals("famID", StringComparison.OrdinalIgnoreCase))
        {
            rawValue = family.FamilyID;
        }
        else if (family.CanonicalProperties.TryGetValue(excelField, out string? value) &&
                 !string.IsNullOrWhiteSpace(value))
        {
            rawValue = value;
        }
        else
        {
            return null;
        }

        string digitsOnly = string.Concat(DigitsOnlyPattern.Matches(rawValue).Select(m => m.Value));
        return digitsOnly.Length > 0 ? digitsOnly : null;
    }

    private static FamilyRecord? FindFamily(IReadOnlyList<FamilyRecord> families, string familyId) =>
        families.FirstOrDefault(f => f.FamilyID.Equals(familyId, StringComparison.OrdinalIgnoreCase));

    private static string FindMatchingToken(
        string[] filenameTokens,
        string familyId,
        IReadOnlyList<FamilyRecord> families,
        IReadOnlyList<MatchingRule> numericRules)
    {
        FamilyRecord? family = FindFamily(families, familyId);
        if (family is null) return string.Empty;

        foreach (string token in filenameTokens)
        {
            foreach (MatchingRule rule in numericRules)
            {
                string? target = GetNumericTarget(family, rule.ExcelField);
                if (target == token) return token;
            }
        }

        return string.Empty;
    }

    private static MatchingRule? FindMatchedRule(
        string familyId,
        IReadOnlyList<FamilyRecord> families,
        IReadOnlyList<MatchingRule> numericRules)
    {
        FamilyRecord? family = FindFamily(families, familyId);
        if (family is null) return null;

        foreach (MatchingRule rule in numericRules)
        {
            if (GetNumericTarget(family, rule.ExcelField) is not null)
                return rule;
        }

        return null;
    }

    private static string? BuildNgpSummary(ImageRecord_LAMBDA record) =>
        record.SelectedPhenotype is null ? null : $"phenotype={record.SelectedPhenotype}";
}
