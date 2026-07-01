using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Prism.Core;

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

    // Inverted token index (family token → postings), built once per family set so Bracket 3 does not
    // rescan every family for every image. Keyed by reference identity of the families list.
    private Dictionary<string, List<Posting>>? tokenIndex;
    private IReadOnlyList<FamilyIDRecord>? indexedFamilies;

    internal StringMatcher(TranslationConfig translationConfig)
    {
        this.translationConfig = translationConfig;
    }

    //  Bracket 3 

    /// <summary>
    /// Attempts Bracket 3 matching: filename string tokens narrow down to exactly one FamilyID.
    /// </summary>
    /// <returns>Accepted MatchEvidence when exactly one FamilyID matches; null otherwise.</returns>
    internal MatchEvidence? TryMatch(
        ImageRecord_LAMBDA record,
        IReadOnlyList<FamilyIDRecord> families)
    {
        string filename      = record.InitialFullName ?? string.Empty;
        string sourceFilename = filename;
        string imageId       = Path.GetFileNameWithoutExtension(filename);

        IReadOnlyList<FilenameToken> imageTokens = ExtractImageTokens(filename);
        if (imageTokens.Count == 0)
            return null;

        // Collect token evidence grouped by family via an inverted token index (built once per family
        // set). This replaces an O(images × families × tokens) scan that made large, verbose catalogues
        // (paragraph-length description columns) pathologically slow.
        Dictionary<string, List<TokenEvidenceItem>> evidenceByFamily = CollectEvidenceByFamily(imageTokens, families);
        if (evidenceByFamily.Count == 0)
            return null;

        // Strict-winner: accept the family that matched the most distinct filename tokens. A true
        // top-tie (e.g. a common token like "ivory" shared equally by several families) is not
        // discriminating and is rejected — only a unique strongest family is accepted.
        List<(string FamilyId, List<TokenEvidenceItem> Evidence, int DistinctMatches)> ranked = evidenceByFamily
            .Select(pair => (
                FamilyId: pair.Key,
                Evidence: pair.Value,
                DistinctMatches: pair.Value.Select(e => e.FilenameToken).Distinct(StringComparer.OrdinalIgnoreCase).Count()))
            .OrderByDescending(candidate => candidate.DistinctMatches)
            .ToList();

        if (ranked.Count > 1 && ranked[0].DistinctMatches == ranked[1].DistinctMatches)
            return null;

        (string matchedFamilyId, List<TokenEvidenceItem> tokenEvidence, int winnerMatches) = ranked[0];
        double score = ComputeStringScore(winnerMatches, imageTokens.Count);
        string matcherName = "StringMatcher.Bracket3";

        return new MatchEvidence
        {
            ImageId             = imageId,
            SourceFilename      = sourceFilename,
            FinalFamilyId       = matchedFamilyId,
            FinalScore          = score,
            IsKo                = false,
            AcceptedMatcherName = matcherName,
            StringTokenEvidence = tokenEvidence,
            TopCandidates       = [new CandidateSummary(matchedFamilyId, score, matcherName)],
            ImageNgpSummary     = BuildNgpSummary(record),
            SafeExplanation     = $"Bracket3: {winnerMatches} string token(s) uniquely matched family {matchedFamilyId} (score={score:F3})."
        };
    }

    //  Inverted token index (Brackets 3 and 4)

    /// <summary>
    /// Groups token evidence by FamilyID using the inverted token index. For each image token (and its
    /// configured synonyms) it looks up the families whose accepted columns contain that token, instead
    /// of scanning every family.
    /// </summary>
    private Dictionary<string, List<TokenEvidenceItem>> CollectEvidenceByFamily(
        IReadOnlyList<FilenameToken> imageTokens,
        IReadOnlyList<FamilyIDRecord> families)
    {
        Dictionary<string, List<Posting>> index = GetOrBuildTokenIndex(families);
        Dictionary<string, List<TokenEvidenceItem>> byFamily = new(StringComparer.OrdinalIgnoreCase);

        foreach (FilenameToken imageToken in imageTokens)
        {
            foreach (string key in ExpandSynonymKeys(imageToken.Normalized).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!index.TryGetValue(key, out List<Posting>? postings))
                    continue;

                bool isExact = key.Equals(imageToken.Normalized, StringComparison.OrdinalIgnoreCase);

                foreach (Posting posting in postings)
                {
                    if (!byFamily.TryGetValue(posting.FamilyId, out List<TokenEvidenceItem>? evidence))
                        byFamily[posting.FamilyId] = evidence = [];

                    evidence.Add(new TokenEvidenceItem(
                        imageToken.Original,
                        posting.FamilyToken,
                        posting.PropertyName,
                        posting.FamilyId,
                        isExact ? 1.0 : 0.85));
                }
            }
        }

        return byFamily;
    }

    /// <summary>
    /// Builds (once per family set, cached by reference) an inverted index mapping each accepted family
    /// token to the families and columns that contain it. Numeric and FamilyID columns are excluded;
    /// Descriptive/Mixed columns are noise-filtered before indexing.
    /// </summary>
    private Dictionary<string, List<Posting>> GetOrBuildTokenIndex(IReadOnlyList<FamilyIDRecord> families)
    {
        if (tokenIndex is not null && ReferenceEquals(indexedFamilies, families))
            return tokenIndex;

        Dictionary<string, List<Posting>> index = new(StringComparer.OrdinalIgnoreCase);

        foreach (FamilyIDRecord family in families)
        {
            foreach (KeyValuePair<string, IReadOnlyList<string>> property in family.NormalizedTokens)
            {
                ExcelColumnClassification classification = family.ColumnClassifications.TryGetValue(
                    property.Key, out ExcelColumnClassification cls)
                        ? cls
                        : ExcelColumnClassification.Descriptive;

                if (classification == ExcelColumnClassification.Numerical ||
                    classification == ExcelColumnClassification.FamilyID)
                    continue;

                foreach (string familyToken in PrepareExcelTokens(property.Value, property.Key, classification))
                {
                    if (string.IsNullOrEmpty(familyToken))
                        continue;

                    if (!index.TryGetValue(familyToken, out List<Posting>? postings))
                        index[familyToken] = postings = [];

                    postings.Add(new Posting(family.FamilyID, property.Key, familyToken));
                }
            }
        }

        tokenIndex = index;
        indexedFamilies = families;
        return index;
    }

    /// <summary>
    /// Yields the lookup keys for an image token: the token itself plus every term sharing a configured
    /// synonym group with it, so synonym matches resolve through the index.
    /// </summary>
    private IEnumerable<string> ExpandSynonymKeys(string normalizedToken)
    {
        yield return normalizedToken;

        foreach (SynonymGroup group in translationConfig.SynonymGroups)
        {
            if (!group.Terms.Any(term => term.Trim().ToLowerInvariant() == normalizedToken))
                continue;

            foreach (string term in group.Terms)
            {
                string normalizedTerm = term.Trim().ToLowerInvariant();
                if (normalizedTerm != normalizedToken)
                    yield return normalizedTerm;
            }
        }
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

    //  Bracket 4 support

    /// <summary>
    /// Scores each candidate by how many filename string tokens appear in its columns.
    /// Used by SemanticMatcher (Bracket 4) to rank and reduce the candidate pool.
    /// Returns all candidates (from <paramref name="candidates"/>) that have at least one token
    /// match, ordered by match count descending.
    /// </summary>
    /// <param name="filename">The image filename to extract string tokens from.</param>
    /// <param name="candidates">
    /// The per-image-filtered candidate subset to score. Changes on nearly every call within a
    /// single Bracket 4 run (post CLIP/numeric reduction), so it is never used to build the index.
    /// </param>
    /// <param name="indexScope">
    /// The stable superset the inverted token index is built (and reference-equality cached) from —
    /// <c>unassignedFamilies</c>, computed once per Bracket 4 run. Evidence is collected for this
    /// whole scope, then filtered down to <paramref name="candidates"/> by FamilyID membership, so
    /// the index is built once per bracket run instead of once per image.
    /// </param>
    internal IReadOnlyList<(FamilyIDRecord Family, int MatchCount, List<TokenEvidenceItem> Evidence)>
        ScoreCandidatesByStringTokens(string filename, IReadOnlyList<FamilyIDRecord> candidates, IReadOnlyList<FamilyIDRecord> indexScope)
    {
        IReadOnlyList<FilenameToken> imageTokens = ExtractImageTokens(filename);
        if (imageTokens.Count == 0)
            return [];

        Dictionary<string, List<TokenEvidenceItem>> evidenceByFamily = CollectEvidenceByFamily(imageTokens, indexScope);
        if (evidenceByFamily.Count == 0)
            return [];

        List<(FamilyIDRecord Family, int MatchCount, List<TokenEvidenceItem> Evidence)> results = [];

        foreach (FamilyIDRecord family in candidates)
        {
            if (!evidenceByFamily.TryGetValue(family.FamilyID, out List<TokenEvidenceItem>? evidence))
                continue;

            // evidence.Count, NOT .Distinct().Count() — matches the exact pre-rewrite MatchCount
            // semantics (raw evidence-item count).
            results.Add((family, evidence.Count, evidence));
        }

        return [..results.OrderByDescending(r => r.MatchCount)];
    }

    //  Token extraction

    // Pairs the original (pre-normalization) text with the normalized form used for comparison.
    private readonly record struct FilenameToken(string Original, string Normalized);

    // Inverted-index posting: one family/column that contains a given accepted token.
    private readonly record struct Posting(string FamilyId, string PropertyName, string FamilyToken);

    /// <summary>
    /// Extracts string tokens from a filename, preserving both the original text and the
    /// normalized form (lowercase, diacritics stripped) used for comparison.
    /// Excludes pure-digit tokens (those belong to NumericMatcher).
    /// </summary>
    private IReadOnlyList<FilenameToken> ExtractImageTokens(string filename)
    {
        string stem = Path.GetFileNameWithoutExtension(filename);

        return TokenSplitPattern.Split(stem)
            .Select(t => new FilenameToken(t, NormalizeDiacritics(t.ToLowerInvariant())))
            .Where(t => t.Normalized.Length >= 2 && !IsAllDigits(t.Normalized))
            .Where(t => !translationConfig.IsStopWord(t.Normalized))
            .ToList();
    }

    //  Scoring 

    private static double ComputeStringScore(int matchedTokenCount, int totalImageTokenCount)
    {
        if (totalImageTokenCount == 0)
            return 0.0;

        return Math.Min(1.0, (double)matchedTokenCount / Math.Max(1, totalImageTokenCount));
    }

    private static string? BuildNgpSummary(ImageRecord_LAMBDA record) =>
        record.SelectedPhenotype is null ? null : $"phenotype={record.SelectedPhenotype}";

    //  Normalization 

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
