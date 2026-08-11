using System.Numerics;
using System.Text.RegularExpressions;

namespace Prism.Services.Matching;

/// <summary>
/// Matches images to FamilyIDs using numeric token extraction and TCD scoring.
/// Bracket 1 (single-token or monotoken, TCD = 0): one filename digit sequence, or all digits of
///     the filename concatenated into a single monotoken, exactly equals a family numeric value.
/// Bracket 2 (multi-token, TCD ≤ maxDistance): consecutive digit sequences (in filename order)
///     concatenate to a family numeric value.
/// Bracket 2-Permuted (any subset, TCD ≤ maxDistancePermuted): any token subset in any order
///     concatenates to a family numeric value; used as a fallback when the in-order pass finds nothing.
/// </summary>
internal sealed class NumericMatcher {
    private static readonly Regex DigitSequencePattern = new(@"\d+", RegexOptions.Compiled);
    private static readonly Regex DigitsOnlyPattern = new(@"\d", RegexOptions.Compiled);

    // The permuted fallback enumerates 2^T token subsets; beyond this many digit runs it is skipped.
    private const int PermutedTokenCap = 12;

    // Whole-value digit strings longer than this are not indexed by the all-columns pass — they are
    // merged multi-value cells or prose, not identifiers (rule-field targets stay uncapped for
    // backward compatibility with pipe-joined EAN columns).
    private const int MaxIndexedWholeValueDigits = 18;

    // Minimum tokens a permuted-subset candidate must combine — a single token is not a permutation.
    private const int MinPermutedSubsetTokens = 2;

    // Size of the 0-9 digit frequency table (one bucket per decimal digit).
    private const int DigitHistogramSize = 10;

    private readonly string familyIdColumnName;
    private readonly Config cfg;
    private readonly int minTokenLength;
    private readonly bool indexAllColumns;
    private readonly int substringRescueLength;

    /// <summary>NumericMatcher's tunables, loaded from MatchingConfig.json's match.numericMatcher section.</summary>
    public sealed class Config {
        /// <summary>
        /// Minimum digit count for a filename token or family digit target to act as standalone numeric
        /// evidence. 1 preserves the historical behavior; 5 stops shot suffixes (_01) and short RefCo
        /// digit fragments (e.g. "MGGE073" → "073") from producing false Bracket 1 ties. Shorter tokens
        /// may still participate in Bracket 2 concatenations whose combined length meets the threshold.
        /// </summary>
        public required int MinNumericTokenLength { get; init; }

        /// <summary>
        /// When true, the numeric digit index additionally covers every digit run (and capped whole-value
        /// digit string) of every family column — not just the configured numeric rule fields. Lets
        /// filenames match identifiers embedded in compound cells (e.g. label "MAN-Posy Green-1010930-60105").
        /// </summary>
        public required bool IndexDigitRunsAllColumns { get; init; }

        /// <summary>
        /// Minimum digit count for the numeric substring rescue pass (accepts the unique family whose
        /// digit target contains the filename token). 0 disables the pass.
        /// </summary>
        public required int MinSubstringRescueLength { get; init; }

        /// <summary>SubstringRescue match confidence — empirical calibration, see Match/jbtodo.md.</summary>
        public required double SubstringRescueConfidence { get; init; }

        /// <summary>Fallback MaxDistance when no numeric rule is configured — empirical calibration.</summary>
        public required double DefaultMaxDistanceFallback { get; init; }
    }

    // Inverted digit-target index (family target digits → postings), built once per (families, rules)
    // pair so Brackets 1–2 look up tokens/concatenations in O(1) instead of rescanning every family
    // (with a per-scan digits regex) for every image. Cached by reference identity like StringMatcher.
    private Dictionary<string, List<DigitPosting>>? digitIndex;
    private Dictionary<int, List<string>>? digitTargetsByLength;
    private IReadOnlyList<FamilyIDRecord>? indexedFamilies;
    private IReadOnlyList<MatchingRule>? indexedRules;

    // One family/rule pair whose digit target equals the index key.
    private readonly record struct DigitPosting(string FamilyId, string Field, MatchingRule Rule);

