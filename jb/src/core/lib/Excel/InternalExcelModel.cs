using System;
using System.Collections.Generic;
using System.Linq;

namespace Prism.Lib.Excel;

/// <summary>
/// Deduplicated in-memory model of accepted Excel data, keyed by FamilyID.
/// </summary>
public sealed class InternalExcelModel
{
    /// <summary>
    /// All deduplicated family records keyed by FamilyID.
    /// </summary>
    public IReadOnlyDictionary<string, FamilyIDRecord> RecordsByFamilyID => this.recordsByFamilyID;

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

        FamilyIDRecord familyIDRecord = this.GetOrCreateFamilyRecord(familyID.Trim());

        foreach (ExcelPropertyValue propertyValue in propertyValues)
        {
            ExcelColumnClassification classification = columnClassifications.TryGetValue(propertyValue.PropertyName, out ExcelColumnClassification configuredClassification)
                ? configuredClassification
                : ExcelColumnClassification.Descriptive;

            familyIDRecord.MergeProperty(propertyValue, classification);
        }

        this.TokenStore.RefreshFromRecord(familyIDRecord);
    }

    /// <summary>
    /// Maps the Internal Excel Model to the canonical FamilyIDRecord collection.
    /// </summary>
    /// <returns>One FamilyIDRecord per valid FamilyID.</returns>
    public IReadOnlyList<FamilyIDRecord> ToFamilyRecords()
    {
        return this.recordsByFamilyID.Values
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
        foreach (FamilyIDRecord record in this.recordsByFamilyID.Values)
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
            .Where(name => this.recordsByFamilyID.Values.All(record =>
                !record.CanonicalProperties.TryGetValue(name, out string? value) || string.IsNullOrWhiteSpace(value)))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (emptyNames.Length == 0)
        {
            return emptyNames;
        }

        foreach (FamilyIDRecord record in this.recordsByFamilyID.Values)
        {
            foreach (string emptyName in emptyNames)
            {
                record.RemoveProperty(emptyName);
            }

            this.TokenStore.RefreshFromRecord(record);
        }

        return emptyNames;
    }

    private FamilyIDRecord GetOrCreateFamilyRecord(string familyID)
    {
        if (this.recordsByFamilyID.TryGetValue(familyID, out FamilyIDRecord? existingRecord))
        {
            return existingRecord;
        }

        FamilyIDRecord familyIDRecord = new(familyID);
        this.recordsByFamilyID.Add(familyID, familyIDRecord);

        return familyIDRecord;
    }
}
