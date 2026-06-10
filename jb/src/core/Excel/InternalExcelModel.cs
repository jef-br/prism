using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Deduplicated in-memory model of accepted Excel data, keyed by FamilyID.
/// </summary>
public sealed class InternalExcelModel
{
    /// <summary>
    /// All deduplicated family records keyed by FamilyID.
    /// </summary>
    public IReadOnlyDictionary<string, FamilyRecord> RecordsByFamilyID => recordsByFamilyID;

    /// <summary>
    /// Token index used by matchers to resolve normalized Excel evidence quickly.
    /// </summary>
    public ExcelTokenStore TokenStore { get; } = new();

    private readonly Dictionary<string, FamilyRecord> recordsByFamilyID = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds one validated worksheet row into the model and merges it with an existing FamilyID when needed.
    /// </summary>
    /// <param name="familyID">Validated primary-key value.</param>
    /// <param name="propertyValues">Dynamic accepted column values for the row.</param>
    /// <param name="columnClassifications">Classifications for accepted dynamic columns.</param>
    public void AddOrMergeFamilyRow(
        string familyID,
        IReadOnlyList<ExcelPropertyValue> propertyValues,
        IReadOnlyDictionary<string, ExcelColumnClassification> columnClassifications)
    {
        if (string.IsNullOrWhiteSpace(familyID))
        {
            throw new ArgumentException("FamilyID is required.", nameof(familyID));
        }

        FamilyRecord familyRecord = GetOrCreateFamilyRecord(familyID.Trim());

        foreach (ExcelPropertyValue propertyValue in propertyValues)
        {
            ExcelColumnClassification classification = columnClassifications.TryGetValue(propertyValue.PropertyName, out ExcelColumnClassification configuredClassification)
                ? configuredClassification
                : ExcelColumnClassification.Descriptive;

            familyRecord.MergeProperty(propertyValue, classification);
        }

        TokenStore.RefreshFromRecord(familyRecord);
    }

    /// <summary>
    /// Maps the Internal Excel Model to the canonical FamilyRecord collection.
    /// </summary>
    /// <returns>One FamilyRecord per valid FamilyID.</returns>
    public IReadOnlyList<FamilyRecord> ToFamilyRecords()
    {
        return recordsByFamilyID.Values
            .OrderBy(record => record.FamilyID, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private FamilyRecord GetOrCreateFamilyRecord(string familyID)
    {
        if (recordsByFamilyID.TryGetValue(familyID, out FamilyRecord? existingRecord))
        {
            return existingRecord;
        }

        FamilyRecord familyRecord = new(familyID);
        recordsByFamilyID.Add(familyID, familyRecord);

        return familyRecord;
    }
}

/// <summary>
/// Searchable token store derived from FamilyRecord normalized tokens.
/// </summary>
public sealed class ExcelTokenStore
{
    /// <summary>
    /// Tokens by normalized value.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ExcelToken>> ByNormalizedValue => byNormalizedValue.ToDictionary(
        pair => pair.Key,
        pair => (IReadOnlyList<ExcelToken>)pair.Value,
        StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, List<ExcelToken>> byNormalizedValue = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ExcelToken> byTokenId = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Rebuilds token entries for one family record.
    /// </summary>
    /// <param name="familyRecord">The family record whose current token state should be indexed.</param>
    public void RefreshFromRecord(FamilyRecord familyRecord)
    {
        RemoveExistingTokensForFamily(familyRecord.FamilyID);

        foreach (KeyValuePair<string, IReadOnlyList<string>> propertyTokens in familyRecord.NormalizedTokens)
        {
            foreach (string normalizedToken in propertyTokens.Value)
            {
                AddToken(familyRecord.FamilyID, propertyTokens.Key, normalizedToken);
            }
        }
    }

    private void RemoveExistingTokensForFamily(string familyID)
    {
        string[] tokenIdsToRemove = byTokenId.Values
            .Where(token => string.Equals(token.FamilyID, familyID, StringComparison.OrdinalIgnoreCase))
            .Select(token => token.TokenID)
            .ToArray();

        foreach (string tokenID in tokenIdsToRemove)
        {
            if (!byTokenId.Remove(tokenID, out ExcelToken? removedToken))
            {
                continue;
            }

            if (!byNormalizedValue.TryGetValue(removedToken.NormalizedValue, out List<ExcelToken>? tokens))
            {
                continue;
            }

            tokens.RemoveAll(token => token.TokenID == tokenID);

            if (tokens.Count == 0)
            {
                byNormalizedValue.Remove(removedToken.NormalizedValue);
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

        byTokenId[tokenID] = token;

        if (!byNormalizedValue.TryGetValue(normalizedToken, out List<ExcelToken>? tokens))
        {
            tokens = [];
            byNormalizedValue.Add(normalizedToken, tokens);
        }

        tokens.Add(token);
    }
}

/// <summary>
/// One normalized Excel token retained as matching evidence.
/// </summary>
public sealed record ExcelToken(
    string TokenID,
    string FamilyID,
    string PropertyName,
    string NormalizedValue);