    /// <summary>
    /// Creates a numeric matcher.
    /// </summary>
    /// <param name="familyIdColumnName">
    /// The rule ExcelField that denotes the FamilyID (ExcelConfig.RecordPrimaryKey). The FamilyID rule
    /// resolves against the intrinsic <see cref="FamilyIDRecord.FamilyID"/> — the 8-digit PRISM identifier
    /// that is also the image filename stem — rather than an Excel column lookup.
    /// </param>
    /// <param name="cfg">NumericMatcher's tunables from MatchingConfig.json's match.numericMatcher section.</param>
    internal NumericMatcher(string familyIdColumnName, Config cfg) {
        this.familyIdColumnName = familyIdColumnName;
        this.cfg = cfg;
        this.minTokenLength = Math.Max(1, cfg.MinNumericTokenLength);
        this.indexAllColumns = cfg.IndexDigitRunsAllColumns;
        this.substringRescueLength = cfg.MinSubstringRescueLength;
    }

    //  Bracket 1

    /// <summary>
    /// Attempts Bracket 1 matching: a single numeric token in the filename, or the monotoken (all digits
    /// of the filename stem concatenated), exactly equals a family numeric value.
    /// </summary>
    /// <returns>Accepted MatchEvidence when exactly one FamilyID matches; null otherwise.</returns>
    internal MatchEvidence? TryMatchBracket1(
        ImageRecord_LAMBDA record,
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules) =>
        this.TryMatchBracket1WithTies(record, families, numericRules).Evidence;

    /// <summary>
    /// Attempts Bracket 1 matching, returning both the evidence and any tied candidates for rejection tracking.
    /// </summary>
    /// <returns>
    /// Evidence when exactly one FamilyID matches (null on tie or no match); all unique candidates when a tie occurs.
    /// </returns>
    internal (MatchEvidence? Evidence, List<CandidateSummary> TiedCandidates) TryMatchBracket1WithTies(
        ImageRecord_LAMBDA record,
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules) {
        string filename = record.MatchingName;
        string stem = Path.GetFileNameWithoutExtension(filename);
        string[] tokens = this.GetEligibleTokens(filename);
        string fileDigits = string.Concat(stem.Where(char.IsDigit));

        // Candidate set: individual digit-run tokens + the full monotoken (all digits of stem).
        // Tokens below MinNumericTokenLength are excluded — a shot suffix like "_01" must not act
        // as standalone evidence. Distinct avoids re-testing when the stem is one unbroken run.
        string[] candidates = fileDigits.Length >= this.minTokenLength && !tokens.Contains(fileDigits)
            ? [.. tokens, fileDigits]
            : tokens;

        Dictionary<string, List<DigitPosting>> index = this.GetOrBuildDigitIndex(families, numericRules);

        // First matching (token, posting) per FamilyID — one index lookup per candidate token.
        Dictionary<string, (string Token, DigitPosting Posting)> matchByFamily = new(StringComparer.OrdinalIgnoreCase);

        foreach (string candidate in candidates) {
            if (!index.TryGetValue(candidate, out List<DigitPosting>? postings))
                continue;

            foreach (DigitPosting posting in postings) {
                if (!matchByFamily.ContainsKey(posting.FamilyId))
                    matchByFamily[posting.FamilyId] = (candidate, posting);
            }
        }

        List<CandidateSummary> uniqueMatches = matchByFamily
            .Select(kv => new CandidateSummary(kv.Key, 1.0, "NumericMatcher.Bracket1"))
            .ToList();

        if (uniqueMatches.Count != 1)
            return (null, uniqueMatches); // 0 = no match; 2+ = tie (caller records ties)

        CandidateSummary winner = uniqueMatches[0];
        (string matchedToken, DigitPosting matchedPosting) = matchByFamily[winner.FamilyId];

        MatchEvidence evidence = new MatchEvidence {
            ImageId = stem,
            SourceFilename = filename,
            FinalFamilyId = winner.FamilyId,
            FinalScore = 1.0,
            IsKo = false,
            AcceptedMatcherName = winner.MatcherName,
            TopCandidates = uniqueMatches,
            NumericTokenEvidence =
            [
                // The index key equals the family's digit target, so token == target here.
                new TokenEvidenceItem(
                    matchedToken,
                    matchedToken,
                    matchedPosting.Field,
                    winner.FamilyId,
                    1.0)
            ],
            ImageNgpSummary = BuildNgpSummary(record),
            SafeExplanation = $"Bracket1: token '{matchedToken}' exactly matched family {winner.FamilyId}."
        };
        return (evidence, []);
    }

