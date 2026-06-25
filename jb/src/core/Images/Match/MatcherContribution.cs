namespace Prism.Core;

/// <summary>Weight and confidence contributed by one matcher to a MatchEvidence record.</summary>
public sealed record MatcherContribution(string MatcherName, double Weight, double Confidence);
