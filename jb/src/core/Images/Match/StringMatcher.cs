using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Matches images to FamilyIDs using normalized string token comparison.
/// Bracket 3: accepts an assignment only when exactly one FamilyID has string token evidence.
/// </summary>
internal sealed class StringMatcher
{
    private static readonly Regex TokenSplitPattern = new(
        @"[^a-zA-ZÀ-ÖØ-öø-ÿ0-9]+",
        RegexOptions.Compiled);

    private readonly TranslationConfig translationConfig;

    internal StringMatcher(TranslationConfig translationConfig)
    {
        this.translationConfig = translationConfig;
    }

    // ─── Bracket 3 ────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts Bracket 3 matching: filename string tokens narrow down to exactly one FamilyID.
    /// </summary>
    /// <returns>Accepted MatchEvidence when exactly one FamilyID matches; null otherwise.</returns>
    internal MatchEvidence? TryMatch(
        ImageRecord_LAMBDA record,
        IReadOnlyList<FamilyRecord> families)
    {
        string filename      = record.InitialFullName ?? string.Empty;
        string sourceFilename = filename;
        string imageId       = Path.GetFileNameWithoutExtension(filename);

        IReadOnlyList<string> imageTokens = ExtractImageTokens(filename);
        if (imageTokens.Count == 0)
            return null;

        // Build evidence for every family; keep only families that have at least one token match
        List<(FamilyRecord Family, List<TokenEvidenceItem> Evidence)> candidates = [];

        foreach (FamilyRecord family in families)
        {
            List<TokenEvidenceItem> evidence = BuildStringEvidence(imageTokens, family);
            if (evidence.Count > 0)
                candidates.Add((family, evidence));
        }

        if (candidates.Count != 1)
            return null; // zero → no match; two+ → tie

        (FamilyRecord matched, List<TokenEvidenceItem> tokenEvidence) = candidates[0];
        double score = ComputeStringScore(tokenEvidence.Count, imageTokens.Count);
        string matcherName = "StringMatcher.Bracket3";

        return new MatchEvidence
        {
            ImageId             = imageId,
            SourceFilename      = sourceFilename,
            FinalFamilyId       = matched.FamilyID,
            FinalScore          = score,
            IsKo                = false,
            AcceptedMatcherName = matcherName,
            StringTokenEvidence = tokenEvidence,
            TopCandidates       = [new CandidateSummary(matched.FamilyID, score, matcherName)],
            ImageNgpSummary     = BuildNgpSummary(record),
            SafeExplanation     = $"Bracket3: {tokenEvidence.Count} string token(s) uniquely matched family {matched.FamilyID} (score={score:F3})."
        };
    }

    // ─── Evidence building ────────────────────────────────────────────────────

    private List<TokenEvidenceItem> BuildStringEvidence(
        IReadOnlyList<string> imageTokens,
        FamilyRecord family)
    {
        List<TokenEvidenceItem> evidence = [];

        foreach (KeyValuePair<string, IReadOnlyList<string>> property in family.NormalizedTokens)
        {
            ExcelColumnClassification classification = family.ColumnClassifications.TryGetValue(
                property.Key, out ExcelColumnClassification cls)
                    ? cls
                    : ExcelColumnClassification.Descriptive;

            // Numeric and FamilyID columns belong to NumericMatcher
            if (classification == ExcelColumnClassification.Numerical ||
                classification == ExcelColumnClassification.FamilyID)
                continue;

            IReadOnlyList<string> familyTokens = PrepareExcelTokens(property.Value, property.Key, classification);

            foreach (string imageToken in imageTokens)
            {
                string? matchedFamilyToken = familyTokens.FirstOrDefault(
                    ft => translationConfig.AreMatchingTokens(imageToken, ft));

                if (matchedFamilyToken is not null)
                {
                    bool isExact = matchedFamilyToken.Equals(imageToken, StringComparison.OrdinalIgnoreCase);
                    evidence.Add(new TokenEvidenceItem(
                        imageToken,
                        matchedFamilyToken,
                        property.Key,
                        family.FamilyID,
                        isExact ? 1.0 : 0.85));
                }
            }
        }

        return evidence;
    }

    /// <summary>
    /// Applies NoiseFilter to Descriptive and Mixed columns before token matching.
    /// Categorical columns are used as-is.
    /// </summary>
    private static IReadOnlyList<string> PrepareExcelTokens(
        IReadOnlyList<string> tokens,
        string propertyName,
        ExcelColumnClassification classification)
    {
        if (classification is ExcelColumnClassification.Descriptive or ExcelColumnClassification.Mixed)
        {
            return tokens
                .Select(t => NoiseFilter.RemoveNumericNoiseForMatching(t, propertyName))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();
        }

        return tokens;
    }

    // ─── Token extraction ─────────────────────────────────────────────────────

    /// <summary>
    /// Extracts and normalizes string tokens from a filename.
    /// Applies lowercase, diacritic stripping, separator splitting, and stop-word removal.
    /// Excludes pure-digit tokens (those belong to NumericMatcher).
    /// </summary>
    private IReadOnlyList<string> ExtractImageTokens(string filename)
    {
        string stem = Path.GetFileNameWithoutExtension(filename);
        string normalized = NormalizeDiacritics(stem.ToLowerInvariant());

        return TokenSplitPattern.Split(normalized)
            .Where(t => t.Length >= 2 && !IsAllDigits(t))
            .Where(t => !translationConfig.IsStopWord(t))
            .ToList();
    }

    // ─── Scoring ─────────────────────────────────────────────────────────────

    private static double ComputeStringScore(int matchedTokenCount, int totalImageTokenCount)
    {
        if (totalImageTokenCount == 0)
            return 0.0;

        return Math.Min(1.0, (double)matchedTokenCount / Math.Max(1, totalImageTokenCount));
    }

    private static string? BuildNgpSummary(ImageRecord_LAMBDA record) =>
        record.SelectedPhenotype is null ? null : $"phenotype={record.SelectedPhenotype}";

    // ─── Normalization ────────────────────────────────────────────────────────

    private static string NormalizeDiacritics(string input)
    {
        string decomposed = input.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(decomposed.Length);

        foreach (char ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool IsAllDigits(string token)
    {
        foreach (char ch in token)
        {
            if (!char.IsDigit(ch)) return false;
        }

        return true;
    }
}
