namespace Prism.Contracts;

/// <summary>One CLIP classification label matched against a MatchingConfig label rule.</summary>
public sealed record LabelEvidenceItem(
    string Label,
    string PropertyName,
    string FamilyId,
    double Weight,
    double Confidence);
