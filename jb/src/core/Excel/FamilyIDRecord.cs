using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Prism.Core;

/// <summary>
/// Represents one deduplicated product family record built from the Internal Excel Model.
/// </summary>
public sealed class FamilyIDRecord
{
    /// <summary>
    /// Creates a family record with the configured primary-key value.
    /// </summary>
    /// <param name="familyID">The validated family identifier.</param>
    public FamilyIDRecord(string familyID)
    {
        if (string.IsNullOrWhiteSpace(familyID))
        {
            throw new ArgumentException("FamilyID is required.", nameof(familyID));
        }

        FamilyID = familyID.Trim();
    }

    /// <summary>
    /// JSON round-trip constructor — rehydrates a family record transmitted between PRISM services over HTTP.
    /// Without this, the get-only dictionaries (e.g. <see cref="CanonicalProperties"/>) deserialize empty and
    /// every non-FamilyID matcher rule silently fails. Rebuilds them with the case-insensitive comparer the
    /// matchers rely on.
    /// </summary>
    [JsonConstructor]
    public FamilyIDRecord(
        string familyID,
        IReadOnlyDictionary<string, string>? canonicalProperties,
        IReadOnlyDictionary<string, ExcelColumnClassification>? columnClassifications,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? normalizedTokens,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? originalSourceCellValues,
        IReadOnlyList<FamilyConflictEvidence>? conflictEvidence)
        : this(familyID)
    {
        if (canonicalProperties is not null)
            foreach (KeyValuePair<string, string> kv in canonicalProperties) this.canonicalProperties[kv.Key] = kv.Value;
        if (columnClassifications is not null)
            foreach (KeyValuePair<string, ExcelColumnClassification> kv in columnClassifications) this.columnClassifications[kv.Key] = kv.Value;
        if (normalizedTokens is not null)
            foreach (KeyValuePair<string, IReadOnlyList<string>> kv in normalizedTokens) this.normalizedTokens[kv.Key] = kv.Value;
        if (originalSourceCellValues is not null)
            foreach (KeyValuePair<string, IReadOnlyList<string>> kv in originalSourceCellValues) this.originalSourceCellValues[kv.Key] = kv.Value;
        if (conflictEvidence is not null)
            this.conflictEvidence.AddRange(conflictEvidence);
    }

    /// <summary>
    /// Product or family identifier.
    /// </summary>
    public string FamilyID { get; }

    /// <summary>
    /// Canonical dynamic properties derived from accepted Excel columns.
    /// </summary>
    public IReadOnlyDictionary<string, string> CanonicalProperties => canonicalProperties;

    /// <summary>
    /// Dynamic column classifications used by matchers.
    /// </summary>
    public IReadOnlyDictionary<string, ExcelColumnClassification> ColumnClassifications => columnClassifications;

    /// <summary>
    /// Normalized tokens by property name.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> NormalizedTokens => normalizedTokens;

    /// <summary>
    /// Original cell values by property name.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> OriginalSourceCellValues => originalSourceCellValues;

    /// <summary>
    /// Conflicting row or column values preserved for manifest and workbench review.
    /// </summary>
    public IReadOnlyList<FamilyConflictEvidence> ConflictEvidence => conflictEvidence;

    private readonly Dictionary<string, string> canonicalProperties = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ExcelColumnClassification> columnClassifications = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<string>> normalizedTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<string>> originalSourceCellValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FamilyConflictEvidence> conflictEvidence = [];

