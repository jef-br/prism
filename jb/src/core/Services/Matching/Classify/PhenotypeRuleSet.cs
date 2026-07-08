using System.Globalization;
using System.Text.Json;

namespace Prism.Core;

/// <summary>
/// Loads <c>ImageRoles.json</c> at startup and evaluates phenotype rules against
/// per-image <see cref="ImageFeatureSnapshot"/> measurements.
///
/// Phenotype assignment is always a hard assignment: the first rule whose required
/// conditions are all satisfied is selected. No soft probability vectors.
///
/// Rules can be updated by editing <c>ImageRoles.json</c> — no recompilation needed.
/// </summary>
public sealed class PhenotypeRuleSet
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
    };

    private readonly IReadOnlyList<PhenotypeRule> rules;

    private PhenotypeRuleSet(IReadOnlyList<PhenotypeRule> rules)
    {
        this.rules = rules;
    }

    /// <summary>
    /// Loads and validates phenotype rules from <c>ImageRoles.json</c>.
    /// </summary>
    /// <param name="jsonPath">Absolute path to <c>ImageRoles.json</c>.</param>
    /// <returns>Loaded rule set ready for evaluation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the file is missing or cannot be parsed.</exception>
    public static PhenotypeRuleSet Load(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new InvalidOperationException($"ImageRoles.json not found at: {jsonPath}");

        string json = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);

        ImageRolesConfig config = JsonSerializer.Deserialize<ImageRolesConfig>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize ImageRoles.json at: {jsonPath}");

        return new PhenotypeRuleSet(config.Phenotypes);
    }

    /// <summary>
    /// Hard-assigns the first phenotype whose required conditions are all met.
    /// Returns <c>null</c> when no phenotype matches — the image is unclassified
    /// and handled by deterministic fallback in the Ordered stage.
    /// </summary>
    public string? Assign(ImageFeatureSnapshot features)
    {
        foreach (PhenotypeRule rule in rules)
        {
            if (AllConditionsMet(rule.Required, features))
                return rule.Id;
        }
        return null;
    }

    /// <summary>All phenotype ids in rule evaluation order.</summary>
    public IReadOnlyList<string> PhenotypeIds => rules.Select(r => r.Id).ToArray();

    /// <summary>
    /// Returns true when the phenotype is definitively ruled out by the measured features:
    /// at least one required condition fails on a KNOWN (non-UNKNOWN) feature value.
    /// UNKNOWN features never contradict — absence of evidence keeps the phenotype in play.
    /// Unknown phenotype ids are never contradicted.
    /// </summary>
    public bool IsContradicted(string phenotypeId, ImageFeatureSnapshot features)
    {
        foreach (PhenotypeRule rule in rules)
        {
            if (!string.Equals(rule.Id, phenotypeId, StringComparison.OrdinalIgnoreCase)) continue;
            return rule.Required.Any(c => ConditionDefinitivelyFails(c, features));
        }
        return false;
    }

    /// <summary>
    /// Returns all matching phenotype ids in evaluation order.
    /// Used for diagnostics; only the first match is the selected phenotype.
    /// </summary>
    public string[] EvaluateCandidates(ImageFeatureSnapshot features)
    {
        List<string> candidates = [];
        foreach (PhenotypeRule rule in rules)
        {
            if (AllConditionsMet(rule.Required, features))
                candidates.Add(rule.Id);
        }
        return [.. candidates];
    }

    //  Condition evaluation 

    private static bool AllConditionsMet(IReadOnlyList<FeatureCondition> conditions, ImageFeatureSnapshot features)
    {
        foreach (FeatureCondition condition in conditions)
        {
            if (!ConditionMet(condition, features))
                return false;
        }
        return true;
    }

    // A condition definitively fails only when every feature it touches is known and the
    // comparison still fails. An anyOf group fails only when all of its children do.
    private static bool ConditionDefinitivelyFails(FeatureCondition condition, ImageFeatureSnapshot features)
    {
        if (condition.IsAnyOfGroup)
            return condition.AnyOf!.All(c => ConditionDefinitivelyFails(c, features));

        if (condition.Feature is null)
            return false;

        string value = features.GetValue(condition.Feature);
        if (string.Equals(value, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
            return false;

        return !ConditionMet(condition, features);
    }

    private static bool ConditionMet(FeatureCondition condition, ImageFeatureSnapshot features)
    {
        if (condition.IsAnyOfGroup)
            return condition.AnyOf!.Any(c => ConditionMet(c, features));

        if (condition.Feature is null)
            return false;

        string value = features.GetValue(condition.Feature);

        // UNKNOWN features never satisfy any condition.
        if (string.Equals(value, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
            return false;

        if (condition.EqualTo is not null)
            return string.Equals(value, condition.EqualTo, StringComparison.OrdinalIgnoreCase);

        if (condition.In is not null)
            return condition.In.Any(opt => string.Equals(value, opt, StringComparison.OrdinalIgnoreCase));

        if (condition.Min is not null || condition.Max is not null)
        {
            if (!double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double numValue))
                return false;
            if (condition.Min is not null && numValue < condition.Min.Value) return false;
            if (condition.Max is not null && numValue > condition.Max.Value) return false;
            return true;
        }

        return false;
    }
}
