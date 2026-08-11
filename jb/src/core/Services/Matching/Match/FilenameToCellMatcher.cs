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
internal sealed class FilenameToCellMatcher {
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

    // Splits a free-text cell into candidate filename tokens: whitespace, commas, and semicolons —
    // the common separators in a "here are the pictures: a.jpg, b.jpg" marketing-description cell.
    private static readonly System.Text.RegularExpressions.Regex CellTokenSplitPattern = new(
        @"[\s,;]+",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Attempts to match by locating the image's own filename inside any Excel cell of exactly one family.
    /// </summary>
    /// <returns>
    /// Accepted MatchEvidence when exactly one family names the file (tied candidates empty). When
    /// two or more families name the same file, evidence is null and tied candidates holds every
    /// matched family, for cross-bracket MATCHES_MULTIPLE_FAMILYIDS attribution.
    /// </returns>
    internal (MatchEvidence? Evidence, List<CandidateSummary> TiedCandidates) TryMatch(ImageRecord_LAMBDA record, IReadOnlyList<FamilyIDRecord> families) {
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
            : collapsedKey.Length > 0 ? this.FindByCollapsedPrefix(collapsedKey)
            : null;

        if (matchedFamilies is null || matchedFamilies.Count == 0)
            return (null, []); // no match

        const string matcherName = "FilenameToCellMatcher";

        if (matchedFamilies.Count != 1) {
            List<CandidateSummary> tied = matchedFamilies
                .Select(f => new CandidateSummary(f, 1.0, matcherName))
                .ToList();
            return (null, tied); // ambiguous, leave for KO
        }

        string familyId = matchedFamilies.First();
        string imageId = Path.GetFileNameWithoutExtension(sourceFilename);

        return (new MatchEvidence {
            ImageId = imageId,
            SourceFilename = sourceFilename,
            FinalFamilyId = familyId,
            FinalScore = 1.0,
            IsKo = false,
            AcceptedMatcherName = matcherName,
            TopCandidates = [new CandidateSummary(familyId, 1.0, matcherName)],
            ImageNgpSummary = record.SelectedPhenotype is null ? null : $"phenotype={record.SelectedPhenotype}",
            SafeExplanation = $"FilenameToCell: image filename '{Basename(sourceFilename).Trim()}' is named in an Excel cell of family {familyId}."
        }, []);
    }

    /// <summary>
    /// Fallback when no collapsed key equals the image's collapsed stem exactly: a cell can name a
    /// file's base reference without whatever the real file's own name adds afterward (e.g. cell lists
    /// "100267_6.jpg", the real file is "100267_6  - BW001_c.jpg" — the extra " - BW001_c" is not in
    /// any Excel cell). Finds every indexed collapsed key that is a prefix of the image's collapsed
    /// stem and unions their families — the same "exactly one family" uniqueness gate the caller
    /// already applies to exact/collapsed matches is what keeps this safe, not a score.
    /// </summary>
    private HashSet<string>? FindByCollapsedPrefix(string collapsedKey) {
        HashSet<string>? matched = null;

        foreach (KeyValuePair<string, HashSet<string>> entry in this.indexByCollapsedKey!) {
            if (entry.Key.Length == 0 || !collapsedKey.StartsWith(entry.Key, StringComparison.Ordinal))
                continue;

            matched ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            matched.UnionWith(entry.Value);
        }

        return matched;
    }

    /// <summary>
    /// Builds (once per family set) an index from filename key → FamilyIDs by scanning every original
    /// cell value of every family for filename-shaped tokens — not just cells whose entire value is a
    /// bare path. A free-text cell ("Pictures are here: a.jpg, b.jpg, c") gets split on whitespace/
    /// commas/semicolons first, then each piece is treated as its own candidate path.
    /// </summary>
    private Dictionary<string, HashSet<string>> GetOrBuildIndex(IReadOnlyList<FamilyIDRecord> families) {
        if (this.indexByFilenameKey is not null && ReferenceEquals(this.indexedFamilies, families))
            return this.indexByFilenameKey;

        Dictionary<string, HashSet<string>> index = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, HashSet<string>> collapsedIndex = new(StringComparer.OrdinalIgnoreCase);

        foreach (FamilyIDRecord family in families) {
            foreach (KeyValuePair<string, IReadOnlyList<string>> property in family.OriginalSourceCellValues) {
                foreach (string cellValue in property.Value) {
                    if (string.IsNullOrWhiteSpace(cellValue))
                        continue;

                    string[] pieces = CellTokenSplitPattern.Split(cellValue);

                    // Extension-less pieces only count in a cell that ALSO lists at least one
                    // extensioned image path — that context is what makes "100267_7" (last item in a
                    // cell otherwise full of "….jpg" URLs) a filename reference rather than a bare SKU
                    // cell like "AB12" (no sibling with an image extension anywhere in that cell).
                    bool cellHasImageExtensionPiece = pieces.Any(p =>
                        ImageExtensions.Contains(Path.GetExtension(Basename(p).Trim().ToLowerInvariant())));

                    foreach (string piece in pieces)
                        IndexCellToken(index, collapsedIndex, piece, family.FamilyID, cellHasImageExtensionPiece);
                }
            }
        }

        this.indexByFilenameKey = index;
        this.indexByCollapsedKey = collapsedIndex;
        this.indexedFamilies = families;
        return index;
    }

    /// <summary>
    /// Indexes one whitespace/comma-split piece of a cell value as a candidate filename, when it looks
    /// filename-shaped: carrying a recognized image extension (the common case), or — since a cell can
    /// list a filename with its extension trimmed off, as `expected-match.json`'s `100267_7` row pins —
    /// carrying a digit AND sharing its cell with at least one piece that DOES carry an image
    /// extension. That second condition is load-bearing: without it, a bare SKU cell like "AB12" (no
    /// sibling with an image extension anywhere in that cell) would index and false-match "AB12.jpg",
    /// which is exactly the case <c>TryMatch_NonImageCellEqualToStem_DoesNotFalseMatch</c> guards. A
    /// bare alphabetic word ("here", "Pictures") is never indexed either way; the "exactly one family
    /// names it" uniqueness check at lookup time is the real guardrail against a coincidental token
    /// collision, not this shape filter.
    /// </summary>
    private static void IndexCellToken(
        Dictionary<string, HashSet<string>> index,
        Dictionary<string, HashSet<string>> collapsedIndex,
        string piece,
        string familyId,
        bool cellHasImageExtensionPiece) {
        string basename = Basename(piece).Trim().ToLowerInvariant();
        if (basename.Length == 0)
            return;

        bool hasImageExtension = ImageExtensions.Contains(Path.GetExtension(basename));
        bool extensionLessButInFilenameCell = !hasImageExtension && cellHasImageExtensionPiece && basename.Any(char.IsDigit);
        if (!hasImageExtension && !extensionLessButInFilenameCell)
            return;

        if (hasImageExtension)
            AddKey(index, basename, familyId);

        string stem = hasImageExtension ? StripExtension(basename) : basename;
        if (stem.Length > 0)
            AddKey(index, stem, familyId);

        string collapsed = CollapseStem(stem);
        if (collapsed.Length > 0)
            AddKey(collapsedIndex, collapsed, familyId);
    }

    /// <summary>
    /// Collapses a stem for tolerant comparison: strips copy/retouch suffixes and removes
    /// separator characters, so "92836758_det815 (1)" and "92836758-det815" meet on one key.
    /// </summary>
    private static string CollapseStem(string stem) {
        string withoutCopySuffix = CopySuffixPattern.Replace(stem, string.Empty);
        return string.Concat(withoutCopySuffix.Where(ch => ch is not (' ' or '_' or '-' or '.')));
    }

    private static void AddKey(Dictionary<string, HashSet<string>> index, string key, string familyId) {
        if (!index.TryGetValue(key, out HashSet<string>? set))
            index[key] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        set.Add(familyId);
    }

    /// <summary>Returns the last path segment, splitting on both '/' and '\' (handles paths and URLs).</summary>
    private static string Basename(string value) {
        int separator = value.LastIndexOfAny(['/', '\\']);
        return separator >= 0 ? value[(separator + 1)..] : value;
    }

    private static string StripExtension(string basename) {
        int dot = basename.LastIndexOf('.');
        return dot > 0 ? basename[..dot] : basename;
    }
}