    /// <summary>
    /// Adds or merges a dynamic property value into this family record.
    /// </summary>
    /// <param name="propertyValue">The property value extracted from a worksheet row.</param>
    /// <param name="classification">The dynamic column classification.</param>
    public void MergeProperty(ExcelPropertyValue propertyValue, ExcelColumnClassification classification)
    {
        if (string.IsNullOrWhiteSpace(propertyValue.PropertyName))
        {
            throw new ArgumentException("Property name is required.", nameof(propertyValue));
        }

        columnClassifications[propertyValue.PropertyName] = classification;

        List<string> sourceValues = GetExistingSourceValues(propertyValue.PropertyName);
        sourceValues.AddRange(propertyValue.SourceValues.Where(value => !string.IsNullOrWhiteSpace(value)));
        originalSourceCellValues[propertyValue.PropertyName] = sourceValues.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        List<string> mergedTokens = GetExistingTokens(propertyValue.PropertyName);
        mergedTokens.AddRange(propertyValue.SourceValues.SelectMany(TokenizeCellValue));
        normalizedTokens[propertyValue.PropertyName] = mergedTokens.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(token => token, StringComparer.OrdinalIgnoreCase).ToArray();

        string currentCanonicalValue = canonicalProperties.TryGetValue(propertyValue.PropertyName, out string? existingValue)
            ? existingValue
            : string.Empty;

        string incomingCanonicalValue = BuildCanonicalValue(propertyValue.SourceValues);

        if (string.IsNullOrWhiteSpace(currentCanonicalValue))
        {
            canonicalProperties[propertyValue.PropertyName] = incomingCanonicalValue;
            AddConflictEvidenceWhenNeeded(propertyValue, "duplicate-column");
            return;
        }

        if (string.IsNullOrWhiteSpace(incomingCanonicalValue) || ValuesAreEquivalent(currentCanonicalValue, incomingCanonicalValue))
        {
            AddConflictEvidenceWhenNeeded(propertyValue, "duplicate-column");
            return;
        }

        IReadOnlyList<string> allTokens = TokenizeCellValue(currentCanonicalValue)
            .Concat(TokenizeCellValue(incomingCanonicalValue))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(token => token, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        canonicalProperties[propertyValue.PropertyName] = string.Join(" ", allTokens);
        conflictEvidence.Add(new FamilyConflictEvidence(
            FamilyID,
            propertyValue.PropertyName,
            "duplicate-row-or-column",
            [currentCanonicalValue, .. propertyValue.SourceValues.Where(value => !string.IsNullOrWhiteSpace(value))],
            allTokens,
            propertyValue.SourceLocations));
    }

    private List<string> GetExistingSourceValues(string propertyName)
    {
        return originalSourceCellValues.TryGetValue(propertyName, out IReadOnlyList<string>? existingValues)
            ? existingValues.ToList()
            : [];
    }

    private List<string> GetExistingTokens(string propertyName)
    {
        return normalizedTokens.TryGetValue(propertyName, out IReadOnlyList<string>? existingTokens)
            ? existingTokens.ToList()
            : [];
    }

    private void AddConflictEvidenceWhenNeeded(ExcelPropertyValue propertyValue, string reasonCode)
    {
        string[] uniqueValues = propertyValue.SourceValues
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (uniqueValues.Length <= 1)
        {
            return;
        }

        string[] tokens = uniqueValues
            .SelectMany(TokenizeCellValue)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(token => token, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        conflictEvidence.Add(new FamilyConflictEvidence(
            FamilyID,
            propertyValue.PropertyName,
            reasonCode,
            uniqueValues,
            tokens,
            propertyValue.SourceLocations));
    }

    private static bool ValuesAreEquivalent(string leftValue, string rightValue)
    {
        return string.Equals(leftValue.Trim(), rightValue.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCanonicalValue(IReadOnlyList<string> sourceValues)
    {
        string[] uniqueValues = sourceValues
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (uniqueValues.Length <= 1)
        {
            return uniqueValues.FirstOrDefault() ?? string.Empty;
        }

        string[] uniqueTokens = uniqueValues
            .SelectMany(TokenizeCellValue)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(token => token, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return string.Join(" ", uniqueTokens);
    }

    private static IReadOnlyList<string> TokenizeCellValue(string sourceValue)
    {
        if (string.IsNullOrWhiteSpace(sourceValue))
        {
            return [];
        }

        return sourceValue
            .Split([' ', '\t', '\r', '\n', ',', ';', '/', '\\', '|', ':', '.', '_', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.ToLowerInvariant())
            .Where(token => token.Length > 0)
            .ToArray();
    }
}

/// <summary>
/// Dynamic classification assigned to an accepted Excel column.
/// </summary>
public enum ExcelColumnClassification
{
    /// <summary>
    /// The FamilyID column — the configured primary key of an Excel record.
    /// </summary>
    FamilyID,

    /// <summary>
    /// All useful values are numeric.
    /// </summary>
    Numerical,

    /// <summary>
    /// Short, low-cardinality string values.
    /// </summary>
    Categorical,

    /// <summary>
    /// Longer or high-cardinality string values.
    /// </summary>
    Descriptive,

    /// <summary>
    /// Values contain both letters and digits.
    /// </summary>
    Mixed
}

/// <summary>
/// One property value extracted from an Excel row, including duplicate-column source values.
/// </summary>
public sealed record ExcelPropertyValue(
    string PropertyName,
    IReadOnlyList<string> SourceValues,
    IReadOnlyList<ExcelCellAddress> SourceLocations);

/// <summary>
/// Evidence retained when duplicate rows or columns disagree.
/// </summary>
public sealed record FamilyConflictEvidence(
    string FamilyID,
    string PropertyName,
    string ReasonCode,
    IReadOnlyList<string> SourceValues,
    IReadOnlyList<string> NormalizedTokens,
    IReadOnlyList<ExcelCellAddress> SourceLocations);

/// <summary>
/// Address of a source cell that contributed to the Internal Excel Model.
/// </summary>
public sealed record ExcelCellAddress(
    string SourceFile,
    string WorksheetName,
    int RowNumber,
    int ColumnNumber,
    string HeaderName);
