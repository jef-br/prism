using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Prism.Core;

/// <summary>
/// Joins orphan worksheet rows (rows without a resolvable FamilyID) to existing families via
/// unique shared keys, after all workbooks have been processed. A catalogue export often carries
/// no FamilyID column of its own — its rows reach a family through an article code that a bundle
/// file's Ref cell names (e.g. "106297094700|…" → Family 98985645) or through a shared EAN.
/// Tier 1 matches against EAN-column digits; tier 2 against every digit run of every family
/// column. A row joins only when its keys point at exactly one family.
/// </summary>
public static class OrphanRowJoiner
{
    private static readonly Regex DigitRunPattern = new(@"\d+", RegexOptions.Compiled);

    // Digit runs shorter than this are not join keys (sizes, quantities, color codes).
    private const int MinimumKeyDigits = 6;

    // Whole-value digit strings longer than this are merged multi-value cells, not identifiers.
    private const int MaximumKeyDigits = 18;

    /// <summary>
    /// Attempts to join every orphan row to an existing family. Joined rows merge their columns
    /// into the family record (enriching it as a matcher target); unjoined rows stay orphaned.
    /// One warning diagnostic per worksheet reports the joined/total tally.
    /// </summary>
    /// <returns>Number of rows joined.</returns>
    public static int Join(InternalExcelModel model, IReadOnlyList<OrphanRow> orphanRows, List<ExcelProcessingDiagnostic> diagnostics)
    {
        if (orphanRows.Count == 0 || model.RecordsByFamilyID.Count == 0)
            return 0;

        Dictionary<string, HashSet<string>> eanKeyIndex = BuildEanKeyIndex(model);
        Dictionary<string, HashSet<string>> runKeyIndex = BuildRunKeyIndex(model);

        Dictionary<(string SourceFile, string WorksheetName), (int Joined, int Total)> tallies = [];
        int joinedTotal = 0;

        foreach (OrphanRow row in orphanRows)
        {
            IReadOnlyList<string> keys = ExtractRowKeys(row);
            string? familyId = ResolveUniqueFamily(keys, eanKeyIndex) ?? ResolveUniqueFamily(keys, runKeyIndex);

            if (familyId is not null)
            {
                model.AddOrMergeFamilyRow(familyId, row.PropertyValues, row.ColumnClassifications);
                joinedTotal++;
            }

            (string SourceFile, string WorksheetName) tallyKey = (row.SourceFile, row.WorksheetName);
            (int joined, int total) = tallies.TryGetValue(tallyKey, out (int Joined, int Total) tally) ? tally : (0, 0);
            tallies[tallyKey] = (joined + (familyId is not null ? 1 : 0), total + 1);
        }

        foreach (KeyValuePair<(string SourceFile, string WorksheetName), (int Joined, int Total)> tally in tallies)
        {
            diagnostics.Add(new ExcelProcessingDiagnostic(
                ExcelDiagnosticSeverity.Warning,
                "excel.orphan_rows_joined",
                $"{tally.Value.Joined} of {tally.Value.Total} rows without a FamilyID were joined to existing families via shared keys.",
                tally.Key.SourceFile,
                tally.Key.WorksheetName,
                null,
                null,
                null));
        }

        return joinedTotal;
    }

    /// <summary>Digit keys of every EAN-canonical column value, mapped to the families that carry them.</summary>
    private static Dictionary<string, HashSet<string>> BuildEanKeyIndex(InternalExcelModel model)
    {
        Dictionary<string, HashSet<string>> index = new(StringComparer.Ordinal);

        foreach (FamilyIDRecord family in model.RecordsByFamilyID.Values)
        {
            if (!family.CanonicalProperties.TryGetValue("ean", out string? eanValue))
                continue;

            foreach (string key in ExtractKeys(eanValue))
                AddKey(index, key, family.FamilyID);
        }

        return index;
    }

    /// <summary>Digit keys of every column value (and the FamilyID itself), mapped to their families.</summary>
    private static Dictionary<string, HashSet<string>> BuildRunKeyIndex(InternalExcelModel model)
    {
        Dictionary<string, HashSet<string>> index = new(StringComparer.Ordinal);

        foreach (FamilyIDRecord family in model.RecordsByFamilyID.Values)
        {
            if (family.FamilyID.Length >= MinimumKeyDigits)
                AddKey(index, family.FamilyID, family.FamilyID);

            foreach (string value in family.CanonicalProperties.Values)
            {
                foreach (string key in ExtractKeys(value))
                    AddKey(index, key, family.FamilyID);
            }
        }

        return index;
    }

    /// <summary>All digit keys of one orphan row's source cell values.</summary>
    private static IReadOnlyList<string> ExtractRowKeys(OrphanRow row)
    {
        List<string> keys = [];

        foreach (ExcelPropertyValue propertyValue in row.PropertyValues)
        {
            foreach (string sourceValue in propertyValue.SourceValues)
            {
                foreach (string key in ExtractKeys(sourceValue))
                {
                    if (!keys.Contains(key))
                        keys.Add(key);
                }
            }
        }

        return keys;
    }

    /// <summary>
    /// Digit keys of one cell value: every digit run of at least MinimumKeyDigits digits, plus the
    /// whole-value digit string when it is identifier-sized.
    /// </summary>
    private static IEnumerable<string> ExtractKeys(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;

        string wholeDigits = string.Concat(value.Where(char.IsDigit));
        if (wholeDigits.Length is >= MinimumKeyDigits and <= MaximumKeyDigits)
            yield return wholeDigits;

        foreach (Match run in DigitRunPattern.Matches(value))
        {
            if (run.Value.Length >= MinimumKeyDigits && run.Value != wholeDigits)
                yield return run.Value;
        }
    }

    /// <summary>The single family every key agrees on, or null when the keys hit zero or several families.</summary>
    private static string? ResolveUniqueFamily(IReadOnlyList<string> keys, Dictionary<string, HashSet<string>> index)
    {
        HashSet<string> families = new(StringComparer.OrdinalIgnoreCase);

        foreach (string key in keys)
        {
            if (!index.TryGetValue(key, out HashSet<string>? holders))
                continue;

            families.UnionWith(holders);
            if (families.Count > 1)
                return null;
        }

        return families.Count == 1 ? families.First() : null;
    }

    private static void AddKey(Dictionary<string, HashSet<string>> index, string key, string familyId)
    {
        if (!index.TryGetValue(key, out HashSet<string>? holders))
            index[key] = holders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        holders.Add(familyId);
    }
}
