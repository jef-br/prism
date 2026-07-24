using System;
using System.Collections.Generic;
using System.Linq;

namespace Prism.Lib.Excel;

/// <summary>
/// Searchable token store derived from FamilyIDRecord normalized tokens.
/// </summary>
public sealed class ExcelTokenStore
{
    /// <summary>
    /// Tokens by normalized value.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ExcelToken>> ByNormalizedValue => this.byNormalizedValue.ToDictionary(
        pair => pair.Key,
        pair => (IReadOnlyList<ExcelToken>)pair.Value,
        StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, List<ExcelToken>> byNormalizedValue = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ExcelToken> byTokenId = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Rebuilds token entries for one family record.
    /// </summary>
    /// <param name="familyIDRecord">The family record whose current token state should be indexed.</param>
    public void RefreshFromRecord(FamilyIDRecord familyIDRecord)
    {
        this.RemoveExistingTokensForFamily(familyIDRecord.FamilyID);

        foreach (KeyValuePair<string, IReadOnlyList<string>> propertyTokens in familyIDRecord.NormalizedTokens)
        {
            foreach (string normalizedToken in propertyTokens.Value)
            {
                this.AddToken(familyIDRecord.FamilyID, propertyTokens.Key, normalizedToken);
            }
        }
    }

    private void RemoveExistingTokensForFamily(string familyID)
    {
        string[] tokenIdsToRemove = this.byTokenId.Values
            .Where(token => string.Equals(token.FamilyID, familyID, StringComparison.OrdinalIgnoreCase))
            .Select(token => token.TokenID)
            .ToArray();

        foreach (string tokenID in tokenIdsToRemove)
        {
            if (!this.byTokenId.Remove(tokenID, out ExcelToken? removedToken))
            {
                continue;
            }

            if (!this.byNormalizedValue.TryGetValue(removedToken.NormalizedValue, out List<ExcelToken>? tokens))
            {
                continue;
            }

            tokens.RemoveAll(token => token.TokenID == tokenID);

            if (tokens.Count == 0)
            {
                this.byNormalizedValue.Remove(removedToken.NormalizedValue);
            }
        }
    }

    private void AddToken(string familyID, string propertyName, string normalizedToken)
    {
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return;
        }

        string tokenID = $"{familyID}:{propertyName}:{normalizedToken}";
        ExcelToken token = new(tokenID, familyID, propertyName, normalizedToken);

        this.byTokenId[tokenID] = token;

        if (!this.byNormalizedValue.TryGetValue(normalizedToken, out List<ExcelToken>? tokens))
        {
            tokens = [];
            this.byNormalizedValue.Add(normalizedToken, tokens);
        }

        tokens.Add(token);
    }
}
