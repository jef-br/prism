using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Prism.Services.Matching;

/// <summary>
/// Gives meaningless image filenames (<c>1.jpg</c>, <c>DSCN2365.jpg</c>, <c>IMG_10005.png</c>) a
/// matchable name by borrowing their folder's name — but only when that folder actually carries
/// meaning. A folder is meaningful when its siblings at the same depth follow the same pattern (one
/// folder per product, not a handful of format buckets) and at least one token of the folder name
/// appears in the Excel data. Pure format folders (<c>HD</c>, <c>Web</c>, <c>packshot</c>,
/// <c>800 x 1200</c>) are rejected. The borrowed name is written to
/// <see cref="ImageRecord_LAMBDA.MatchingAlias"/>; the real filename is left untouched, so the
/// filename-in-cell matcher still sees the original.
/// </summary>
internal sealed class FolderNameEnricher {
    private static readonly Regex TokenSplitPattern = new(@"[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly Regex AlphaDigitBoundaryPattern = new(@"(?<=\d)(?=\D)|(?<=\D)(?=\d)", RegexOptions.Compiled);
    private static readonly Regex DimensionPattern = new(@"^\d{2,5}\s*[x×]\s*\d{2,5}$", RegexOptions.Compiled);

    private readonly Config cfg;
    private readonly HashSet<string> cameraPrefixes;
    private readonly HashSet<string> noiseFolderTokens;
    private readonly int minMeaningfulTokenLength;

    /// <summary>FolderNameEnricher's tunables, loaded from MatchingConfig.json's match.folderNameEnricher section.</summary>
    public sealed class Config {
        /// <summary>Camera/scanner filename prefixes that carry no product meaning.</summary>
        public required IReadOnlyList<string> CameraPrefixes { get; init; }

        /// <summary>Folder names (and folder tokens) that describe format/quality, not the product.</summary>
        public required IReadOnlyList<string> NoiseFolderTokens { get; init; }

        /// <summary>
        /// A bare (all-digit) folder token counts as meaning only when at least this long — short numbers
        /// are sequence indices; long ones are product numbers or references.
        /// </summary>
        public required int MinBareNumberLength { get; init; }

        /// <summary>Floor applied to MinMeaningfulTokenLength.</summary>
        public required int MinTokenLengthFloor { get; init; }

        /// <summary>Minimum non-noise sibling folders for the parent to count as a per-item pattern.</summary>
        public required int MinPerItemSiblings { get; init; }

        /// <summary>Shortest folder token that may count as Excel-relevant meaning.</summary>
        public required int MinMeaningfulTokenLength { get; init; }
    }

    /// <summary>Creates the enricher.</summary>
    /// <param name="cfg">FolderNameEnricher's tunables from MatchingConfig.json's match.folderNameEnricher section.</param>
    internal FolderNameEnricher(Config cfg) {
        this.cfg = cfg;
        this.cameraPrefixes = new HashSet<string>(cfg.CameraPrefixes, StringComparer.Ordinal);
        this.noiseFolderTokens = new HashSet<string>(cfg.NoiseFolderTokens, StringComparer.Ordinal);
        this.minMeaningfulTokenLength = Math.Max(cfg.MinTokenLengthFloor, cfg.MinMeaningfulTokenLength);
    }

    /// <summary>
    /// Assigns <see cref="ImageRecord_LAMBDA.MatchingAlias"/> to every record whose filename is
    /// meaningless and whose folder is meaningful with respect to the Excel model.
    /// </summary>
    internal void Enrich(IReadOnlyList<ImageRecord_LAMBDA> records, IReadOnlyList<FamilyIDRecord> families) {
        List<ImageRecord_LAMBDA> meaningless = records
            .Where(r => !r.IsKo && r.MatchingAlias is null && this.FilenameIsMeaningless(r.InitialFullName))
            .ToList();

        if (meaningless.Count == 0)
            return;

        HashSet<string> excelTokens = this.BuildExcelTokenVocabulary(families);
        if (excelTokens.Count == 0)
            return;

        // Group the meaningless images by their immediate folder, and record the sibling folders that
        // sit at the same depth (same parent) — the shared-pattern test needs the sibling set.
        Dictionary<string, List<string>> foldersByParent = MapSiblingFolders(records);

        foreach (ImageRecord_LAMBDA record in meaningless) {
            string? folderPath = GetFolderPath(record.InitialFullName);
            if (folderPath is null)
                continue;

            string folderName = LastSegment(folderPath);
            string parentPath = ParentPath(folderPath);

            IReadOnlyList<string> siblings = foldersByParent.TryGetValue(parentPath, out List<string>? s) ? s : [folderName];

            if (!this.FolderIsMeaningful(folderName, siblings, excelTokens))
                continue;

            string filename = Path.GetFileName(record.InitialFullName);
            record.MatchingAlias = $"{folderName} {filename}";
        }
    }

    //  Filename meaninglessness

    /// <summary>
    /// True when the filename stem has no product-bearing content: after removing digits and camera
    /// prefixes, no alphabetic token of at least 3 characters remains.
    /// </summary>
    private bool FilenameIsMeaningless(string fullName) {
        string stem = Path.GetFileNameWithoutExtension(fullName).ToLowerInvariant();

        foreach (string raw in TokenSplitPattern.Split(stem)) {
            foreach (string token in AlphaDigitBoundaryPattern.Split(raw)) {
                if (token.Length < 3 || token.All(char.IsDigit))
                    continue;

                if (!this.cameraPrefixes.Contains(token))
                    return false; // a real word survives → the filename is meaningful, leave it alone
            }
        }

        return true;
    }

    //  Folder meaningfulness

    /// <summary>
    /// True when the folder name is not pure format noise, its siblings form a per-item pattern (more
    /// than one non-noise sibling), and at least one folder token appears in the Excel vocabulary.
    /// </summary>
    private bool FolderIsMeaningful(string folderName, IReadOnlyList<string> siblings, HashSet<string> excelTokens) {
        List<string> folderTokens = this.MeaningfulTokens(folderName);
        if (folderTokens.Count == 0)
            return false; // folder is entirely noise (HD, Web, 800x1200, …)

        // Sibling pattern: the parent must hold several per-item folders, not two or three format
        // buckets. A lone product folder, or a set of only-noise folders, does not qualify.
        int perItemSiblings = siblings.Count(sib => this.MeaningfulTokens(sib).Count > 0);
        if (perItemSiblings < this.cfg.MinPerItemSiblings)
            return false;

        // Excel relevance: at least one meaningful folder token must appear in the Excel data.
        return folderTokens.Any(excelTokens.Contains);
    }

    /// <summary>
    /// The meaningful tokens of a folder name: alphanumeric pieces (also split at letter↔digit
    /// boundaries) that are not format-noise words and not pure short numbers, plus dimension folders
    /// (<c>800 x 1200</c>) treated as wholly noise.
    /// </summary>
    private List<string> MeaningfulTokens(string folderName) {
        string normalized = NormalizeDiacritics(folderName.Trim().ToLowerInvariant());

        if (DimensionPattern.IsMatch(normalized))
            return [];

        List<string> tokens = [];
        foreach (string raw in TokenSplitPattern.Split(normalized)) {
            if (raw.Length == 0)
                continue;

            // Keep mixed alphanumeric codes whole (SH23005) — they are strong product keys.
            bool hasLetter = raw.Any(char.IsLetter);
            bool hasDigit = raw.Any(char.IsDigit);

            if (hasLetter && hasDigit && raw.Length >= this.minMeaningfulTokenLength && !this.noiseFolderTokens.Contains(raw)) {
                tokens.Add(raw);
                continue;
            }

            foreach (string piece in AlphaDigitBoundaryPattern.Split(raw)) {
                if (piece.Length < this.minMeaningfulTokenLength)
                    continue;
                if (this.noiseFolderTokens.Contains(piece))
                    continue;
                // A short bare number (a sequence index like "800" or "3") is not folder meaning, but a
                // long bare number is a product number / reference and is one of the strongest keys.
                if (piece.All(char.IsDigit) && piece.Length < this.cfg.MinBareNumberLength)
                    continue;

                tokens.Add(piece);
            }
        }

        return tokens;
    }

    //  Excel vocabulary

    /// <summary>
    /// Every normalized token (and letter↔digit split) that appears in any family's cell values,
    /// so folder tokens can be tested for Excel relevance in O(1).
    /// </summary>
    private HashSet<string> BuildExcelTokenVocabulary(IReadOnlyList<FamilyIDRecord> families) {
        HashSet<string> vocabulary = new(StringComparer.Ordinal);

        foreach (FamilyIDRecord family in families) {
            this.AddToken(vocabulary, family.FamilyID.ToLowerInvariant());

            foreach (string value in family.CanonicalProperties.Values) {
                foreach (string raw in TokenSplitPattern.Split(NormalizeDiacritics(value.ToLowerInvariant()))) {
                    this.AddToken(vocabulary, raw);
                    foreach (string piece in AlphaDigitBoundaryPattern.Split(raw))
                        this.AddToken(vocabulary, piece);
                }
            }
        }

        return vocabulary;
    }

    private void AddToken(HashSet<string> vocabulary, string token) {
        if (token.Length >= this.minMeaningfulTokenLength && !this.noiseFolderTokens.Contains(token))
            vocabulary.Add(token);
    }

    //  Path and sibling helpers

    /// <summary>Maps each parent folder path to the distinct immediate child folder names beneath it.</summary>
    private static Dictionary<string, List<string>> MapSiblingFolders(IReadOnlyList<ImageRecord_LAMBDA> records) {
        Dictionary<string, HashSet<string>> childrenByParent = new(StringComparer.OrdinalIgnoreCase);

        foreach (ImageRecord_LAMBDA record in records) {
            string? folderPath = GetFolderPath(record.InitialFullName);
            if (folderPath is null)
                continue;

            string parent = ParentPath(folderPath);
            string child = LastSegment(folderPath);

            if (!childrenByParent.TryGetValue(parent, out HashSet<string>? set))
                childrenByParent[parent] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            set.Add(child);
        }

        return childrenByParent.ToDictionary(kv => kv.Key, kv => kv.Value.ToList(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The directory portion of a path, or null when the name carries no folder.</summary>
    private static string? GetFolderPath(string fullName) {
        string normalized = fullName.Replace('\\', '/');
        int slash = normalized.LastIndexOf('/');
        return slash > 0 ? normalized[..slash] : null;
    }

    private static string LastSegment(string path) {
        string trimmed = path.TrimEnd('/');
        int slash = trimmed.LastIndexOf('/');
        return slash >= 0 ? trimmed[(slash + 1)..] : trimmed;
    }

    private static string ParentPath(string path) {
        string trimmed = path.TrimEnd('/');
        int slash = trimmed.LastIndexOf('/');
        return slash >= 0 ? trimmed[..slash] : string.Empty;
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
