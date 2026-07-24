using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Prism.Services.Matching;

/// <summary>
/// Final matching bracket: propagates a matched FamilyID to unmatched images that are shots of the
/// same product. "CARDIGAN_MAGENTA76_A.jpg" carries no resolvable key itself, but its sibling
/// "24211507_CARDIGAN_76_MAGENTA_B.jpg" matched via RefCo — both stems reduce to the same rare
/// token profile {cardigan, magenta}. An unmatched image inherits a FamilyID only when every
/// profile-related matched sibling agrees on one family.
/// </summary>
internal sealed class SiblingPropagator {
    private static readonly Regex TokenSplitPattern = new(@"[^a-zA-Z0-9]+", RegexOptions.Compiled);

    // Splits mixed tokens at letter↔digit boundaries: "magenta76" → ["magenta", "76"].
    private static readonly Regex AlphaDigitBoundaryPattern = new(
        @"(?<=\d)(?=\D)|(?<=\D)(?=\d)",
        RegexOptions.Compiled);

    // Shot descriptors and copy markers that identify the photo, not the product.
    private static readonly Regex ShotSuffixPattern = new(
        @"^([a-z]|\d{1,3}|[a-z]\d{1,2}|det\d+|ret|retouch(ed)?|copy|front|back|side|top|detail\d*)$",
        RegexOptions.Compiled);

    private readonly Config cfg;

    /// <summary>SiblingPropagator's tunables, loaded from MatchingConfig.json's match.siblingPropagator section.</summary>
    public sealed class Config
    {
        /// <summary>A token shared by more than this fraction of the batch is batch noise (brand, collection).</summary>
        public required double CommonTokenRatio { get; init; }

        /// <summary>
        /// A token is never treated as batch noise below this many carriers — a product's own shots
        /// legitimately share its tokens, and small batches must not have their profiles emptied.
        /// </summary>
        public required int CommonTokenFloor { get; init; }

        /// <summary>SiblingPropagation match confidence — empirical calibration, see Match/jbtodo.md.</summary>
        public required double SiblingPropagationConfidence { get; init; }

        /// <summary>Minimum shared rare tokens for two profiles to be considered related.</summary>
        public required int MinCommonTokens { get; init; }

        /// <summary>A single shared token this long or longer counts as reference-grade identity on its own.</summary>
        public required int ReferenceGradeTokenLength { get; init; }
    }

    /// <summary>Creates the sibling propagator.</summary>
    /// <param name="cfg">SiblingPropagator's tunables from MatchingConfig.json's match.siblingPropagator section.</param>
    internal SiblingPropagator(Config cfg) {
        this.cfg = cfg;
    }