    //  Bracket 2

    /// <summary>
    /// Attempts Bracket 2 matching: consecutive numeric tokens concatenated (in filename order) match a family
    /// numeric value with TCD ≤ maxDistance. Falls back to a permuted pass (any token subset, any order,
    /// TCD ≤ MaxDistancePermuted) when the in-order pass finds nothing.
    /// </summary>
    /// <returns>Accepted MatchEvidence for the best match when exactly one FamilyID qualifies; null otherwise.</returns>
    internal MatchEvidence? TryMatchBracket2(
        ImageRecord_LAMBDA record,
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules) =>
        this.TryMatchBracket2WithTies(record, families, numericRules).Evidence;

    /// <summary>
    /// Attempts Bracket 2 matching, returning both the evidence and any tied candidates for rejection tracking.
    /// </summary>
    /// <returns>
    /// Evidence when exactly one FamilyID qualifies (null on tie or no match); all tied candidates when a tie occurs.
    /// </returns>
    internal (MatchEvidence? Evidence, List<CandidateSummary> TiedCandidates) TryMatchBracket2WithTies(
        ImageRecord_LAMBDA record,
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules) {
        string filename = record.MatchingName;
        string stem = Path.GetFileNameWithoutExtension(filename);
        string[] tokens = GetNumericTokensFromFilename(filename);

        if (tokens.Length < 2)
            return (null, []);

        Dictionary<string, List<DigitPosting>> index = this.GetOrBuildDigitIndex(families, numericRules);

        // Collect best TCD per FamilyID
        Dictionary<string, (double Tcd, string[] Subset, string PropertyName)> bestPerFamily =
            new(StringComparer.OrdinalIgnoreCase);

        // In-order pass: consecutive subsets in filename order, TCD ≤ MaxDistance. The concatenation
        // must equal a family target exactly, so each subset is one index lookup.
        for (int start = 0; start < tokens.Length; start++) {
            for (int length = 2; length <= tokens.Length - start; length++) {
                string[] subset = tokens.Skip(start).Take(length).ToArray();
                string concatenated = string.Concat(subset);

                if (!index.TryGetValue(concatenated, out List<DigitPosting>? postings))
                    continue;

                double tcd = TokenizedConcatenationDistance.Compute(subset, concatenated);
                if (double.IsPositiveInfinity(tcd))
                    continue;

                foreach (DigitPosting posting in postings) {
                    if (tcd > posting.Rule.MaxDistance)
                        continue;

                    if (!bestPerFamily.TryGetValue(posting.FamilyId, out var existing) || tcd < existing.Tcd)
                        bestPerFamily[posting.FamilyId] = (tcd, subset, posting.Field);
                }
            }
        }

        // Permuted fallback: all token subsets (consecutive or not), any order via TCD permutations,
        // TCD ≤ MaxDistancePermuted. Only runs when in-order pass found nothing. Candidate targets
        // come from the index grouped by exact length (a permutation concatenation preserves total
        // length), then a digit-count fingerprint check runs before the TCD computation.
        bool fromPermuted = false;
        if (bestPerFamily.Count == 0 && tokens.Length <= PermutedTokenCap &&
            numericRules.Any(r => r.MaxDistancePermuted > 0)) {
            fromPermuted = true;
            Dictionary<int, List<string>> targetsByLength = this.GetTargetsByLength(index);
            int fullMask = 1 << tokens.Length;

            for (int mask = 0; mask < fullMask; mask++) {
                if (BitOperations.PopCount((uint)mask) < MinPermutedSubsetTokens)
                    continue;

                string[] subset = Enumerable.Range(0, tokens.Length)
                    .Where(i => (mask >> i & 1) != 0)
                    .Select(i => tokens[i])
                    .ToArray();

                int subsetLength = subset.Sum(t => t.Length);
                if (!targetsByLength.TryGetValue(subsetLength, out List<string>? targets))
                    continue;

                int[] subsetDigitCounts = CountDigits(subset);

                foreach (string target in targets) {
                    if (!DigitCountsMatch(subsetDigitCounts, target))
                        continue;

                    double tcd = TokenizedConcatenationDistance.Compute(subset, target);
                    if (double.IsPositiveInfinity(tcd))
                        continue;

                    foreach (DigitPosting posting in index[target]) {
                        if (posting.Rule.MaxDistancePermuted <= 0 || tcd > posting.Rule.MaxDistancePermuted)
                            continue;

                        if (!bestPerFamily.TryGetValue(posting.FamilyId, out var existing) || tcd < existing.Tcd)
                            bestPerFamily[posting.FamilyId] = (tcd, subset, posting.Field);
                    }
                }
            }
        }

        if (bestPerFamily.Count == 0)
            return (null, []);

        string matcherName = fromPermuted ? "NumericMatcher.Bracket2-Permuted" : "NumericMatcher.Bracket2";

        // Build CandidateSummary list for tie reporting
        List<CandidateSummary> tiedCandidates = bestPerFamily
            .Select(kv => new CandidateSummary(
                kv.Key,
                TokenizedConcatenationDistance.ConvertDistanceToConfidence(kv.Value.Tcd) / 100.0,
                matcherName))
            .ToList();

        if (bestPerFamily.Count > 1)
            return (null, tiedCandidates); // tie — caller records rejected candidates

        KeyValuePair<string, (double Tcd, string[] Subset, string PropertyName)> match = bestPerFamily.First();
        double confidence = TokenizedConcatenationDistance.ConvertDistanceToConfidence(match.Value.Tcd) / 100.0;

        string safeExplanation = fromPermuted
            ? $"Bracket2-Permuted: tokens [{string.Join(", ", match.Value.Subset)}] (permuted) matched family {match.Key} (TCD={match.Value.Tcd:F3})."
            : $"Bracket2: tokens [{string.Join(", ", match.Value.Subset)}] concatenated to match family {match.Key} (TCD={match.Value.Tcd:F3}).";

        MatchEvidence evidence = new MatchEvidence {
            ImageId = stem,
            SourceFilename = filename,
            FinalFamilyId = match.Key,
            FinalScore = confidence,
            IsKo = false,
            AcceptedMatcherName = matcherName,
            TopCandidates = [new CandidateSummary(match.Key, confidence, matcherName)],
            NumericTokenEvidence =
            [
                new TokenEvidenceItem(
                    string.Join("+", match.Value.Subset),
                    this.GetFamilyDigitsForField(FindFamily(families, match.Key)!, match.Value.PropertyName) ?? string.Empty,
                    match.Value.PropertyName,
                    match.Key,
                    confidence)
            ],
            ImageNgpSummary = BuildNgpSummary(record),
            SafeExplanation = safeExplanation
        };
        return (evidence, []);
    }

