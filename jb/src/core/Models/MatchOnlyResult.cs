namespace Prism.Contracts;

/// <summary>
/// Result returned by the match-only routes: a mapping from each submitted filename to the computed
/// output name (<c>{FamilyID}_det{DetOrder}.jpg</c>), or null when the image could not be matched.
/// </summary>
public sealed record MatchOnlyResult {
    /// <summary>
    /// One entry per submitted image. Value is the computed output filename, or null when the image
    /// was KO'd or could not be matched to any FamilyID.
    /// </summary>
    public required IReadOnlyDictionary<string, string?> FileNameMap { get; init; }

    /// <summary>Number of images successfully matched and ordered.</summary>
    public int Matched { get; init; }

    /// <summary>Number of images that could not be matched (KO or no candidate found).</summary>
    public int Unmatched { get; init; }
}
