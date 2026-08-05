namespace Prism.Services.Matching;

/// <summary>
/// Intermediate result of assigning one image to a det slot during ordering.
/// </summary>
internal sealed record AssignmentRecord(
    int DetSlot,
    double AxisPosition,
    string? WinningPhenotype,
    int PhenotypeRank,
    int NgpConfidence,
    string TieBreakerWon,
    bool IsOverflow);
