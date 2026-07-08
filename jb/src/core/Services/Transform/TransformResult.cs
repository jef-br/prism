namespace Prism.Core;

/// <summary>
/// What the Transform service hands forward: the LAMBDA collection enriched in place with each image's
/// <c>TransformationResult</c>. Carries the originating <see cref="MatchingResult"/> forward (which in turn
/// carries Ingest) so the Export step can still reach normalized images, counts, and warnings.
/// </summary>
public sealed record TransformResult
{
    /// <summary>The matching output whose LAMBDA records were transformed in place.</summary>
    public required MatchingResult Matched { get; init; }

    /// <summary>Non-KO images that received a transform decision (0 when Transform is disabled).</summary>
    public int OkTransformedCount { get; init; }
}
