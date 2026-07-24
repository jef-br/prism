using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Prism.Services.Matching;

/// <summary>
/// Loads configured matching synonyms and stop words from TranslationDictionary.json.
/// </summary>
public sealed record TranslationConfig
{
    /// <summary>
    /// Domain-specific synonym groups used after exact token matching.
    /// </summary>
    public IReadOnlyList<SynonymGroup> SynonymGroups { get; init; } = [];

    /// <summary>
    /// Multilingual header-term groups used by Excel header/PK detection. Kept separate from
    /// <see cref="SynonymGroups"/> so header vocabulary never contaminates value matching.
    /// </summary>
    public IReadOnlyList<HeaderGroup> HeaderGroups { get; init; } = [];

    /// <summary>
    /// Words ignored during string matching.
    /// </summary>
    public StopWordConfig StopWords { get; init; } = new();

    /// <summary>
    /// Loads translation configuration from a JSON file.
    /// </summary>
    /// <param name="configPath">Path to TranslationDictionary.json.</param>
    /// <returns>The parsed translation configuration.</returns>
    public static TranslationConfig Load(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new ArgumentException("Translation config path is required.", nameof(configPath));
        }

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("Translation config file was not found.", configPath);
        }

        string json = File.ReadAllText(configPath);
        TranslationConfig? config = JsonSerializer.Deserialize<TranslationConfig>(json, JsonOptions);

        return config ?? throw new PrismConfigurationException("Translation config could not be parsed.");
    }

    /// <summary>
    /// Checks whether a token is configured as a stop word.
    /// </summary>
    /// <param name="token">The normalized or raw token.</param>
    /// <returns>True when the token should be ignored by string matching.</returns>
    public bool IsStopWord(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string normalizedToken = NormalizeToken(token);

        return this.StopWords.General.Any(stopWord => NormalizeToken(stopWord) == normalizedToken)
            || this.StopWords.Domain.Any(stopWord => NormalizeToken(stopWord) == normalizedToken);
    }

    /// <summary>
    /// Checks whether a token is a language-neutral general stop word (articles, prepositions).
    /// Header detection uses this instead of <see cref="IsStopWord"/> so domain words that are
    /// noise in values ("color", "style", "size") still count as meaningful column headers.
    /// </summary>
    /// <param name="token">The normalized or raw token.</param>
    /// <returns>True when the token is a general stop word.</returns>
    public bool IsGeneralStopWord(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string normalizedToken = NormalizeToken(token);

        return this.StopWords.General.Any(stopWord => NormalizeToken(stopWord) == normalizedToken);
    }

    /// <summary>
    /// Checks whether two tokens are exact matches or configured synonyms.
    /// </summary>
    /// <param name="leftToken">The first token.</param>
    /// <param name="rightToken">The second token.</param>
    /// <returns>True when tokens match exactly or through a synonym group.</returns>
    public bool AreMatchingTokens(string? leftToken, string? rightToken)
    {
        if (string.IsNullOrWhiteSpace(leftToken) || string.IsNullOrWhiteSpace(rightToken))
        {
            return false;
        }

        string normalizedLeftToken = NormalizeToken(leftToken);
        string normalizedRightToken = NormalizeToken(rightToken);

        if (normalizedLeftToken == normalizedRightToken)
        {
            return true;
        }

        return this.SynonymGroups.Any(group => group.ContainsBoth(normalizedLeftToken, normalizedRightToken));
    }

    /// <summary>
    /// Checks whether a token is a configured header term in any language.
    /// </summary>
    /// <param name="token">A single header token (diacritics already folded by the caller).</param>
    /// <returns>True when the token belongs to any header group.</returns>
    public bool IsHeaderTerm(string? token)
    {
        return this.TryResolveHeaderCanonical(token, out _);
    }

    /// <summary>
    /// Resolves a header token to its canonical English header id (e.g. "color", "familyid").
    /// </summary>
    /// <param name="token">A single header token (diacritics already folded by the caller).</param>
    /// <param name="canonicalId">The matched group id, or empty when no group contains the token.</param>
    /// <returns>True when a header group contains the token.</returns>
    public bool TryResolveHeaderCanonical(string? token, out string canonicalId)
    {
        canonicalId = string.Empty;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string normalizedToken = NormalizeToken(token);

        foreach (HeaderGroup group in this.HeaderGroups)
        {
            if (group.ContainsTerm(normalizedToken))
            {
                canonicalId = group.Id;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves a complete header cell to its canonical id by comparing the whole phrase —
    /// diacritics folded, lowercased, all non-alphanumerics stripped — against every configured
    /// term normalized the same way. This is how multi-word terms like "Product Type" /
    /// "Tipo di prodotto" match, since the per-token path cannot see across token boundaries.
    /// </summary>
    /// <param name="header">The raw header cell text.</param>
    /// <param name="canonicalId">The matched group id, or empty when no group matches the phrase.</param>
    /// <returns>True when a header group term equals the normalized phrase.</returns>
    public bool TryResolveHeaderPhrase(string? header, out string canonicalId)
    {
        canonicalId = string.Empty;

        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        string normalizedPhrase = NormalizePhrase(header);

        if (normalizedPhrase.Length == 0)
        {
            return false;
        }

        foreach (HeaderGroup group in this.HeaderGroups)
        {
            if (group.Terms.Any(term => NormalizePhrase(term) == normalizedPhrase))
            {
                canonicalId = group.Id;
                return true;
            }
        }

        return false;
    }

    private static string NormalizeToken(string token)
    {
        return token.Trim().ToLowerInvariant();
    }

    // Folds diacritics ("código" -> "codigo"), lowercases, and strips every non-alphanumeric
    // character so "Product Type", "product-type", and "producttype" normalize identically.
    private static string NormalizePhrase(string text)
    {
        string decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);

        foreach (char ch in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
