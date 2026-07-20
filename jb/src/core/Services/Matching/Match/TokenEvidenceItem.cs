namespace Prism.Contracts;

/// <summary>Paired token evidence linking one image token to one Excel family token.</summary>
public sealed record TokenEvidenceItem(
    string FilenameToken,
    string FamilyToken,
    string PropertyName,
    string FamilyId,
    double Score);
