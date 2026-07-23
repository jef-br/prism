using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Prism.Services.Matching;

/// <summary>
/// Matches images to FamilyIDs using normalized string token comparison.
/// Bracket 3: accepts an assignment only when exactly one FamilyID has string token evidence.
/// </summary>
internal sealed class StringMatcher
{
    // Non-exact (synonym/fuzzy) token-match confidence — empirical calibration, see Match/jbtodo.md.
    private const double NonExactTokenMatchConfidence = 0.85;

    private static readonly Regex TokenSplitPattern = new(
        @"[^a-zA-ZÀ-ÖØ-öø-ÿ0-9]+",
        RegexOptions.Compiled);

    // Splits mixed tokens at letter↔digit boundaries: "magenta76" → ["magenta", "76"].
    private static readonly Regex AlphaDigitBoundaryPattern = new(
        @"(?<=\d)(?=\D)|(?<=\D)(?=\d)",
        RegexOptions.Compiled);

    private readonly TranslationConfig translationConfig;
    private readonly int bracket3MinDistinctTokens;
    private readonly int identifierTokenMinLength;
    private readonly bool indexExcelTokenBigrams;
    private readonly int fuzzyMinTokenLength;
    private readonly int fuzzyMaxEditDistance;
    private readonly double fuzzyMatchScore;

    // Inverted token index (family token → postings), built once per family set so Bracket 3 does not
    // rescan every family for every image. Keyed by reference identity of the families list.
    private Dictionary<string, List<Posting>>? tokenIndex;
    private IReadOnlyList<FamilyIDRecord>? indexedFamilies;

    // Categorical-only subset of tokenIndex, built alongside it. PRISM-match.md: string matching
    // tolerates edit distance for categorical columns (color, material, product type) specifically —
    // spelling mistakes are penalized less than a serial-number discrepancy. Descriptive/Mixed columns
    // stay exact-match-only (free text is too large/ambiguous for a safe fuzzy scan).
    private Dictionary<string, List<Posting>>? categoricalTokenIndex;

    internal StringMatcher(TranslationConfig translationConfig, int bracket3MinDistinctTokens, int identifierTokenMinLength, bool indexExcelTokenBigrams, int fuzzyMinTokenLength, int fuzzyMaxEditDistance, double fuzzyMatchScore)
    {
        this.translationConfig = translationConfig;
        this.bracket3MinDistinctTokens = bracket3MinDistinctTokens;
        this.identifierTokenMinLength = identifierTokenMinLength;
        this.indexExcelTokenBigrams = indexExcelTokenBigrams;
        this.fuzzyMinTokenLength = fuzzyMinTokenLength;
        this.fuzzyMaxEditDistance = fuzzyMaxEditDistance;
        this.fuzzyMatchScore = fuzzyMatchScore;
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
        string filename      = record.MatchingName;
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
        {
            // Top tie: short digit tokens (excluded from string tokens) may still discriminate —
            // "76" picks the family whose columns carry color code 76 over its 13-coded sibling.
            string? tiebreakWinner = BreakTieWithShortDigitTokens(filename, ranked, families);
            if (tiebreakWinner is null)
                return null;

            ranked = [ranked.First(r => r.FamilyId == tiebreakWinner)];
        }

        // Precision gate: a winner carried by fewer distinct tokens than configured (e.g. a single
        // shared color word) is not accepted here — later brackets may still assign it — unless one
        // matched token is identifier-grade (letters+digits, unique to this family across the index).
        if (ranked[0].DistinctMatches < bracket3MinDistinctTokens &&
            !HasUniqueIdentifierToken(ranked[0].Evidence, ranked[0].FamilyId))
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

    /// <summary>
    /// Attempts to break a Bracket 3 top tie using the pure-digit filename tokens that string
    /// matching excludes. A short digit token that appears in the normalized tokens of exactly one
    /// tied family picks that family; tokens pointing at different families leave the tie standing.
    /// </summary>
    /// <returns>The FamilyID every discriminating digit token agrees on; null when none or conflicting.</returns>
    private string? BreakTieWithShortDigitTokens(
        string filename,
        List<(string FamilyId, List<TokenEvidenceItem> Evidence, int DistinctMatches)> ranked,
        IReadOnlyList<FamilyIDRecord> families)
    {
        int topMatches = ranked[0].DistinctMatches;
        HashSet<string> tiedFamilyIds = ranked
            .Where(r => r.DistinctMatches == topMatches)
            .Select(r => r.FamilyId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<FamilyIDRecord> tiedFamilies = families
            .Where(f => tiedFamilyIds.Contains(f.FamilyID))
            .ToList();

        string stem = Path.GetFileNameWithoutExtension(filename);
        string? agreedWinner = null;

        foreach (string token in TokenSplitPattern.Split(stem))
        {
            if (token.Length == 0 || !IsAllDigits(token))
                continue;

            List<string> holders = tiedFamilies
                .Where(f => f.NormalizedTokens.Values.Any(tokens => tokens.Contains(token, StringComparer.OrdinalIgnoreCase)))
                .Select(f => f.FamilyID)
                .ToList();

            if (holders.Count != 1)
                continue; // not discriminating among the tied families

            if (agreedWinner is not null && !agreedWinner.Equals(holders[0], StringComparison.OrdinalIgnoreCase))
                return null; // two digit tokens point at different tied families

            agreedWinner = holders[0];
        }

        return agreedWinner;
    }

    /// <summary>
    /// True when the evidence contains an identifier-grade filename token — letters and digits mixed,
    /// at least IdentifierTokenMinLength long, and present in exactly one family across the token
    /// index. Such a token (e.g. "1707527E", "A129") is a reference code, not a shared word, and is
    /// allowed to carry a Bracket 3 assignment alone.
    /// </summary>
    private bool HasUniqueIdentifierToken(List<TokenEvidenceItem> evidence, string familyId)
    {
        if (identifierTokenMinLength <= 0 || tokenIndex is null)
            return false;

        foreach (TokenEvidenceItem item in evidence)
        {
            string normalized = NormalizeDiacritics(item.FilenameToken.ToLowerInvariant());

            if (normalized.Length < identifierTokenMinLength ||
                !normalized.Any(char.IsLetter) || !normalized.Any(char.IsDigit))
                continue;

            if (!tokenIndex.TryGetValue(normalized, out List<Posting>? postings))
                continue;

            HashSet<string> holders = postings
                .Select(p => p.FamilyId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (holders.Count == 1 && holders.Contains(familyId))
                return true;
        }

        return false;
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
        Dictionary<string, List<Posting>> categoricalIndex = GetOrBuildCategoricalTokenIndex(families);
        Dictionary<string, List<TokenEvidenceItem>> byFamily = new(StringComparer.OrdinalIgnoreCase);

        foreach (FilenameToken imageToken in imageTokens)
        {
            bool anyHit = false;

            foreach (string key in ExpandSynonymKeys(imageToken.Normalized).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!index.TryGetValue(key, out List<Posting>? postings))
                    continue;

                anyHit = true;
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
                        isExact ? 1.0 : NonExactTokenMatchConfidence));
                }
            }

            // Exact/synonym lookup found nothing for this token — fall back to a bounded edit-distance
            // scan against categorical-column tokens only, so a typo like "gray"/"grey" still counts as
            // evidence instead of forcing the image into a later bracket or a KO.
            if (!anyHit)
                CollectFuzzyCategoricalEvidence(imageToken, categoricalIndex, byFamily);
        }