    //  Bracket 2-Intersect

    /// <summary>
    /// Attempts intersection matching: when individual tokens each hit multiple families, the
    /// intersection of the per-token candidate sets can still be unique — e.g. color code "60105"
    /// hits every family of that color while article run "1010930" narrows to one garment, and only
    /// one family carries both. Hit sets come from eligible single tokens, the monotoken, and
    /// in-order concatenations; at least two non-empty hit sets are required.
    /// </summary>
    /// <returns>Accepted MatchEvidence when the intersection is exactly one FamilyID; null otherwise.</returns>
    internal (MatchEvidence? Evidence, List<CandidateSummary> TiedCandidates) TryMatchByTokenIntersection(
        ImageRecord_LAMBDA record,
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules) {
        string filename = record.MatchingName;
        string stem = Path.GetFileNameWithoutExtension(filename);
        string[] tokens = GetNumericTokensFromFilename(filename);

        Dictionary<string, List<DigitPosting>> index = this.GetOrBuildDigitIndex(families, numericRules);

        // One entry per evidence source (token or concatenation) that hit at least one family.
        List<(string Token, HashSet<string> Families)> hitSets = [];

        foreach (string token in this.GetEligibleTokens(filename))
            CollectHitSet(index, token, hitSets);

        string fileDigits = string.Concat(stem.Where(char.IsDigit));
        if (fileDigits.Length >= this.minTokenLength && !tokens.Contains(fileDigits))
            CollectHitSet(index, fileDigits, hitSets);

        for (int start = 0; start < tokens.Length; start++) {
            for (int length = 2; length <= tokens.Length - start; length++) {
                string concatenated = string.Concat(tokens.Skip(start).Take(length));
                if (concatenated.Length >= this.minTokenLength && concatenated != fileDigits)
                    CollectHitSet(index, concatenated, hitSets);
            }
        }

        if (hitSets.Count < 2)
            return (null, []); // a single evidence source is Bracket 1/2 territory, not intersection

        HashSet<string> intersection = new(hitSets[0].Families, StringComparer.OrdinalIgnoreCase);
        foreach ((_, HashSet<string> hitFamilies) in hitSets.Skip(1))
            intersection.IntersectWith(hitFamilies);

        if (intersection.Count != 1) {
            string tieMatcherName = "NumericMatcher.Bracket2-Intersect";
            List<CandidateSummary> tied = intersection
                .Select(f => new CandidateSummary(f, 1.0, tieMatcherName))
                .ToList();
            return (null, tied);
        }

        string familyId = intersection.First();
        string matcherName = "NumericMatcher.Bracket2-Intersect";

        List<TokenEvidenceItem> tokenEvidence = hitSets
            .Select(hit => new TokenEvidenceItem(hit.Token, hit.Token, FindPostingField(index, hit.Token, familyId), familyId, 1.0))
            .ToList();

        return (new MatchEvidence {
            ImageId = stem,
            SourceFilename = filename,
            FinalFamilyId = familyId,
            FinalScore = 1.0,
            IsKo = false,
            AcceptedMatcherName = matcherName,
            TopCandidates = [new CandidateSummary(familyId, 1.0, matcherName)],
            NumericTokenEvidence = tokenEvidence,
            ImageNgpSummary = BuildNgpSummary(record),
            SafeExplanation = $"Bracket2-Intersect: tokens [{string.Join(", ", hitSets.Select(h => h.Token))}] jointly narrowed to family {familyId}."
        }, []);
    }

