using System.Text.Json;

namespace Prism.Core;

/// <summary>
/// Loaded configuration for det-slot ordering rules and filename keyword stems.
/// Parses DetOrderRules.json and DetOrderKeywordStems.json on construction.
/// </summary>
public sealed class DetOrderConfig
{
    // productTypeId (lowercase) → ordered list of slot rules
    private readonly Dictionary<string, List<DetSlotRule>> slotsByProductType;

    // keyword → list of stem strings (lowercase)
    private readonly Dictionary<string, List<string>> stemsByKeyword;

    private DetOrderConfig(
        Dictionary<string, List<DetSlotRule>> slotsByProductType,
        Dictionary<string, List<string>> stemsByKeyword)
    {
        this.slotsByProductType = slotsByProductType;
        this.stemsByKeyword     = stemsByKeyword;
    }

    //  Factory 

    /// <summary>
    /// Loads and parses both JSON config files.
    /// Throws <see cref="InvalidOperationException"/> on bad JSON or missing required structure.
    /// </summary>
    /// <param name="rulesPath">Absolute path to DetOrderRules.json.</param>
    /// <param name="stemsPath">Absolute path to DetOrderKeywordStems.json.</param>
    public static DetOrderConfig Load(string rulesPath, string stemsPath)
    {
        var slots = ParseRules(rulesPath);
        var stems = ParseStems(stemsPath);
        return new DetOrderConfig(slots, stems);
    }

    //  Public API 

    /// <summary>
    /// Returns the ordered det slot rules for the given product type id.
    /// Falls back to "default" when <paramref name="productTypeId"/> is not in the config.
    /// </summary>
    /// <param name="productTypeId">Product type id (e.g. "clothing-tops"), or null.</param>
    public IReadOnlyList<DetSlotRule> GetSlots(string? productTypeId)
    {
        if (productTypeId is not null &&
            slotsByProductType.TryGetValue(productTypeId, out List<DetSlotRule>? found))
        {
            return found;
        }

        return slotsByProductType.TryGetValue("default", out List<DetSlotRule>? fallback)
            ? fallback
            : [];
    }

    /// <summary>
    /// Returns true when the given product type id is present in the loaded rules.
    /// </summary>
    public bool HasProductType(string key)
        => slotsByProductType.ContainsKey(key);

    /// <summary>
    /// Returns true when any token in the filename stem matches a known stem for the keyword.
    /// Splits on <c>_</c>, <c>-</c>, space, and <c>.</c>; compares case-insensitively.
    /// Returns false when the keyword is not present in the stems dictionary.
    /// </summary>
    /// <param name="filename">Original filename (may include extension).</param>
    /// <param name="keyword">Keyword from a slot rule (e.g. "front", "back").</param>
    public bool FilenameMatchesSlotKeyword(string filename, string keyword)
    {
        if (!stemsByKeyword.TryGetValue(keyword, out List<string>? stems))
            return false;

        string stemName = Path.GetFileNameWithoutExtension(filename);
        string[] tokens = stemName.Split(['_', '-', ' ', '.'], StringSplitOptions.RemoveEmptyEntries);

        foreach (string token in tokens)
        {
            if (stems.Contains(token.ToLowerInvariant()))
                return true;
        }

        return false;
    }

    //  Parsers 

    /// <summary>
    /// Parses DetOrderRules.json.
    /// Expected structure:
    /// <code>{ "productTypes": { "clothing-tops": { "det0": { "keyword": "front", "phenotypes": [...] }, ... }, ... } }</code>
    /// </summary>
    private static Dictionary<string, List<DetSlotRule>> ParseRules(string path)
    {
        string json = ReadJsonFile(path);
        using JsonDocument doc = ParseJsonDocument(json, path);

        if (!doc.RootElement.TryGetProperty("productTypes", out JsonElement productTypesEl))
            throw new InvalidOperationException(
                $"DetOrderRules.json at '{path}' is missing required 'productTypes' property.");

        var result = new Dictionary<string, List<DetSlotRule>>(StringComparer.OrdinalIgnoreCase);

        foreach (JsonProperty productType in productTypesEl.EnumerateObject())
        {
            string typeId = productType.Name.ToLowerInvariant();
            var slotRules = new List<DetSlotRule>();

            foreach (JsonProperty slotEntry in productType.Value.EnumerateObject())
            {
                int slotIndex = ParseSlotIndex(slotEntry.Name, path);

                string keyword = slotEntry.Value.TryGetProperty("keyword", out JsonElement kwEl)
                    ? kwEl.GetString() ?? string.Empty
                    : string.Empty;

                List<string> phenotypes = [];
                if (slotEntry.Value.TryGetProperty("phenotypes", out JsonElement phenotypesEl))
                {
                    foreach (JsonElement phenotype in phenotypesEl.EnumerateArray())
                    {
                        string? id = phenotype.GetString();
                        if (!string.IsNullOrWhiteSpace(id))
                            phenotypes.Add(id);
                    }
                }

                slotRules.Add(new DetSlotRule(slotIndex, keyword, phenotypes));
            }

            slotRules.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
            result[typeId] = slotRules;
        }

        return result;
    }

    /// <summary>
    /// Parses DetOrderKeywordStems.json.
    /// Expected structure:
    /// <code>{ "DetOrderKeywordStems": { "front": [...stems...], ... } }</code>
    /// </summary>
    private static Dictionary<string, List<string>> ParseStems(string path)
    {
        string json = ReadJsonFile(path);
        using JsonDocument doc = ParseJsonDocument(json, path);

        if (!doc.RootElement.TryGetProperty("DetOrderKeywordStems", out JsonElement stemsEl))
            throw new InvalidOperationException(
                $"DetOrderKeywordStems.json at '{path}' is missing required 'DetOrderKeywordStems' property.");

        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (JsonProperty keyword in stemsEl.EnumerateObject())
        {
            var stemList = new List<string>();
            foreach (JsonElement stem in keyword.Value.EnumerateArray())
            {
                string? s = stem.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    stemList.Add(s.ToLowerInvariant());
            }
            result[keyword.Name.ToLowerInvariant()] = stemList;
        }

        return result;
    }

    //  Helpers 

    private static string ReadJsonFile(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"Config file not found: '{path}'.");
        return File.ReadAllText(path, System.Text.Encoding.UTF8);
    }

    private static JsonDocument ParseJsonDocument(string json, string path)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse JSON at '{path}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parses slot name in the form "det0", "det1", … into an integer index.
    /// </summary>
    private static int ParseSlotIndex(string slotName, string sourcePath)
    {
        if (slotName.StartsWith("det", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(slotName[3..], out int index))
        {
            return index;
        }

        throw new InvalidOperationException(
            $"Unexpected slot name '{slotName}' in '{sourcePath}'. Expected format: det0, det1, …");
    }
}
