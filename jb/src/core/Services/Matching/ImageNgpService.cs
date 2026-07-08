namespace Prism.Services.Matching;

/// <summary>
/// In-process ImageNGP implementation. Holds the loaded <see cref="PhenotypeRuleSet"/> (from
/// <c>ImageRoles.json</c>) and evaluates it against a feature snapshot. Internal to Matching.
/// </summary>
public sealed class ImageNgpService : IImageNgpService
{
    private readonly PhenotypeRuleSet ruleSet;

    /// <summary>Creates the service over an already-loaded phenotype rule set.</summary>
    public ImageNgpService(PhenotypeRuleSet ruleSet)
        => this.ruleSet = ruleSet ?? throw new ArgumentNullException(nameof(ruleSet));

    /// <inheritdoc/>
    public string[] EvaluateCandidates(ImageFeatureSnapshot features)
        => ruleSet.EvaluateCandidates(features);
}
