using System.Text.Json;

namespace Prism.Core;

/// <summary>
/// Startup cross-file validator. Checks that every feature id, feature value, and phenotype id used
/// in <c>ImageRoles.json</c>, <c>DetOrderRules.json</c>, and <c>ClipPrompts.json</c> is defined in the
/// canonical <c>ImageNGP.json</c> taxonomy. Collects every problem and throws a single
/// <see cref="PrismConfigurationException"/> — fail fast and loud, all issues at once.
///
/// This is what makes the taxonomy safe to edit by hand: a typo (e.g. <c>hero-orientaton</c>) is
/// caught at startup instead of silently evaluating to UNKNOWN and never matching at runtime.
/// </summary>
public static class ImageNgpValidator
{
    /// <summary>
    /// Validates the rule and mapping files in <paramref name="coreConfigDirectory"/> against the
    /// canonical taxonomy. Throws <see cref="PrismConfigurationException"/> listing all problems.
    /// </summary>
    /// <param name="coreConfigDirectory">Directory containing Prism_Config.json (the core config root).</param>
    public static void Validate(string coreConfigDirectory)
    {
        string vocabularyPath = Path.Combine(coreConfigDirectory, "ImageNGP.json");
        string imageRolesPath = Path.Combine(coreConfigDirectory, "ImageRoles.json");
        string detOrderPath   = Path.Combine(coreConfigDirectory, "DetOrderRules.json");
        string clipPromptsPath = Path.Combine(coreConfigDirectory, "ClipPrompts.json");

        ImageNgpVocabulary vocabulary = ImageNgpVocabulary.Load(vocabularyPath);

        List<string> problems = [];
        ValidateImageRoles(imageRolesPath, vocabulary, problems);
        ValidateDetOrderRules(detOrderPath, vocabulary, problems);
        ValidateClipPrompts(clipPromptsPath, vocabulary, problems);

        if (problems.Count > 0) {
            throw new PrismConfigurationException(
                $"ImageNGP taxonomy validation failed with {problems.Count} problem(s):{Environment.NewLine}" +
                string.Join(Environment.NewLine, problems.Select(p => "  - " + p)));
        }
    }

    //  ImageRoles.json 

