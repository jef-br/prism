using System.Text.RegularExpressions;

namespace Prism.Core;

/// <summary>
/// Matches images to FamilyIDs using numeric token extraction and TCD scoring.
/// Bracket 1 (single-token, TCD = 0): one filename digit sequence exactly equals a family numeric value.
/// Bracket 2 (multi-token, TCD ≤ maxDistance): consecutive digit sequences concatenate to a family numeric value.
/// </summary>
internal sealed class NumericMatcher
{
    private static readonly Regex DigitSequencePattern = new(@"\d+", RegexOptions.Compiled);
    private static readonly Regex DigitsOnlyPattern    = new(@"\d",   RegexOptions.Compiled);

    private readonly string familyIdColumnName;

    /// <summary>
    /// Creates a numeric matcher.
    /// </summary>
    /// <param name="familyIdColumnName">
    /// The rule ExcelField that denotes the FamilyID (ExcelConfig.RecordPrimaryKey). The FamilyID rule
    /// resolves against the intrinsic <see cref="FamilyIDRecord.FamilyID"/> — the 8-digit PRISM identifier
    /// that is also the image filename stem — rather than an Excel column lookup.
    /// </param>
    internal NumericMatcher(string familyIdColumnName)
    {
        this.familyIdColumnName = familyIdColumnName;
    }

    //  Bracket 1

    /// <summary>
    /// Attempts Bracket 1 matching: a single numeric token in the filename exactly equals a family numeric value.
    /// </summary>
    /// <returns>Accepted MatchEvidence when exactly one FamilyID matches; null otherwise.</returns>
    internal MatchEvidence? TryMatchBracket1(
        ImageRecord_LAMBDA record,
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules) =>
        TryMatchBracket1WithTies(record, families, numericRules).Evidence;

    /// <summary>
    /// Attempts Bracket 1 matching, returning both the evidence and any tied candidates for rejection tracking.
    /// </summary>
    /// <returns>
    /// Evidence when exactly one FamilyID matches (null on tie or no match); all unique candidates when a tie occurs.
    /// </returns>
    internal (MatchEvidence? Evidence, List<CandidateSummary> TiedCandidates) TryMatchBracket1WithTies(
        ImageRecord_LAMBDA record,
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules)
    {
        string filename      = record.InitialFullName ?? string.Empty;
        string[]   tokens    = GetNumericTokensFromFilename(filename);
        string     sourceFilename = filename;
        string     imageId   = Path.GetFileNameWithoutExtension(filename);

        List<CandidateSummary> allMatches = [];

        foreach (string token in tokens)
        {
            foreach (FamilyIDRecord family in families)
            {
                foreach (MatchingRule rule in numericRules)
                {
                    string? target = GetFamilyDigitsForField(family, rule.ExcelField);
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

        if (uniqueMatches.Count != 1)
            return (null, uniqueMatches); // 0 = no match; 2+ = tie (caller records ties)

        CandidateSummary winner = uniqueMatches[0];
        MatchEvidence evidence = new MatchEvidence
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
                    GetFamilyDigitsForField(FindFamily(families, winner.FamilyId)!, numericRules[0].ExcelField) ?? winner.FamilyId,
                    FindMatchedRule(winner.FamilyId, families, numericRules)?.ExcelField ?? string.Empty,
                    winner.FamilyId,
                    1.0)
            ],
            ImageNgpSummary  = BuildNgpSummary(record),
            SafeExplanation  = $"Bracket1: single numeric token exactly matched family {winner.FamilyId}."
        };
        return (evidence, []);
    }

    //  Bracket 2

    /// <summary>
    /// Attempts Bracket 2 matching: consecutive numeric tokens concatenated (in filename order) match a family
    /// numeric value with TCD ≤ maxDistance.
    /// </summary>
    /// <returns>Accepted MatchEvidence for the best match when exactly one FamilyID qualifies; null otherwise.</returns>
    internal MatchEvidence? TryMatchBracket2(
        ImageRecord_LAMBDA record,
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules) =>
        TryMatchBracket2WithTies(record, families, numericRules).Evidence;