    /// <summary>
    /// Assigns FamilyIDs to unmatched records whose rare-token profile is a subset or superset of
    /// exactly one matched family's profiles. Returns the records still unmatched afterwards.
    /// </summary>
    /// <param name="unmatched">Records without a FamilyID after all earlier brackets.</param>
    /// <param name="allRecords">All LAMBDA records; matched ones provide the propagation sources.</param>
    internal List<ImageRecord_LAMBDA> Run(List<ImageRecord_LAMBDA> unmatched, List<ImageRecord_LAMBDA> allRecords) {
        Dictionary<string, int> batchTokenCounts = CountBatchTokens(allRecords);

        List<(ImageRecord_LAMBDA Record, HashSet<string> Profile)> matchedProfiles = [];
        foreach (ImageRecord_LAMBDA record in allRecords) {
            if (record.IsKo || record.MatchEvidence?.FinalFamilyId is null)
                continue;

            HashSet<string> profile = BuildProfile(record.MatchingName);
            RemoveBatchCommonTokens(profile, batchTokenCounts, allRecords.Count);

            if (profile.Count > 0)
                matchedProfiles.Add((record, profile));
        }

        if (matchedProfiles.Count == 0)
            return unmatched;

        // Index matched images by their exact rare-token profile. When several photos of one product
        // share an identical profile (24211507_CARDIGAN_76_MAGENTA_A/_B and CARDIGAN_MAGENTA76_C all
        // reduce to {cardigan, magenta}) and all belong to one family, that profile is a strong,
        // unambiguous product key — the shots of that product. An unmatched photo with the same profile
        // is the next shot of the same product; it joins even if it also loosely overlaps another
        // product. Profiles owned by two families are ambiguous and dropped from this tier.
        Dictionary<string, string> familyByExactProfile = BuildExactProfileOwners(matchedProfiles);

        List<ImageRecord_LAMBDA> stillUnmatched = [];

        foreach (ImageRecord_LAMBDA record in unmatched) {
            HashSet<string> profile = BuildProfile(record.MatchingName);
            RemoveBatchCommonTokens(profile, batchTokenCounts, allRecords.Count);

            if (profile.Count == 0) {
                stillUnmatched.Add(record);
                continue;
            }

            // Tier 1: exact-profile membership — this photo is another shot of a known product set.
            string profileKey = ProfileKey(profile);
            if (familyByExactProfile.TryGetValue(profileKey, out string? exactFamily)) {
                AssignSibling(record, exactFamily, profile, $"same shot set as family {exactFamily}");
                continue;
            }

            // Tier 2: loose relation — subset/superset overlap, refused when related siblings disagree.
            (string? familyId, string? siblingName) = FindLooseRelation(profile, matchedProfiles);
            if (familyId is null) {
                stillUnmatched.Add(record);
                continue;
            }

            AssignSibling(record, familyId, profile, $"matches sibling '{siblingName}' of family {familyId}");
        }

        return stillUnmatched;
    }

    /// <summary>
    /// Maps each exact rare-token profile to the single family that owns it, dropping any profile
    /// carried by matched images of two or more different families (ambiguous, unsafe to propagate).
    /// </summary>
    private static Dictionary<string, string> BuildExactProfileOwners(
        List<(ImageRecord_LAMBDA Record, HashSet<string> Profile)> matchedProfiles) {
        Dictionary<string, string?> owner = new(StringComparer.Ordinal);

        foreach ((ImageRecord_LAMBDA record, HashSet<string> profile) in matchedProfiles) {
            string key = ProfileKey(profile);
            string family = record.MatchEvidence!.FinalFamilyId!;

            if (!owner.TryGetValue(key, out string? current))
                owner[key] = family;
            else if (current is not null && !current.Equals(family, StringComparison.OrdinalIgnoreCase))
                owner[key] = null; // two families share this exact profile → ambiguous
        }

        return owner
            .Where(kv => kv.Value is not null)
            .ToDictionary(kv => kv.Key, kv => kv.Value!, StringComparer.Ordinal);
    }

    /// <summary>
    /// Finds the family of a loosely related matched sibling (subset/superset overlap). Returns null
    /// when nothing relates, or when related siblings belong to different families.
    /// </summary>
    private (string? FamilyId, string? SiblingName) FindLooseRelation(
        HashSet<string> profile,
        List<(ImageRecord_LAMBDA Record, HashSet<string> Profile)> matchedProfiles) {
        string? familyId = null;
        string? siblingName = null;

        foreach ((ImageRecord_LAMBDA sibling, HashSet<string> siblingProfile) in matchedProfiles) {
            if (!ProfilesAreRelated(profile, siblingProfile))
                continue;

            string siblingFamilyId = sibling.MatchEvidence!.FinalFamilyId!;

            if (familyId is null) {
                familyId = siblingFamilyId;
                siblingName = Path.GetFileName(sibling.InitialFullName ?? string.Empty);
            }
            else if (!familyId.Equals(siblingFamilyId, StringComparison.OrdinalIgnoreCase)) {
                return (null, null); // related siblings disagree → refuse
            }
        }

        return (familyId, siblingName);
    }

