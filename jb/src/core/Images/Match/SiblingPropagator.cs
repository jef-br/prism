using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Prism.Core;

/// <summary>
/// Final matching bracket: propagates a matched FamilyID to unmatched images that are shots of the
/// same product. "CARDIGAN_MAGENTA76_A.jpg" carries no resolvable key itself, but its sibling
/// "24211507_CARDIGAN_76_MAGENTA_B.jpg" matched via RefCo — both stems reduce to the same rare
/// token profile {cardigan, magenta}. An unmatched image inherits a FamilyID only when every
/// profile-related matched sibling agrees on one family.
/// </summary>
internal sealed class SiblingPropagator
{
    private static readonly Regex TokenSplitPattern = new(@"[^a-zA-Z0-9]+", RegexOptions.Compiled);

    // Splits mixed tokens at letter↔digit boundaries: "magenta76" → ["magenta", "76"].
    private static readonly Regex AlphaDigitBoundaryPattern = new(
        @"(?<=\d)(?=\D)|(?<=\D)(?=\d)",
        RegexOptions.Compiled);

    // Shot descriptors and copy markers that identify the photo, not the product.
    private static readonly Regex ShotSuffixPattern = new(
        @"^([a-z]|\d{1,3}|[a-z]\d{1,2}|det\d+|ret|retouch(ed)?|copy|front|back|side|top|detail\d*)$",
        RegexOptions.Compiled);

    // A token shared by more than this fraction of the batch is batch noise (brand, collection).
    private const double CommonTokenRatio = 0.5;

    // A token is never treated as batch noise below this many carriers — a product's own shots
    // legitimately share its tokens, and small batches must not have their profiles emptied.
    private const int CommonTokenFloor = 10;

    /// <summary>
    /// Assigns FamilyIDs to unmatched records whose rare-token profile is a subset or superset of
    /// exactly one matched family's profiles. Returns the records still unmatched afterwards.
    /// </summary>
    /// <param name="unmatched">Records without a FamilyID after all earlier brackets.</param>
    /// <param name="allRecords">All LAMBDA records; matched ones provide the propagation sources.</param>
    internal List<ImageRecord_LAMBDA> Run(List<ImageRecord_LAMBDA> unmatched, List<ImageRecord_LAMBDA> allRecords)
    {
        Dictionary<string, int> batchTokenCounts = CountBatchTokens(allRecords);

        List<(ImageRecord_LAMBDA Record, HashSet<string> Profile)> matchedProfiles = [];
        foreach (ImageRecord_LAMBDA record in allRecords)
        {
            if (record.IsKo || record.MatchEvidence?.FinalFamilyId is null)
                continue;

            HashSet<string> profile = BuildProfile(record.InitialFullName ?? string.Empty);
            RemoveBatchCommonTokens(profile, batchTokenCounts, allRecords.Count);

            if (profile.Count > 0)
                matchedProfiles.Add((record, profile));
        }

        if (matchedProfiles.Count == 0)
            return unmatched;

        List<ImageRecord_LAMBDA> stillUnmatched = [];

        foreach (ImageRecord_LAMBDA record in unmatched)
        {
            string filename = record.InitialFullName ?? string.Empty;
            HashSet<string> profile = BuildProfile(filename);
            RemoveBatchCommonTokens(profile, batchTokenCounts, allRecords.Count);

            if (profile.Count == 0)
            {
                stillUnmatched.Add(record);
                continue;
            }

            string? familyId = null;
            string? siblingName = null;
            bool conflicting = false;

            foreach ((ImageRecord_LAMBDA sibling, HashSet<string> siblingProfile) in matchedProfiles)
            {
                if (!ProfilesAreRelated(profile, siblingProfile))
                    continue;

                string siblingFamilyId = sibling.MatchEvidence!.FinalFamilyId!;

                if (familyId is null)
                {
                    familyId = siblingFamilyId;
                    siblingName = Path.GetFileName(sibling.InitialFullName ?? string.Empty);
                }
                else if (!familyId.Equals(siblingFamilyId, StringComparison.OrdinalIgnoreCase))
                {
                    conflicting = true;
                    break;
                }
            }

            if (familyId is null || conflicting)
            {
                stillUnmatched.Add(record);
                continue;
            }

            string stem = Path.GetFileNameWithoutExtension(filename);
            const string matcherName = "SiblingPropagator";

            record.MatchEvidence = new MatchEvidence
            {
                ImageId             = stem,
                SourceFilename      = filename,
                FinalFamilyId       = familyId,
                FinalScore          = 0.9,
                IsKo                = false,
                AcceptedMatcherName = matcherName,
                TopCandidates       = [new CandidateSummary(familyId, 0.9, matcherName)],
                ImageNgpSummary     = record.SelectedPhenotype is null ? null : $"phenotype={record.SelectedPhenotype}",
                SafeExplanation     = $"SiblingPropagation: rare token profile [{string.Join(", ", profile.OrderBy(t => t, StringComparer.Ordinal))}] matches sibling '{siblingName}' of family {familyId}."
            };
        }

        return stillUnmatched;
    }

