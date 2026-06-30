using System.Collections.Generic;
using System.Linq;

namespace Prism.Core;

/// <summary>
/// A configured set of Excel header terms that all denote the same canonical column
/// (e.g. "color", "colour", "couleur", "kleur", "farbe", "colore"). Used by header-row
/// detection and FamilyID-column resolution; never consulted during value matching.
/// </summary>
public sealed record HeaderGroup
{
    /// <summary>
    /// Canonical English header id this group resolves to (e.g. "familyid", "ean", "color").
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// All single-token header terms, across languages, that map to this canonical id.
    /// Terms are stored ASCII-folded and lowercase so they compare against folded header tokens.
    /// </summary>
    public IReadOnlyList<string> Terms { get; init; } = [];

    /// <summary>
    /// Checks whether a normalized header token is one of this group's terms.
    /// </summary>
    /// <param name="normalizedToken">A lowercase, diacritics-folded header token.</param>
    /// <returns>True when the token is configured for this group.</returns>
    public bool ContainsTerm(string normalizedToken)
    {
        return Terms.Any(term => term.Trim().ToLowerInvariant() == normalizedToken);
    }
}