    private static void ValidateImageRoles(string path, ImageNgpVocabulary vocabulary, List<string> problems)
    {
        JsonElement root = LoadRoot(path, problems);
        if (root.ValueKind == JsonValueKind.Undefined) return;

        if (!root.TryGetProperty("phenotypes", out JsonElement phenotypesEl) || phenotypesEl.ValueKind != JsonValueKind.Array) {
            problems.Add($"ImageRoles.json is missing a 'phenotypes' array.");
            return;
        }

        foreach (JsonElement rule in phenotypesEl.EnumerateArray()) {
            string id = rule.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(id))
                problems.Add("ImageRoles.json has a phenotype rule with no 'id'.");
            else if (!vocabulary.HasPhenotype(id))
                problems.Add($"ImageRoles.json: phenotype id '{id}' is not in the ImageNGP.json catalogue.");

            if (rule.TryGetProperty("required", out JsonElement requiredEl) && requiredEl.ValueKind == JsonValueKind.Array) {
                foreach (JsonElement condition in requiredEl.EnumerateArray())
                    ValidateCondition(condition, id, vocabulary, problems);
            }
        }
    }

    private static void ValidateCondition(JsonElement condition, string phenotypeId, ImageNgpVocabulary vocabulary, List<string> problems)
    {
        // OR group: recurse into each child condition.
        if (condition.TryGetProperty("anyOf", out JsonElement anyOfEl) && anyOfEl.ValueKind == JsonValueKind.Array) {
            foreach (JsonElement child in anyOfEl.EnumerateArray())
                ValidateCondition(child, phenotypeId, vocabulary, problems);
            return;
        }

        if (!condition.TryGetProperty("feature", out JsonElement featureEl)) return;
        string feature = featureEl.GetString() ?? "";

        if (!vocabulary.HasFeature(feature)) {
            problems.Add($"ImageRoles.json (phenotype '{phenotypeId}'): unknown feature '{feature}'.");
            return;
        }

        if (condition.TryGetProperty("equals", out JsonElement equalsEl)) {
            string value = equalsEl.GetString() ?? "";
            if (!vocabulary.IsAllowedValue(feature, value))
                problems.Add($"ImageRoles.json (phenotype '{phenotypeId}'): value '{value}' is not valid for feature '{feature}'.");
        }

        if (condition.TryGetProperty("in", out JsonElement inEl) && inEl.ValueKind == JsonValueKind.Array) {
            foreach (JsonElement opt in inEl.EnumerateArray()) {
                string value = opt.GetString() ?? "";
                if (!vocabulary.IsAllowedValue(feature, value))
                    problems.Add($"ImageRoles.json (phenotype '{phenotypeId}'): value '{value}' is not valid for feature '{feature}'.");
            }
        }
    }

    //  DetOrderRules.json 

    private static void ValidateDetOrderRules(string path, ImageNgpVocabulary vocabulary, List<string> problems)
    {
        JsonElement root = LoadRoot(path, problems);
        if (root.ValueKind == JsonValueKind.Undefined) return;

        if (!root.TryGetProperty("productTypes", out JsonElement productTypesEl) || productTypesEl.ValueKind != JsonValueKind.Object) {
            problems.Add("DetOrderRules.json is missing a 'productTypes' object.");
            return;
        }

        foreach (JsonProperty productType in productTypesEl.EnumerateObject()) {
            foreach (JsonProperty slot in productType.Value.EnumerateObject()) {
                if (!slot.Value.TryGetProperty("phenotypes", out JsonElement phenotypesEl) || phenotypesEl.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (JsonElement phenotype in phenotypesEl.EnumerateArray()) {
                    string id = phenotype.GetString() ?? "";
                    if (!vocabulary.HasPhenotype(id))
                        problems.Add($"DetOrderRules.json ({productType.Name}.{slot.Name}): phenotype id '{id}' is not in the ImageNGP.json catalogue.");
                }
            }
        }
    }

    //  ClipPrompts.json 

    private static void ValidateClipPrompts(string path, ImageNgpVocabulary vocabulary, List<string> problems)
    {
        JsonElement root = LoadRoot(path, problems);
        if (root.ValueKind == JsonValueKind.Undefined) return;

        if (!root.TryGetProperty("prompts", out JsonElement promptsEl) || promptsEl.ValueKind != JsonValueKind.Array) {
            problems.Add("ClipPrompts.json is missing a 'prompts' array.");
            return;
        }

        foreach (JsonElement entry in promptsEl.EnumerateArray()) {
            string prompt = entry.TryGetProperty("prompt", out JsonElement promptEl) ? promptEl.GetString() ?? "" : "";
            string feature = entry.TryGetProperty("feature", out JsonElement featureEl) ? featureEl.GetString() ?? "" : "";
            string value = entry.TryGetProperty("value", out JsonElement valueEl) ? valueEl.GetString() ?? "" : "";

            if (!vocabulary.HasFeature(feature)) {
                problems.Add($"ClipPrompts.json (prompt '{prompt}'): unknown feature '{feature}'.");
                continue;
            }

            if (!vocabulary.IsAllowedValue(feature, value))
                problems.Add($"ClipPrompts.json (prompt '{prompt}'): value '{value}' is not valid for feature '{feature}'.");
        }
    }

    //  Helpers 

    /// <summary>
    /// Reads and parses a JSON file. On a missing file or parse error, records a problem and returns
    /// an <see cref="JsonValueKind.Undefined"/> element so the caller skips it.
    /// </summary>
    private static JsonElement LoadRoot(string path, List<string> problems)
    {
        if (!File.Exists(path)) {
            problems.Add($"Required config file not found: '{path}'.");
            return default;
        }

        try {
            string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException ex) {
            problems.Add($"Failed to parse '{path}': {ex.Message}");
            return default;
        }
    }
}