    /// <summary>Appends the family hit set for <paramref name="token"/> when the index knows it.</summary>
    private static void CollectHitSet(
        Dictionary<string, List<DigitPosting>> index,
        string token,
        List<(string Token, HashSet<string> Families)> hitSets) {
        if (hitSets.Any(h => h.Token == token) || !index.TryGetValue(token, out List<DigitPosting>? postings))
            return;

        hitSets.Add((token, postings.Select(p => p.FamilyId).ToHashSet(StringComparer.OrdinalIgnoreCase)));
    }

    /// <summary>The source field of the first posting linking <paramref name="token"/> to <paramref name="familyId"/>.</summary>
    private static string FindPostingField(Dictionary<string, List<DigitPosting>> index, string token, string familyId) {
        foreach (DigitPosting posting in index[token]) {
            if (posting.FamilyId.Equals(familyId, StringComparison.OrdinalIgnoreCase))
                return posting.Field;
        }

        return index[token][0].Field;
    }

    //  Substring rescue

    /// <summary>
    /// Attempts substring rescue: accepts the family that every rescue token's containment evidence
    /// agrees on — the intersection of families named by each individual token — rather than trusting
    /// whichever token happens to resolve uniquely first. Runs only for tokens (or the monotoken) of
    /// at least MinSubstringRescueLength digits; disabled when that is 0. A longer welded token (e.g.
    /// reference "87186790" + shot suffix "1" = "871867901") can accidentally resolve to one family
    /// while the shorter, honest token ("87186790") is ambiguous between two — intersecting every
    /// token's family set means the honest token's ambiguity is not silently discarded just because a
    /// welded token happened to narrow to one side of it.
    /// </summary>
    /// <returns>
    /// Accepted MatchEvidence when exactly one family is named by every rescue token that hit at least
    /// one target (tied candidates empty in that case). When no token hits any target, or the
    /// per-token family sets don't intersect to exactly one family, evidence is null and tied
    /// candidates holds the union of every family any rescue token named, for cross-bracket
    /// MATCHES_MULTIPLE_FAMILYIDS attribution.
    /// </returns>
    internal (MatchEvidence? Evidence, List<CandidateSummary> TiedCandidates) TryMatchBySubstringRescue(
        ImageRecord_LAMBDA record,
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules) {
        if (this.substringRescueLength <= 0)
            return (null, []);

        string filename = record.MatchingName;
        string stem = Path.GetFileNameWithoutExtension(filename);
        string[] tokens = GetNumericTokensFromFilename(filename);

        string fileDigits = string.Concat(stem.Where(char.IsDigit));
        string[] rescueTokens = tokens
            .Concat(tokens.Contains(fileDigits) ? Array.Empty<string>() : [fileDigits])
            .Where(t => t.Length >= this.substringRescueLength)
            .OrderByDescending(t => t.Length)
            .ToArray();

        if (rescueTokens.Length == 0)
            return (null, []);

        Dictionary<string, List<DigitPosting>> index = this.GetOrBuildDigitIndex(families, numericRules);
        const string matcherName = "NumericMatcher.SubstringRescue";
        HashSet<string> allNamedFamilies = new(StringComparer.OrdinalIgnoreCase);

        // Per rescue token that hit at least one target: which families it names, plus one (target,
        // field) sample per family for evidence attribution if that token turns out to be the winner.
        var perTokenFamilies = new List<HashSet<string>>();
        Dictionary<string, (string Token, string Target, string Field)> sampleByFamily =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (string token in rescueTokens) {
            HashSet<string> containingFamilies = new(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, List<DigitPosting>> entry in index) {
                if (entry.Key.Length <= token.Length || !entry.Key.Contains(token, StringComparison.Ordinal))
                    continue;

                foreach (DigitPosting posting in entry.Value) {
                    containingFamilies.Add(posting.FamilyId);
                    sampleByFamily.TryAdd(posting.FamilyId, (token, entry.Key, posting.Field));
                }
            }

            if (containingFamilies.Count == 0)
                continue;

            allNamedFamilies.UnionWith(containingFamilies);
            perTokenFamilies.Add(containingFamilies);
        }

        if (perTokenFamilies.Count == 0)
            return (null, []); // no token contained in any target — nothing to report as ambiguous either

        // Any single rescue token that is ambiguous on its own (2+ families) disqualifies the image,
        // full stop — a more "precise" token cannot override it by intersection. Intersecting an
        // ambiguous set {A, B} with a unique set {A} narrows to {A} and would make the ambiguous token
        // vanish from the decision entirely, which is exactly the "871867901" (welds the shot suffix
        // on, resolves uniquely to one EAN by accident) overriding "87186790" (the honest reference,
        // ambiguous between two EANs) bug this fix exists to prevent. So: reject outright if any token
        // alone is ambiguous, before ever comparing tokens against each other.
        if (perTokenFamilies.Any(f => f.Count > 1)) {
            List<CandidateSummary> ambiguousOnOwnToken = allNamedFamilies
                .Select(f => new CandidateSummary(f, 0.9, matcherName))
                .ToList();
            return (null, ambiguousOnOwnToken);
        }

        // Every token that hit something resolved uniquely on its own — now require they all agree.
        HashSet<string> agreeingFamilies = new(perTokenFamilies[0], StringComparer.OrdinalIgnoreCase);
        foreach (HashSet<string> tokenFamilies in perTokenFamilies.Skip(1))
            agreeingFamilies.IntersectWith(tokenFamilies);

        if (agreeingFamilies.Count != 1) {
            // Different tokens each resolved uniquely, but to different families — contradiction.
            List<CandidateSummary> disagreeing = allNamedFamilies
                .Select(f => new CandidateSummary(f, 0.9, matcherName))
                .ToList();
            return (null, disagreeing);
        }

        string winningFamilyId = agreeingFamilies.First();
        (string winningToken, string winningTarget, string winningField) = sampleByFamily[winningFamilyId];

        return (new MatchEvidence {
            ImageId = stem,
            SourceFilename = filename,
            FinalFamilyId = winningFamilyId,
            FinalScore = this.cfg.SubstringRescueConfidence,
            IsKo = false,
            AcceptedMatcherName = matcherName,
            TopCandidates = [new CandidateSummary(winningFamilyId, this.cfg.SubstringRescueConfidence, matcherName)],
            NumericTokenEvidence = [new TokenEvidenceItem(winningToken, winningTarget, winningField, winningFamilyId, this.cfg.SubstringRescueConfidence)],
            ImageNgpSummary = BuildNgpSummary(record),
            SafeExplanation = $"SubstringRescue: token '{winningToken}' is contained in target '{winningTarget}' of family {winningFamilyId}."
        }, []);
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
        IReadOnlyList<MatchingRule> numericRules) {
        string[] tokens = GetNumericTokensFromFilename(filename);
        if (tokens.Length == 0 || candidates.Count <= 1)
            return candidates;

        // The candidate list changes per image (post CLIP filtering), so it must not replace the
        // single-slot cache. The cached index from Brackets 1–2 covers the full family superset and
        // the postings are intersected with `remaining` below, so reusing it is equivalent.
        Dictionary<string, List<DigitPosting>> index =
            this.digitIndex is not null && ReferenceEquals(this.indexedRules, numericRules)
                ? this.digitIndex
                : this.BuildDigitIndex(candidates, numericRules);

        List<FamilyIDRecord> remaining = [.. candidates];

        foreach (string token in tokens) {
            if (!index.TryGetValue(token, out List<DigitPosting>? postings))
                continue;

            HashSet<string> familiesWithToken = postings
                .Select(p => p.FamilyId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            List<FamilyIDRecord> matching = remaining
                .Where(f => familiesWithToken.Contains(f.FamilyID))
                .ToList();

            // Only reduce when the token is discriminating (matches some but not all)
            if (matching.Count > 0 && matching.Count < remaining.Count)
                remaining = matching;
        }

        return remaining;
    }

    //  Digit-target index

    /// <summary>
    /// Builds (once per (families, rules) pair, cached by reference) the inverted index mapping each
    /// family digit target to the family/rule pairs that produce it.
    /// </summary>
    private Dictionary<string, List<DigitPosting>> GetOrBuildDigitIndex(
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules) {
        if (this.digitIndex is not null &&
            ReferenceEquals(this.indexedFamilies, families) &&
            ReferenceEquals(this.indexedRules, numericRules))
            return this.digitIndex;

        this.digitIndex = this.BuildDigitIndex(families, numericRules);
        this.digitTargetsByLength = null;
        this.indexedFamilies = families;
        this.indexedRules = numericRules;
        return this.digitIndex;
    }

    /// <summary>
    /// Raw index builder — one pass over families × rules, plus (when IndexDigitRunsAllColumns) one
    /// pass over every family column indexing each digit run and the capped whole-value digit string.
    /// Targets shorter than MinNumericTokenLength are never indexed.
    /// </summary>
    private Dictionary<string, List<DigitPosting>> BuildDigitIndex(
        IReadOnlyList<FamilyIDRecord> families,
        IReadOnlyList<MatchingRule> numericRules) {
        Dictionary<string, List<DigitPosting>> index = new(StringComparer.Ordinal);
        MatchingRule runRule = this.BuildRunIndexRule(numericRules);

        foreach (FamilyIDRecord family in families) {
            foreach (MatchingRule rule in numericRules) {
                string? target = this.GetFamilyDigitsForField(family, rule.ExcelField);
                if (target is null || target.Length < this.minTokenLength)
                    continue;

                AddPosting(index, target, new DigitPosting(family.FamilyID, rule.ExcelField, rule));
            }

            if (!this.indexAllColumns)
                continue;

            foreach (KeyValuePair<string, string> property in family.CanonicalProperties) {
                string? wholeDigits = DigitsOnly(property.Value);
                if (wholeDigits is not null &&
                    wholeDigits.Length >= this.minTokenLength && wholeDigits.Length <= MaxIndexedWholeValueDigits) {
                    AddPosting(index, wholeDigits, new DigitPosting(family.FamilyID, property.Key, runRule));
                }

                foreach (Match run in DigitSequencePattern.Matches(property.Value)) {
                    if (run.Value.Length >= this.minTokenLength && run.Value != wholeDigits)
                        AddPosting(index, run.Value, new DigitPosting(family.FamilyID, property.Key, runRule));
                }
            }
        }

        return index;
    }

    /// <summary>Appends one posting to the index bucket for <paramref name="target"/>.</summary>
    private static void AddPosting(Dictionary<string, List<DigitPosting>> index, string target, DigitPosting posting) {
        if (!index.TryGetValue(target, out List<DigitPosting>? postings))
            index[target] = postings = [];

        postings.Add(posting);
    }

    /// <summary>
    /// The synthetic rule attached to all-columns run postings: distances copied from the first
    /// configured numeric rule so Bracket 2 TCD gating treats run targets like rule targets.
    /// </summary>
    private MatchingRule BuildRunIndexRule(IReadOnlyList<MatchingRule> numericRules) {
        MatchingRule? first = numericRules.Count > 0 ? numericRules[0] : null;
        return new MatchingRule {
            ExcelField = "*",
            Type = "numeric",
            Strategy = "NumericalMatcher",
            Weight = first?.Weight ?? 1.0,
            MaxDistance = first?.MaxDistance ?? this.cfg.DefaultMaxDistanceFallback,
            MaxDistancePermuted = first?.MaxDistancePermuted ?? 0.0
        };
    }

    /// <summary>Distinct index targets grouped by string length, for the permuted fallback.</summary>
    private Dictionary<int, List<string>> GetTargetsByLength(Dictionary<string, List<DigitPosting>> index) {
        if (this.digitTargetsByLength is not null && ReferenceEquals(index, this.digitIndex))
            return this.digitTargetsByLength;

        Dictionary<int, List<string>> byLength = [];
        foreach (string target in index.Keys) {
            if (!byLength.TryGetValue(target.Length, out List<string>? list))
                byLength[target.Length] = list = [];
            list.Add(target);
        }

        this.digitTargetsByLength = byLength;
        return byLength;
    }

    /// <summary>Per-digit ('0'–'9') character counts across all tokens in the subset.</summary>
    private static int[] CountDigits(string[] tokens) {
        int[] counts = new int[10];
        foreach (string token in tokens)
            foreach (char ch in token)
                if (ch is >= '0' and <= '9') counts[ch - '0']++;
        return counts;
    }

    /// <summary>
    /// True when the target's digit multiset equals the subset's — a permutation concatenation can
    /// only exist when both sides use exactly the same digits.
    /// </summary>
    private static bool DigitCountsMatch(int[] subsetCounts, string target) {
        Span<int> counts = stackalloc int[10];
        foreach (char ch in target)
            if (ch is >= '0' and <= '9') counts[ch - '0']++;

        for (int i = 0; i < DigitHistogramSize; i++)
            if (counts[i] != subsetCounts[i]) return false;
        return true;
    }

    //  Helpers

    /// <summary>
    /// Extracts all digit sequences from the filename stem, preserving left-to-right order.
    /// </summary>
    private static string[] GetNumericTokensFromFilename(string filename) {
        string stem = Path.GetFileNameWithoutExtension(filename);
        return DigitSequencePattern.Matches(stem)
            .Select(m => m.Value)
            .ToArray();
    }

    /// <summary>
    /// Filename digit tokens long enough to act as standalone numeric evidence
    /// (length ≥ MinNumericTokenLength). Shorter runs still participate in concatenations.
    /// </summary>
    private string[] GetEligibleTokens(string filename) =>
        GetNumericTokensFromFilename(filename)
            .Where(t => t.Length >= this.minTokenLength)
            .ToArray();

    /// <summary>
    /// The digits PRISM matches a filename token against for one rule field.
    /// For the FamilyID field this is the intrinsic <see cref="FamilyIDRecord.FamilyID"/>; for any other
    /// field it is the digits of the family's Excel column value (CanonicalProperties).
    /// </summary>
    private string? GetFamilyDigitsForField(FamilyIDRecord family, string excelField) {
        if (excelField.Equals(this.familyIdColumnName, StringComparison.OrdinalIgnoreCase))
            return DigitsOnly(family.FamilyID);

        return GetDigitsOfFamilyExcelColumn(family, excelField);
    }

    /// <summary>
    /// Returns the digits-only value of a family's Excel column (from CanonicalProperties), or null when
    /// the column is absent/empty or contains no digits. Does not handle the FamilyID — that is routed to
    /// the intrinsic identifier by <see cref="GetFamilyDigitsForField"/>.
    /// </summary>
    private static string? GetDigitsOfFamilyExcelColumn(FamilyIDRecord family, string excelField) {
        if (!family.CanonicalProperties.TryGetValue(excelField, out string? value) ||
            string.IsNullOrWhiteSpace(value))
            return null;

        return DigitsOnly(value);
    }

    /// <summary>
    /// Concatenates every digit character in <paramref name="value"/>; returns null when there are none.
    /// </summary>
    private static string? DigitsOnly(string value) {
        string digits = string.Concat(DigitsOnlyPattern.Matches(value).Select(m => m.Value));
        return digits.Length > 0 ? digits : null;
    }

    private static FamilyIDRecord? FindFamily(IReadOnlyList<FamilyIDRecord> families, string familyId) =>
        families.FirstOrDefault(f => f.FamilyID.Equals(familyId, StringComparison.OrdinalIgnoreCase));

    private static string? BuildNgpSummary(ImageRecord_LAMBDA record) =>
        record.SelectedPhenotype is null ? null : $"phenotype={record.SelectedPhenotype}";
}
