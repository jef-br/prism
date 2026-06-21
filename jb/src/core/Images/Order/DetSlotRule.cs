namespace Prism.Core;

/// <summary>
/// One det slot rule: the zero-based slot index, the ordering keyword, and the ordered phenotype preference list.
/// </summary>
/// <param name="SlotIndex">Zero-based position in the det sequence.</param>
/// <param name="Keyword">Filename hint keyword for this slot (e.g. "front", "back").</param>
/// <param name="Phenotypes">Phenotype ids in preference order (first = most preferred).</param>
public sealed record DetSlotRule(int SlotIndex, string Keyword, IReadOnlyList<string> Phenotypes);
