using System.Collections.Generic;

namespace Prism.Contracts;

/// <summary>
/// Evidence retained when duplicate rows or columns disagree.
/// </summary>
public sealed record FamilyConflictEvidence(
    string FamilyID,
    string PropertyName,
    string ReasonCode,
    IReadOnlyList<string> SourceValues,
    IReadOnlyList<string> NormalizedTokens,
    IReadOnlyList<ExcelCellAddress> SourceLocations);
