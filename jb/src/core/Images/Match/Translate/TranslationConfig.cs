using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Prism.Core;

/// <summary>
/// Loads configured matching synonyms and stop words from TranslationConfig.json.
/// </summary>
public sealed record TranslationConfig
{
    /// <summary>
    /// Domain-specific synonym groups used after exact token matching.
    /// </summary>
    public IReadOnlyList<SynonymGroup> SynonymGroups { get; init; } = [];

    /// <summary>
    /// Words ignored during string matching.
    /// </summary>
    public StopWordConfig StopWords { get; init; } = new();

    /// <summary>
    /// Loads translation configuration from a JSON file.
    /// </summary>
    /// <param name="configPath">Path to TranslationConfig.json.</param>
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

        return config ?? throw new InvalidOperationException("Translation config could not be parsed.");
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

        return StopWords.General.Any(stopWord => NormalizeToken(stopWord) == normalizedToken)
            || StopWords.Domain.Any(stopWord => NormalizeToken(stopWord) == normalizedToken);
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

        return SynonymGroups.Any(group => group.ContainsBoth(normalizedLeftToken, normalizedRightToken));
    }

    private static string NormalizeToken(string token)
    {
        return token.Trim().ToLowerInvariant();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

/// <summary>
/// A configured set of product terms that are equivalent for matching.
/// </summary>
public sealed record SynonymGroup
{
    /// <summary>
    /// Stable identifier for the synonym group.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// The product attribute area this group belongs to, such as color, material, or productType.
    /// </summary>
    public string Domain { get; init; } = string.Empty;

    /// <summary>
    /// All words that should be treated as equivalent inside this group.
    /// </summary>
    public IReadOnlyList<string> Terms { get; init; } = [];

    /// <summary>
    /// Checks whether both normalized tokens are present in this group.
    /// </summary>
    /// <param name="normalizedLeftToken">The first normalized token.</param>
    /// <param name="normalizedRightToken">The second normalized token.</param>
    /// <returns>True when both tokens belong to this synonym group.</returns>
    public bool ContainsBoth(string normalizedLeftToken, string normalizedRightToken)
    {
        bool hasLeftToken = Terms.Any(term => term.Trim().ToLowerInvariant() == normalizedLeftToken);
        bool hasRightToken = Terms.Any(term => term.Trim().ToLowerInvariant() == normalizedRightToken);

        return hasLeftToken && hasRightToken;
    }
}

/// <summary>
/// Stop words ignored by string matching while still being available to diagnostics.
/// </summary>
public sealed record StopWordConfig
{
    /// <summary>
    /// Language-neutral common words.
    /// </summary>
    public IReadOnlyList<string> General { get; init; } = [];

    /// <summary>
    /// Domain-specific product words that are too broad to count as evidence.
    /// </summary>
    public IReadOnlyList<string> Domain { get; init; } = [];
}