        return byFamily;
    }

    /// <summary>
    /// Categorical-only edit-distance fallback. Compares the image token against every distinct
    /// categorical family token (a small, low-cardinality set per PRISM-match.md) and records evidence
    /// for any within FuzzyMaxEditDistance. Descriptive/Mixed columns never reach this path.
    /// </summary>
    private void CollectFuzzyCategoricalEvidence(
        FilenameToken imageToken,
        Dictionary<string, List<Posting>> categoricalIndex,
        Dictionary<string, List<TokenEvidenceItem>> byFamily)
    {
        if (imageToken.Normalized.Length < fuzzyMinTokenLength)
            return;

        foreach (KeyValuePair<string, List<Posting>> entry in categoricalIndex)
        {
            if (entry.Key.Length < fuzzyMinTokenLength ||
                Math.Abs(entry.Key.Length - imageToken.Normalized.Length) > fuzzyMaxEditDistance)
                continue;

            if (ModelBuilder.ComputeLevenshteinDistance(imageToken.Normalized, entry.Key) > fuzzyMaxEditDistance)
                continue;

            foreach (Posting posting in entry.Value)
            {
                if (!byFamily.TryGetValue(posting.FamilyId, out List<TokenEvidenceItem>? evidence))
                    byFamily[posting.FamilyId] = evidence = [];

                evidence.Add(new TokenEvidenceItem(
                    imageToken.Original,
                    posting.FamilyToken,
                    posting.PropertyName,
                    posting.FamilyId,
                    fuzzyMatchScore));
            }
        }
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
        Dictionary<string, List<Posting>> categorical = new(StringComparer.OrdinalIgnoreCase);

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

                    Posting posting = new(family.FamilyID, property.Key, familyToken);
                    AddPosting(index, familyToken, posting);

                    if (classification == ExcelColumnClassification.Categorical)
                        AddPosting(categorical, familyToken, posting);
                }

                if (indexExcelTokenBigrams)
                    IndexAdjacentTokenBigrams(index, family, property.Key);
            }
        }

        tokenIndex = index;
        categoricalTokenIndex = categorical;
        indexedFamilies = families;
        return index;
    }

    /// <summary>Categorical-only subset of the token index, built and cached alongside GetOrBuildTokenIndex.</summary>
    private Dictionary<string, List<Posting>> GetOrBuildCategoricalTokenIndex(IReadOnlyList<FamilyIDRecord> families)
    {
        GetOrBuildTokenIndex(families);
        return categoricalTokenIndex!;
    }

    /// <summary>Appends one posting to the index bucket for <paramref name="key"/>.</summary>
    private static void AddPosting(Dictionary<string, List<Posting>> index, string key, Posting posting)
    {
        if (!index.TryGetValue(key, out List<Posting>? postings))
            index[key] = postings = [];

        postings.Add(posting);
    }

    /// <summary>
    /// Indexes concatenations of adjacent cell tokens in both orders for one family column, so a
    /// glued filename token ("palmblue", "magenta76") finds the family whose cell reads
    /// "…PALM BLUE" / "76 MAGENTA". Adjacency comes from the original cell values —
    /// NormalizedTokens are sorted alphabetically and have lost it. Digit-only pairs are skipped
    /// (digit concatenations belong to NumericMatcher).
    /// </summary>
    private void IndexAdjacentTokenBigrams(Dictionary<string, List<Posting>> index, FamilyIDRecord family, string propertyName)
    {
        if (!family.OriginalSourceCellValues.TryGetValue(propertyName, out IReadOnlyList<string>? cellValues))
            return;

        foreach (string cellValue in cellValues)
        {
            string? previous = null;

            foreach (string rawToken in TokenSplitPattern.Split(cellValue))
            {
                string token = NormalizeDiacritics(rawToken.ToLowerInvariant());
                if (token.Length < 2)
                {
                    previous = null; // a 1-char fragment breaks adjacency
                    continue;
                }

                if (previous is not null && !(IsAllDigits(previous) && IsAllDigits(token)))
                {
                    AddBigramPosting(index, previous + token, family.FamilyID, propertyName, $"{previous} {token}");
                    AddBigramPosting(index, token + previous, family.FamilyID, propertyName, $"{previous} {token}");
                }

                previous = token;
            }
        }
    }

    /// <summary>Adds one bigram posting unless the same family already holds that key.</summary>
    private static void AddBigramPosting(Dictionary<string, List<Posting>> index, string key, string familyId, string propertyName, string familyToken)
    {
        if (!index.TryGetValue(key, out List<Posting>? postings))
            index[key] = postings = [];

        if (!postings.Any(p => p.FamilyId.Equals(familyId, StringComparison.OrdinalIgnoreCase)))
            postings.Add(new Posting(familyId, propertyName, familyToken));
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

    /// <summary>
    /// Counts the meaningful filename tokens (same extraction ExtractImageTokens uses for Bracket 3/4
    /// matching). Used by SemanticMatcher to compute stringSignal's denominator from the filename
    /// itself, rather than mixing in an unrelated candidate-pool count.
    /// </summary>
    internal int CountFilenameTokens(string filename) => ExtractImageTokens(filename).Count;

    //  Token extraction

    // Pairs the original (pre-normalization) text with the normalized form used for comparison.
    private readonly record struct FilenameToken(string Original, string Normalized);

    // Inverted-index posting: one family/column that contains a given accepted token.
    private readonly record struct Posting(string FamilyId, string PropertyName, string FamilyToken);

    /// <summary>
    /// Extracts string tokens from a filename, preserving both the original text and the
    /// normalized form (lowercase, diacritics stripped) used for comparison.
    /// Excludes pure-digit tokens (those belong to NumericMatcher). Mixed tokens are additionally
    /// split at letter↔digit boundaries so "magenta76" also yields "magenta".
    /// </summary>
    private IReadOnlyList<FilenameToken> ExtractImageTokens(string filename)
    {
        string stem = Path.GetFileNameWithoutExtension(filename);
        List<FilenameToken> tokens = [];

        foreach (string raw in TokenSplitPattern.Split(stem))
        {
            AddImageToken(tokens, raw);

            string[] parts = AlphaDigitBoundaryPattern.Split(raw);
            if (parts.Length > 1)
            {
                foreach (string part in parts)
                    AddImageToken(tokens, part);
            }
        }

        return tokens;
    }

    /// <summary>Appends one candidate token when it passes the length/digit/stop-word filters.</summary>
    private void AddImageToken(List<FilenameToken> tokens, string raw)
    {
        FilenameToken token = new(raw, NormalizeDiacritics(raw.ToLowerInvariant()));

        if (token.Normalized.Length < 2 || IsAllDigits(token.Normalized) ||
            translationConfig.IsStopWord(token.Normalized))
            return;

        if (!tokens.Any(t => t.Normalized.Equals(token.Normalized, StringComparison.Ordinal)))
            tokens.Add(token);
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
