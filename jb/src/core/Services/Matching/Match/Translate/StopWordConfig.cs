using System.Collections.Generic;

namespace Prism.Services.Matching;

/// <summary>
/// Stop words ignored by string matching while still being available to diagnostics.
/// </summary>
public sealed record StopWordConfig
{
    /// <summary>
    /// Language-neutral common words.
    /// </summary>
    public IReadOnlyList<string> General { get; init; } = [];

    /// <summary>
    /// Domain-specific product words that are too broad to count as evidence.
    /// </summary>
    public IReadOnlyList<string> Domain { get; init; } = [];
}
