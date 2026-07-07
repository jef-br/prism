using System;
using System.Collections.Generic;
using System.Linq;

namespace Prism.Core;

/// <summary>
/// Deduplicated in-memory model of accepted Excel data, keyed by FamilyID.
/// </summary>
public sealed class InternalExcelModel
{
    /// <summary>
    /// All deduplicated family records keyed by FamilyID.
    /// </summary>
    public IReadOnlyDictionary<string, FamilyIDRecord> RecordsByFamilyID => recordsByFamilyID;

    /// <summary>
    /// Token index used by matchers to resolve normalized Excel evidence quickly.
    /// </summary>
    public ExcelTokenStore TokenStore { get; } = new();

    private readonly Dictionary<string, FamilyIDRecord> recordsByFamilyID = new(StringComparer.OrdinalIgnoreCase);

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

        FamilyIDRecord familyIDRecord = GetOrCreateFamilyRecord(familyID.Trim());

        foreach (ExcelPropertyValue propertyValue in propertyValues)
        {
            ExcelColumnClassification classification = columnClassifications.TryGetValue(propertyValue.PropertyName, out ExcelColumnClassification configuredClassification)
                ? configuredClassification
                : ExcelColumnClassification.Descriptive;

            familyIDRecord.MergeProperty(propertyValue, classification);
        }

        TokenStore.RefreshFromRecord(familyIDRecord);
    }

    /// <summary>
    /// Maps the Internal Excel Model to the canonical FamilyIDRecord collection.
    /// </summary>
    /// <returns>One FamilyIDRecord per valid FamilyID.</returns>
    public IReadOnlyList<FamilyIDRecord> ToFamilyRecords()
    {
        return recordsByFamilyID.Values
            .OrderBy(record => record.FamilyID, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Removes canonical properties that are empty across every family record, shrinking the model
    /// and the downstream matcher search space. Runs once after collation completes. The primary-key
    /// property is never pruned. Returns the dropped property names for diagnostic emission.
    /// </summary>
    /// <param name="primaryKeyName">Configured primary-key property to exempt from pruning.</param>
    /// <returns>Names of the properties dropped, sorted for stable diagnostics.</returns>
    internal IReadOnlyList<string> PruneEmptyProperties(string primaryKeyName)
    {
        // A property can linger in classifications/tokens without ever holding a canonical value (an
        // all-blank column that survived the per-worksheet fill gate registers a classification but no
        // canonical value). Union all property-name-keyed dictionaries so we prune that bloat too.
        HashSet<string> candidateNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (FamilyIDRecord record in recordsByFamilyID.Values)
        {
            foreach (string propertyName in record.CanonicalProperties.Keys
                .Concat(record.ColumnClassifications.Keys)
                .Concat(record.NormalizedTokens.Keys)
                .Concat(record.OriginalSourceCellValues.Keys))
            {
                if (!string.Equals(propertyName, primaryKeyName, StringComparison.OrdinalIgnoreCase))
                {
                    candidateNames.Add(propertyName);
                }
            }
        }

        string[] emptyNames = candidateNames
            .Where(name => recordsByFamilyID.Values.All(record =>
                !record.CanonicalProperties.TryGetValue(name, out string? value) || string.IsNullOrWhiteSpace(value)))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (emptyNames.Length == 0)
        {
            return emptyNames;
        }

        foreach (FamilyIDRecord record in recordsByFamilyID.Values)
        {
            foreach (string emptyName in emptyNames)
            {
                record.RemoveProperty(emptyName);
            }

            TokenStore.RefreshFromRecord(record);
        }

        return emptyNames;
    }

    private FamilyIDRecord GetOrCreateFamilyRecord(string familyID)
    {
        if (recordsByFamilyID.TryGetValue(familyID, out FamilyIDRecord? existingRecord))
        {
            return existingRecord;
        }

        FamilyIDRecord familyIDRecord = new(familyID);
        recordsByFamilyID.Add(familyID, familyIDRecord);

        return familyIDRecord;
    }
}

/// <summary>
/// Searchable token store derived from FamilyIDRecord normalized tokens.
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
    /// <param name="familyIDRecord">The family record whose current token state should be indexed.</param>
    public void RefreshFromRecord(FamilyIDRecord familyIDRecord)
    {
        RemoveExistingTokensForFamily(familyIDRecord.FamilyID);

        foreach (KeyValuePair<string, IReadOnlyList<string>> propertyTokens in familyIDRecord.NormalizedTokens)
        {
            foreach (string normalizedToken in propertyTokens.Value)
            {
                AddToken(familyIDRecord.FamilyID, propertyTokens.Key, normalizedToken);
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
