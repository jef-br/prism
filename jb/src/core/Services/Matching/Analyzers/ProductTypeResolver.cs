using System.Text.Json;

namespace Prism.Services.Matching;

/// <summary>
/// Maps raw product-type evidence — Excel producttype/NGP column values, filename tokens, and
/// CLIP product-type-label values — to the canonical DetOrderRules productTypes slugs via
/// ProductTypeMap.json. Resolution order per value: kebab-normalized equality with a slug,
/// whole-phrase term equality, then per-token term lookup. Comparison is alphanumeric-only,
/// case- and diacritics-insensitive. Returns null when nothing maps — never guesses.
/// </summary>
public sealed class ProductTypeResolver {
    private readonly HashSet<string> slugs;
    private readonly Dictionary<string, string> termToSlug;

    private ProductTypeResolver(HashSet<string> slugs, Dictionary<string, string> termToSlug) {
        this.slugs = slugs;
        this.termToSlug = termToSlug;
    }

    /// <summary>Loads and indexes ProductTypeMap.json from the given path.</summary>
    public static ProductTypeResolver Load(string jsonPath) {
        if (!File.Exists(jsonPath))
            throw new PrismConfigurationException($"ProductTypeMap.json not found at: {jsonPath}");

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        if (!doc.RootElement.TryGetProperty("productTypes", out JsonElement types) || types.ValueKind != JsonValueKind.Object)
            throw new PrismConfigurationException($"ProductTypeMap.json at {jsonPath} is missing the \"productTypes\" object.");

        HashSet<string> slugs = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> termToSlug = new(StringComparer.OrdinalIgnoreCase);

        foreach (JsonProperty type in types.EnumerateObject()) {
            slugs.Add(type.Name);
            foreach (JsonElement term in type.Value.EnumerateArray()) {
                string normalized = NormalizeTerm(term.GetString() ?? string.Empty);
                if (normalized.Length > 0) termToSlug.TryAdd(normalized, type.Name);
            }
        }

        if (slugs.Count == 0)
            throw new PrismConfigurationException($"ProductTypeMap.json at {jsonPath} defines no product types.");

        return new ProductTypeResolver(slugs, termToSlug);
    }

    /// <summary>
    /// Resolves the product type from a family's IEM properties: the canonical "producttype"
    /// column first, then the client "ngp" column (an Excel field unrelated to the ImageNGP
    /// phenotype taxonomy). Null when neither column resolves.
    /// </summary>
    public string? ResolveFromFamily(FamilyIDRecord family) {
        if (family.CanonicalProperties.TryGetValue("producttype", out string? productType)
            && this.ResolveValue(productType) is string fromProductType)
            return fromProductType;

        if (family.CanonicalProperties.TryGetValue("ngp", out string? ngp)
            && this.ResolveValue(ngp) is string fromNgp)
            return fromNgp;

        return null;
    }

    /// <summary>
    /// Resolves a single raw value ("camiseta", "topwear", "Tote bag") to a canonical slug,
    /// or null when nothing maps.
    /// </summary>
    public string? ResolveValue(string? value) {
        if (string.IsNullOrWhiteSpace(value)) return null;

        string kebab = value.Trim().ToLowerInvariant().Replace(' ', '-').Replace('_', '-');
        if (this.slugs.Contains(kebab)) return this.slugs.First(s => string.Equals(s, kebab, StringComparison.OrdinalIgnoreCase));

        string phrase = NormalizeTerm(value);
        if (this.termToSlug.TryGetValue(phrase, out string? phraseSlug)) return phraseSlug;

        return this.ResolveTokens(Tokenize(value));
    }

    /// <summary>Resolves the first token that maps to a term, or null.</summary>
    public string? ResolveTokens(IEnumerable<string> tokens) {
        foreach (string token in tokens) {
            string normalized = NormalizeTerm(token);
            if (normalized.Length > 0 && this.termToSlug.TryGetValue(normalized, out string? slug)) return slug;
        }
        return null;
    }

    /// <summary>Splits a raw value into alphanumeric tokens.</summary>
    public static IEnumerable<string> Tokenize(string value) {
        var current = new System.Text.StringBuilder();
        foreach (char ch in value) {
            if (char.IsLetterOrDigit(ch)) { current.Append(ch); continue; }
            if (current.Length > 0) { yield return current.ToString(); current.Clear(); }
        }
        if (current.Length > 0) yield return current.ToString();
    }

    // Folds diacritics, lowercases, strips non-alphanumerics — same contract as the Excel
    // header phrase normalization so evidence matches regardless of source formatting.
    private static string NormalizeTerm(string text) {
        string decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);

        foreach (char ch in decomposed) {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(ch)) builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }
}