    /// <summary>
    /// Attempts Bracket 2 matching, returning both the evidence and any tied candidates for rejection tracking.
    /// </summary>
    /// <returns>
    /// Evidence when exactly one FamilyID qualifies (null on tie or no match); all tied candidates when a tie occurs.
    /// </returns>
    internal (MatchEvidence? Evidence, List<CandidateSummary> TiedCandidates) TryMatchBracket2WithTies(
        ImageRecord_LAMBDA record,
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules)
    {
        string   filename       = record.InitialFullName ?? string.Empty;
        string[] tokens         = GetNumericTokensFromFilename(filename);
        string   sourceFilename = filename;
        string   imageId        = Path.GetFileNameWithoutExtension(filename);

        if (tokens.Length < 2)
            return (null, []);

        // Collect best TCD per FamilyID
        Dictionary<string, (double Tcd, string[] Subset, string PropertyName)> bestPerFamily =
            new(StringComparer.OrdinalIgnoreCase);

        for (int start = 0; start < tokens.Length; start++)
        {
            for (int length = 2; length <= tokens.Length - start; length++)
            {
                string[] subset       = tokens.Skip(start).Take(length).ToArray();
                string   concatenated = string.Concat(subset);

                foreach (FamilyIDRecord family in families)
                {
                    foreach (MatchingRule rule in numericRules)
                    {
                        string? target = GetFamilyDigitsForField(family, rule.ExcelField);
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
            return (null, []);

        // Build CandidateSummary list for tie reporting
        List<CandidateSummary> tiedCandidates = bestPerFamily
            .Select(kv => new CandidateSummary(
                kv.Key,
                TokenizedConcatenationDistance.ConvertDistanceToConfidence(kv.Value.Tcd) / 100.0,
                "NumericMatcher.Bracket2"))
            .ToList();

        if (bestPerFamily.Count > 1)
            return (null, tiedCandidates); // tie — caller records rejected candidates

        KeyValuePair<string, (double Tcd, string[] Subset, string PropertyName)> match = bestPerFamily.First();
        string matcherName = "NumericMatcher.Bracket2";
        double confidence  = TokenizedConcatenationDistance.ConvertDistanceToConfidence(match.Value.Tcd) / 100.0;

        MatchEvidence evidence = new MatchEvidence
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
                    GetFamilyDigitsForField(FindFamily(families, match.Key)!, match.Value.PropertyName) ?? string.Empty,
                    match.Value.PropertyName,
                    match.Key,
                    confidence)
            ],
            ImageNgpSummary  = BuildNgpSummary(record),
            SafeExplanation  = $"Bracket2: tokens [{string.Join(", ", match.Value.Subset)}] concatenated to match family {match.Key} (TCD={match.Value.Tcd:F3})."
        };
        return (evidence, []);
    }

    //  Bracket 4 support 

    /// <summary>
    /// Reduces the candidate pool for Bracket 4 semantic matching by eliminating families
    /// whose numeric fields are contradicted by tokens in the filename.
    /// Tokens that match some but not all candidates narrow the pool to only the matching families.
    /// Tokens that match all or none of the remaining candidates have no effect.
    /// </summary>
    internal IReadOnlyList<FamilyIDRecord> ReduceCandidatesByNumericTokens(
        string filename,
        IReadOnlyList<FamilyIDRecord> candidates,
        IReadOnlyList<MatchingRule> numericRules)
    {
        string[] tokens = GetNumericTokensFromFilename(filename);
        if (tokens.Length == 0 || candidates.Count <= 1)
            return candidates;

        List<FamilyIDRecord> remaining = [..candidates];

        foreach (string token in tokens)
        {
            List<FamilyIDRecord> matching = remaining
                .Where(f => numericRules.Any(r => GetFamilyDigitsForField(f, r.ExcelField) == token))
                .ToList();

            // Only reduce when the token is discriminating (matches some but not all)
            if (matching.Count > 0 && matching.Count < remaining.Count)
                remaining = matching;
        }

        return remaining;
    }

    //  Helpers 

    /// <summary>
    /// Extracts all digit sequences from the filename stem, preserving left-to-right order.
    /// </summary>
    private static string[] GetNumericTokensFromFilename(string filename)
    {
        string stem = Path.GetFileNameWithoutExtension(filename);
        return DigitSequencePattern.Matches(stem)
            .Select(m => m.Value)
            .ToArray();
    }

    /// <summary>
    /// The digits PRISM matches a filename token against for one rule field.
    /// For the FamilyID field this is the intrinsic <see cref="FamilyIDRecord.FamilyID"/>; for any other
    /// field it is the digits of the family's Excel column value (CanonicalProperties).
    /// </summary>
    private string? GetFamilyDigitsForField(FamilyIDRecord family, string excelField)
    {
        if (excelField.Equals(familyIdColumnName, StringComparison.OrdinalIgnoreCase))
            return DigitsOnly(family.FamilyID);

        return GetDigitsOfFamilyExcelColumn(family, excelField);
    }

    /// <summary>
    /// Returns the digits-only value of a family's Excel column (from CanonicalProperties), or null when
    /// the column is absent/empty or contains no digits. Does not handle the FamilyID — that is routed to
    /// the intrinsic identifier by <see cref="GetFamilyDigitsForField"/>.
    /// </summary>
    private static string? GetDigitsOfFamilyExcelColumn(FamilyIDRecord family, string excelField)
    {
        if (!family.CanonicalProperties.TryGetValue(excelField, out string? value) ||
            string.IsNullOrWhiteSpace(value))
            return null;

        return DigitsOnly(value);
    }

    /// <summary>
    /// Concatenates every digit character in <paramref name="value"/>; returns null when there are none.
    /// </summary>
    private static string? DigitsOnly(string value)
    {
        string digits = string.Concat(DigitsOnlyPattern.Matches(value).Select(m => m.Value));
        return digits.Length > 0 ? digits : null;
    }

    private static FamilyIDRecord? FindFamily(IReadOnlyList<FamilyIDRecord> families, string familyId) =>
        families.FirstOrDefault(f => f.FamilyID.Equals(familyId, StringComparison.OrdinalIgnoreCase));

    private string FindMatchingToken(
        string[] filenameTokens,
        string familyId,
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules)
    {
        FamilyIDRecord? family = FindFamily(families, familyId);
        if (family is null) return string.Empty;

        foreach (string token in filenameTokens)
        {
            foreach (MatchingRule rule in numericRules)
            {
                string? target = GetFamilyDigitsForField(family, rule.ExcelField);
                if (target == token) return token;
            }
        }

        return string.Empty;
    }

    private MatchingRule? FindMatchedRule(
        string familyId,
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules)
    {
        FamilyIDRecord? family = FindFamily(families, familyId);
        if (family is null) return null;

        foreach (MatchingRule rule in numericRules)
        {
            if (GetFamilyDigitsForField(family, rule.ExcelField) is not null)
                return rule;
        }

        return null;
    }

    private static string? BuildNgpSummary(ImageRecord_LAMBDA record) =>
        record.SelectedPhenotype is null ? null : $"phenotype={record.SelectedPhenotype}";
}