    /// <summary>
    /// Reduces a filename stem to its rare-token identity profile: lowercased, diacritics stripped,
    /// split on separators and letter↔digit boundaries, shot descriptors and short digit runs removed.
    /// </summary>
    private static HashSet<string> BuildProfile(string filename)
    {
        string stem = NormalizeDiacritics(Path.GetFileNameWithoutExtension(filename).ToLowerInvariant());
        HashSet<string> profile = new(StringComparer.Ordinal);

        foreach (string raw in TokenSplitPattern.Split(stem))
        {
            foreach (string part in AlphaDigitBoundaryPattern.Split(raw))
            {
                if (part.Length < 2 || ShotSuffixPattern.IsMatch(part))
                    continue;

                profile.Add(part);
            }
        }

        return profile;
    }

    /// <summary>Counts how many batch images carry each profile token, for common-token removal.</summary>
    private static Dictionary<string, int> CountBatchTokens(List<ImageRecord_LAMBDA> allRecords)
    {
        Dictionary<string, int> counts = new(StringComparer.Ordinal);

        foreach (ImageRecord_LAMBDA record in allRecords)
        {
            foreach (string token in BuildProfile(record.InitialFullName ?? string.Empty))
            {
                counts[token] = counts.TryGetValue(token, out int count) ? count + 1 : 1;
            }
        }

        return counts;
    }

    /// <summary>
    /// Removes tokens present in more than CommonTokenRatio of the batch (brand/collection noise).
    /// Never removes below CommonTokenFloor carriers, so sibling-shared tokens survive in small batches.
    /// </summary>
    private static void RemoveBatchCommonTokens(HashSet<string> profile, Dictionary<string, int> batchTokenCounts, int batchSize)
    {
        double threshold = Math.Max(CommonTokenFloor, batchSize * CommonTokenRatio);
        profile.RemoveWhere(token =>
            batchTokenCounts.TryGetValue(token, out int count) && count > threshold);
    }

    /// <summary>
    /// True when one profile contains the other and they share enough identity: at least two common
    /// tokens, or one common token of five characters or more (a reference-grade token).
    /// </summary>
    private static bool ProfilesAreRelated(HashSet<string> profile, HashSet<string> siblingProfile)
    {
        if (profile.Count == 0 || siblingProfile.Count == 0)
            return false;

        bool subsetRelated = profile.IsSubsetOf(siblingProfile) || siblingProfile.IsSubsetOf(profile);
        if (!subsetRelated)
            return false;

        int common = profile.Count(siblingProfile.Contains);
        return common >= 2 || profile.Any(t => t.Length >= 5 && siblingProfile.Contains(t));
    }

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
}
