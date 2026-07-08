namespace Prism.Services.Matching;

/// <summary>
/// Internal to Matching — not visible to the orchestrator. The fan-in step: evaluates the phenotype
/// rules in <c>ImageRoles.json</c> against the measured features (FeatureAnalysis + Classification
/// outputs) and returns the qualifying phenotype ids in evaluation order.
/// </summary>
public interface IImageNgpService
{
    /// <summary>Returns all phenotype ids whose rules matched, best candidate first; empty when none matched.</summary>
    string[] EvaluateCandidates(ImageFeatureSnapshot features);
}
