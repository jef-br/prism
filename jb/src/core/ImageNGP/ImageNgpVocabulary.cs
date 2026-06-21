using System.Globalization;
using System.Text.Json;

namespace Prism.Core;

/// <summary>
/// The canonical PRISM image taxonomy loaded from <c>ImageNGP.json</c>: every feature id with its
/// datatype and allowed values, plus the closed catalogue of phenotype ids.
///
/// This is the single source of truth that <see cref="ImageNgpValidator"/> checks the rule and
/// mapping files (<c>ImageRoles.json</c>, <c>DetOrderRules.json</c>, <c>ClipPrompts.json</c>)
/// against at startup. The taxonomy can change by editing <c>ImageNGP.json</c> — no recompilation.
/// </summary>
public sealed class ImageNgpVocabulary
{
    private readonly Dictionary<string, FeatureDefinition> featuresById;
    private readonly HashSet<string> phenotypeIds;

    private ImageNgpVocabulary(Dictionary<string, FeatureDefinition> featuresById, HashSet<string> phenotypeIds)
    {
        this.featuresById = featuresById;
        this.phenotypeIds = phenotypeIds;
    }

    // ─── Factory ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads and parses <c>ImageNGP.json</c>.
    /// Throws <see cref="PrismConfigurationException"/> on a missing file or bad structure.
    /// </summary>
    /// <param name="jsonPath">Absolute path to ImageNGP.json.</param>
    public static ImageNgpVocabulary Load(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new PrismConfigurationException($"ImageNGP.json not found at: {jsonPath}");

        string json = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);

        JsonDocument doc;
        try {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex) {
            throw new PrismConfigurationException($"Failed to parse ImageNGP.json at '{jsonPath}': {ex.Message}", ex);
        }

        using (doc) {
            var features = ParseFeatures(doc.RootElement, jsonPath);
            var phenotypes = ParsePhenotypes(doc.RootElement, jsonPath);
            return new ImageNgpVocabulary(features, phenotypes);
        }
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>True when the feature id is defined in the taxonomy (case-insensitive).</summary>
    public bool HasFeature(string featureId)
        => featuresById.ContainsKey(featureId);

    /// <summary>True when the phenotype id is in the taxonomy catalogue (case-insensitive).</summary>
    public bool HasPhenotype(string phenotypeId)
        => phenotypeIds.Contains(phenotypeId);

    /// <summary>
    /// True when <paramref name="value"/> is acceptable for the given feature.
    /// enum/boolean features require membership in the allowed values list; integer/float features
    /// require a parseable number; string features accept anything. <c>UNKNOWN</c> is always accepted.
    /// Returns false when the feature id is not defined.
    /// </summary>
    public bool IsAllowedValue(string featureId, string value)
    {
        if (!featuresById.TryGetValue(featureId, out FeatureDefinition? def))
            return false;

        if (string.Equals(value, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
            return true;

        switch (def.Datatype.ToLowerInvariant()) {
            case "enum":
            case "boolean":
                return def.Values.Any(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase));
            case "integer":
                return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
            case "float":
                return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
            default:
                return true;
        }
    }

    // ─── Parsers ──────────────────────────────────────────────────────────────

    private static Dictionary<string, FeatureDefinition> ParseFeatures(JsonElement root, string path)
    {
        if (!root.TryGetProperty("features", out JsonElement featuresEl) || featuresEl.ValueKind != JsonValueKind.Array)
            throw new PrismConfigurationException($"ImageNGP.json at '{path}' is missing required 'features' array.");

        var result = new Dictionary<string, FeatureDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (JsonElement entry in featuresEl.EnumerateArray()) {
            string id = entry.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(id))
                throw new PrismConfigurationException($"ImageNGP.json at '{path}' has a feature entry with no 'id'.");

            string datatype = entry.TryGetProperty("datatype", out JsonElement dtEl) ? dtEl.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(datatype))
                throw new PrismConfigurationException($"ImageNGP.json at '{path}': feature '{id}' has no 'datatype'.");

            List<string> values = [];
            if (entry.TryGetProperty("values", out JsonElement valuesEl) && valuesEl.ValueKind == JsonValueKind.Array) {
                foreach (JsonElement v in valuesEl.EnumerateArray()) {
                    string? s = v.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        values.Add(s);
                }
            }

            if (result.ContainsKey(id))
                throw new PrismConfigurationException($"ImageNGP.json at '{path}': duplicate feature id '{id}'.");
            result[id] = new FeatureDefinition { Id = id, Datatype = datatype, Values = values };
        }

        return result;
    }

    private static HashSet<string> ParsePhenotypes(JsonElement root, string path)
    {
        if (!root.TryGetProperty("phenotypes", out JsonElement phenotypesEl) || phenotypesEl.ValueKind != JsonValueKind.Array)
            throw new PrismConfigurationException($"ImageNGP.json at '{path}' is missing required 'phenotypes' array.");

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (JsonElement p in phenotypesEl.EnumerateArray()) {
            string? id = p.GetString();
            if (!string.IsNullOrWhiteSpace(id))
                result.Add(id);
        }

        return result;
    }
}
