/// <summary>
/// A qualifying (image, det-slot) pair built during det-order candidate evaluation.
/// </summary>
internal sealed record Candidate(
    int ImageIndex,
    int DetSlot,
    int PhenotypeRank,
    int NgpConfidence,
    int HintScore,
    int SourceIndex,
    string Phenotype);
