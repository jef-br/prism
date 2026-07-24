using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Prism.Services.Matching;

/// <summary>
/// Last-resort bracket: matches an image to the unique FamilyID whose Excel row names that exact
/// image file in any cell. Many catalogue exports list the image filename directly (e.g.
/// "Chemin de l'image" = "/medias (3)/92836758_det815.jpg", or "Product image #1" =
/// "WB113068-BEIGE32_(1).jpg"), so when the token-based brackets cannot resolve an image this
/// direct link still can. Accepts only an exact, unique filename↔cell match.
/// </summary>
internal sealed class FilenameToCellMatcher
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff", ".bmp", ".gif"
    };

    // filename key (basename-with-extension and stem, lowercased) → FamilyIDs whose cells name it.
    private Dictionary<string, HashSet<string>>? indexByFilenameKey;
    // collapsed stem key (separators and copy suffixes removed) → FamilyIDs; separate namespace so
    // collapsed lookups can never shadow exact ones.
    private Dictionary<string, HashSet<string>>? indexByCollapsedKey;
    private IReadOnlyList<FamilyIDRecord>? indexedFamilies;

    // Copy/retouch markers appended by explorers and DAM exports: " (1)", "-copy", "_RET".
    private static readonly System.Text.RegularExpressions.Regex CopySuffixPattern = new(
        @"(\s*\(\d+\)|[-_ ]+(copy|ret|retouch(ed)?))+$",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>
    /// Attempts to match by locating the image's own filename inside any Excel cell of exactly one family.
    /// </summary>
    /// <returns>
    /// Accepted MatchEvidence when exactly one family names the file (tied candidates empty). When
    /// two or more families name the same file, evidence is null and tied candidates holds every
    /// matched family, for cross-bracket MATCHES_MULTIPLE_FAMILYIDS attribution.
    /// </returns>
    internal (MatchEvidence? Evidence, List<CandidateSummary> TiedCandidates) TryMatch(ImageRecord_LAMBDA record, IReadOnlyList<FamilyIDRecord> families)
    {
        Dictionary<string, HashSet<string>> index = this.GetOrBuildIndex(families);

        string sourceFilename = record.InitialFullName ?? string.Empty;
        string basenameKey = Basename(sourceFilename).Trim().ToLowerInvariant();
        if (basenameKey.Length == 0)
            return (null, []);

        string stemKey = StripExtension(basenameKey);
        string collapsedKey = CollapseStem(stemKey);

        HashSet<string>? matchedFamilies =
            index.TryGetValue(basenameKey, out HashSet<string>? byBasename) ? byBasename
            : index.TryGetValue(stemKey, out HashSet<string>? byStem) ? byStem
            : collapsedKey.Length > 0 && this.indexByCollapsedKey!.TryGetValue(collapsedKey, out HashSet<string>? byCollapsed) ? byCollapsed
            : null;

        if (matchedFamilies is null || matchedFamilies.Count == 0)
            return (null, []); // no match

        const string matcherName = "FilenameToCellMatcher";

        if (matchedFamilies.Count != 1)
        {
            List<CandidateSummary> tied = matchedFamilies
                .Select(f => new CandidateSummary(f, 1.0, matcherName))
                .ToList();
            return (null, tied); // ambiguous, leave for KO
        }

        string familyId = matchedFamilies.First();
        string imageId  = Path.GetFileNameWithoutExtension(sourceFilename);

        return (new MatchEvidence
        {
            ImageId             = imageId,
            SourceFilename      = sourceFilename,
            FinalFamilyId       = familyId,
            FinalScore          = 1.0,
            IsKo                = false,
            AcceptedMatcherName = matcherName,
            TopCandidates       = [new CandidateSummary(familyId, 1.0, matcherName)],
            ImageNgpSummary     = record.SelectedPhenotype is null ? null : $"phenotype={record.SelectedPhenotype}",
            SafeExplanation     = $"FilenameToCell: image filename '{Basename(sourceFilename).Trim()}' is named in an Excel cell of family {familyId}."
        }, []);
    }

    /// <summary>
    /// Builds (once per family set) an index from filename key → FamilyIDs by scanning every original
    /// cell value of every family and keeping only cells whose basename carries an image extension.
    /// </summary>
    private Dictionary<string, HashSet<string>> GetOrBuildIndex(IReadOnlyList<FamilyIDRecord> families)
    {
        if (this.indexByFilenameKey is not null && ReferenceEquals(this.indexedFamilies, families))
            return this.indexByFilenameKey;

        Dictionary<string, HashSet<string>> index = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, HashSet<string>> collapsedIndex = new(StringComparer.OrdinalIgnoreCase);

        foreach (FamilyIDRecord family in families)
        {
            foreach (KeyValuePair<string, IReadOnlyList<string>> property in family.OriginalSourceCellValues)
            {
                foreach (string cellValue in property.Value)
                {
                    if (string.IsNullOrWhiteSpace(cellValue))
                        continue;

                    string basename = Basename(cellValue).Trim().ToLowerInvariant();
                    if (basename.Length == 0 || !ImageExtensions.Contains(Path.GetExtension(basename)))
                        continue;

                    AddKey(index, basename, family.FamilyID);

                    string stem = StripExtension(basename);
                    if (stem.Length > 0)
                        AddKey(index, stem, family.FamilyID);

                    string collapsed = CollapseStem(stem);
                    if (collapsed.Length > 0)
                        AddKey(collapsedIndex, collapsed, family.FamilyID);
                }
            }
        }

        this.indexByFilenameKey = index;
        this.indexByCollapsedKey = collapsedIndex;
        this.indexedFamilies = families;
        return index;
    }

    /// <summary>
    /// Collapses a stem for tolerant comparison: strips copy/retouch suffixes and removes
    /// separator characters, so "92836758_det815 (1)" and "92836758-det815" meet on one key.
    /// </summary>
    private static string CollapseStem(string stem)
    {
        string withoutCopySuffix = CopySuffixPattern.Replace(stem, string.Empty);
        return string.Concat(withoutCopySuffix.Where(ch => ch is not (' ' or '_' or '-' or '.')));
    }

    private static void AddKey(Dictionary<string, HashSet<string>> index, string key, string familyId)
    {
        if (!index.TryGetValue(key, out HashSet<string>? set))
            index[key] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        set.Add(familyId);
    }

    /// <summary>Returns the last path segment, splitting on both '/' and '\' (handles paths and URLs).</summary>
    private static string Basename(string value)
    {
        int separator = value.LastIndexOfAny(['/', '\\']);
        return separator >= 0 ? value[(separator + 1)..] : value;
    }

    private static string StripExtension(string basename)
    {
        int dot = basename.LastIndexOf('.');
        return dot > 0 ? basename[..dot] : basename;
    }
}
