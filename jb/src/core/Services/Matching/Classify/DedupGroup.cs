namespace Prism.Services.Matching;

/// <summary>
/// A visual deduplication group with one canonical image and zero or more duplicates.
/// </summary>
public sealed record DedupGroup(
    ImageRecord_INPUT Canonical,
    IReadOnlyList<ImageRecord_INPUT> Duplicates);