    /// <summary>Writes the sibling-propagation match evidence onto a record.</summary>
    private void AssignSibling(ImageRecord_LAMBDA record, string familyId, HashSet<string> profile, string reason) {
        string filename = record.MatchingName;
        string stem = Path.GetFileNameWithoutExtension(filename);
        const string matcherName = "SiblingPropagator";

        record.MatchEvidence = new MatchEvidence {
            ImageId = stem,
            SourceFilename = record.InitialFullName ?? filename,
            FinalFamilyId = familyId,
            FinalScore = cfg.SiblingPropagationConfidence,
            IsKo = false,
            AcceptedMatcherName = matcherName,
            TopCandidates = [new CandidateSummary(familyId, cfg.SiblingPropagationConfidence, matcherName)],
            ImageNgpSummary = record.SelectedPhenotype is null ? null : $"phenotype={record.SelectedPhenotype}",
            SafeExplanation = $"SiblingPropagation: rare token profile [{string.Join(", ", profile.OrderBy(t => t, StringComparer.Ordinal))}] {reason}."
        };
    }

    /// <summary>Stable key for an exact rare-token profile (order-independent).</summary>
    private static string ProfileKey(HashSet<string> profile) =>
        string.Join("", profile.OrderBy(t => t, StringComparer.Ordinal));

    /// <summary>
    /// Reduces a filename stem to its rare-token identity profile: lowercased, diacritics stripped,
    /// split on separators and letter↔digit boundaries, shot descriptors and short digit runs removed.
    /// </summary>
    private static HashSet<string> BuildProfile(string filename) {
        string stem = NormalizeDiacritics(Path.GetFileNameWithoutExtension(filename).ToLowerInvariant());
        HashSet<string> profile = new(StringComparer.Ordinal);

        foreach (string raw in TokenSplitPattern.Split(stem)) {
            foreach (string part in AlphaDigitBoundaryPattern.Split(raw)) {
                if (part.Length < 2 || ShotSuffixPattern.IsMatch(part))
                    continue;

                profile.Add(part);
            }
        }

        return profile;
    }

    /// <summary>Counts how many batch images carry each profile token, for common-token removal.</summary>
    private static Dictionary<string, int> CountBatchTokens(List<ImageRecord_LAMBDA> allRecords) {
        Dictionary<string, int> counts = new(StringComparer.Ordinal);

        foreach (ImageRecord_LAMBDA record in allRecords) {
            foreach (string token in BuildProfile(record.MatchingName)) {
                counts[token] = counts.TryGetValue(token, out int count) ? count + 1 : 1;
            }
        }

        return counts;
    }

    /// <summary>
    /// Removes tokens present in more than CommonTokenRatio of the batch (brand/collection noise).
    /// Never removes below CommonTokenFloor carriers, so sibling-shared tokens survive in small batches.
    /// </summary>
    private void RemoveBatchCommonTokens(HashSet<string> profile, Dictionary<string, int> batchTokenCounts, int batchSize) {
        double threshold = Math.Max(cfg.CommonTokenFloor, batchSize * cfg.CommonTokenRatio);
        profile.RemoveWhere(token =>
            batchTokenCounts.TryGetValue(token, out int count) && count > threshold);
    }

    /// <summary>
    /// True when one profile contains the other and they share enough identity: at least two common
    /// tokens, or one common token of five characters or more (a reference-grade token).
    /// </summary>
    private bool ProfilesAreRelated(HashSet<string> profile, HashSet<string> siblingProfile) {
        if (profile.Count == 0 || siblingProfile.Count == 0)
            return false;

        bool subsetRelated = profile.IsSubsetOf(siblingProfile) || siblingProfile.IsSubsetOf(profile);
        if (!subsetRelated)
            return false;

        int common = profile.Count(siblingProfile.Contains);
        return common >= cfg.MinCommonTokens || profile.Any(t => t.Length >= cfg.ReferenceGradeTokenLength && siblingProfile.Contains(t));
    }

    private static string NormalizeDiacritics(string input) {
        string decomposed = input.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(decomposed.Length);

        foreach (char ch in decomposed) {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
