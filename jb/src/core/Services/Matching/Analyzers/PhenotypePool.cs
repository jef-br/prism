namespace Prism.Services.Matching;

/// <summary>
/// The shrinking set of phenotype candidates for one image during the post-match refinement chain.
/// Starts with every phenotype in ImageRoles.json (at first we know nothing — the image qualifies
/// for all of them) and each <see cref="Eliminate"/> wave removes phenotypes whose required
/// conditions are contradicted by KNOWN feature values. UNKNOWN evidence never eliminates.
/// </summary>
public sealed class PhenotypePool
{
    private readonly PhenotypeRuleSet ruleSet;
    private readonly List<string> candidates;

    public PhenotypePool(PhenotypeRuleSet ruleSet)
    {
        this.ruleSet = ruleSet;
        this.candidates = [.. ruleSet.PhenotypeIds];
    }

    /// <summary>Remaining candidate phenotype ids, in rule evaluation order.</summary>
    public IReadOnlyList<string> Candidates => this.candidates;

    /// <summary>Removes every candidate contradicted by the current feature measurements.</summary>
    public void Eliminate(ImageFeatureSnapshot features)
        => this.candidates.RemoveAll(id => this.ruleSet.IsContradicted(id, features));

    /// <summary>True when the phenotype id is still in the pool.</summary>
    public bool Contains(string phenotypeId)
        => this.candidates.Contains(phenotypeId, StringComparer.OrdinalIgnoreCase);
}
