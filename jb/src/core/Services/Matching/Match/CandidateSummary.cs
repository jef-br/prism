namespace Prism.Contracts;

/// <summary>One candidate FamilyID considered during matching.</summary>
public sealed record CandidateSummary(
    string FamilyId,
    double Score,
    string MatcherName);
