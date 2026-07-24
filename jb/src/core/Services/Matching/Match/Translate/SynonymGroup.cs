using System.Collections.Generic;
using System.Linq;

namespace Prism.Services.Matching;

/// <summary>
/// A configured set of product terms that are equivalent for matching.
/// </summary>
public sealed record SynonymGroup
{
    /// <summary>
    /// Stable identifier for the synonym group.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// The product attribute area this group belongs to, such as color, material, or productType.
    /// </summary>
    public string Domain { get; init; } = string.Empty;

    /// <summary>
    /// All words that should be treated as equivalent inside this group.
    /// </summary>
    public IReadOnlyList<string> Terms { get; init; } = [];

    /// <summary>
    /// Checks whether both normalized tokens are present in this group.
    /// </summary>
    /// <param name="normalizedLeftToken">The first normalized token.</param>
    /// <param name="normalizedRightToken">The second normalized token.</param>
    /// <returns>True when both tokens belong to this synonym group.</returns>
    public bool ContainsBoth(string normalizedLeftToken, string normalizedRightToken)
    {
        bool hasLeftToken = this.Terms.Any(term => term.Trim().ToLowerInvariant() == normalizedLeftToken);
        bool hasRightToken = this.Terms.Any(term => term.Trim().ToLowerInvariant() == normalizedRightToken);

        return hasLeftToken && hasRightToken;
    }
}
