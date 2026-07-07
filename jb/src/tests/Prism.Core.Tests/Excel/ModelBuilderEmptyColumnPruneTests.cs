using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PrismCoreTests.Excel;

/// <summary>
/// Unit tests for the model-scope empty-column prune that runs after Excel collation
/// (<see cref="InternalExcelModel.PruneEmptyProperties"/> wired into <see cref="ModelBuilder"/>).
/// A canonical property blank across every merged family record is dropped to shrink the matcher
/// search space; the FamilyID primary key and any property with at least one value are retained.
/// </summary>
public class ModelBuilderEmptyColumnPruneTests
{
    //  Direct model-level prune behavior

    [Fact]
    public void PruneEmptyProperties_DropsPropertyBlankAcrossAllRecords()
    {
        InternalExcelModel model = new();
        MergeRow(model, "10000001", ("color", "blue"), ("weight", ""));
        MergeRow(model, "10000002", ("color", "red"), ("weight", "   "));

        IReadOnlyList<string> dropped = model.PruneEmptyProperties("FamilyID");

        Assert.Contains("weight", dropped);
        Assert.DoesNotContain("color", dropped);
        Assert.All(model.ToFamilyRecords(), record => Assert.False(record.CanonicalProperties.ContainsKey("weight")));
        Assert.All(model.ToFamilyRecords(), record => Assert.False(record.ColumnClassifications.ContainsKey("weight")));
        Assert.All(model.ToFamilyRecords(), record => Assert.False(record.NormalizedTokens.ContainsKey("weight")));
    }

    [Fact]
    public void PruneEmptyProperties_KeepsPropertyWithValueInAnySingleRecord()
    {
        InternalExcelModel model = new();
        MergeRow(model, "10000001", ("color", "blue"));           // color present here
        MergeRow(model, "10000002", ("color", ""));               // blank in the second family

        IReadOnlyList<string> dropped = model.PruneEmptyProperties("FamilyID");

        Assert.DoesNotContain("color", dropped);
        Assert.Contains(model.ToFamilyRecords(), record => record.CanonicalProperties.ContainsKey("color"));
    }

    [Fact]
    public void PruneEmptyProperties_NeverDropsPrimaryKey()
    {
        InternalExcelModel model = new();
        // Register the primary key as a property that happens to be blank-valued.
        MergeRow(model, "10000001", ("FamilyID", ""));

        IReadOnlyList<string> dropped = model.PruneEmptyProperties("FamilyID");

        Assert.DoesNotContain("FamilyID", dropped);
    }

    [Fact]
    public void PruneEmptyProperties_ClearsTokenStoreForDroppedProperty()
    {
        InternalExcelModel model = new();
        MergeRow(model, "10000001", ("color", "blue"), ("weight", ""));

        model.PruneEmptyProperties("FamilyID");

        bool anyWeightToken = model.TokenStore.ByNormalizedValue.Values
            .SelectMany(tokens => tokens)
            .Any(token => token.PropertyName == "weight");
        Assert.False(anyWeightToken);
    }

    //  End-to-end through the builder + diagnostic emission

    [Fact]
    public void BuildFromWorkbooks_ColumnWithValuesInOneFileButNotTheSurvivingFamilies_PrunedModelWide()
    {
        ModelBuilder builder = ExcelTestFixtures.BuildBuilder();

        // File A: two families, no Notes column at all.
        ExcelWorkbook fileA = ExcelTestFixtures.Workbook("a",
            ["Family ID", "Color"],
            ["50000001", "blue"],
            ["50000002", "red"]);

        // File B: a Notes column that is well-populated WITHIN its own worksheet (so it survives the
        // per-worksheet fill-ratio gate), but every one of its rows has a blank Notes cell for the
        // families that carry a resolvable FamilyID matching file A. Here file B's Notes values sit on
        // rows whose FamilyID is invalid, so those rows orphan and their Notes never reach a family —
        // leaving Notes registered but model-wide empty. This is the case only the post-merge prune catches.
        ExcelWorkbook fileB = ExcelTestFixtures.Workbook("b",
            ["Family ID", "Notes"],
            ["50000001", ""],
            ["50000002", ""],
            ["bad-key-1", "some note"],
            ["bad-key-2", "another note"],
            ["bad-key-3", "third note"]);

        ExcelModelBuildResult result = builder.BuildFromWorkbooks([fileA, fileB]);

        // Notes is gone from every surviving family, and it was removed by the model-wide prune
        // (its within-sheet fill ratio in file B was high enough to pass the per-worksheet gate).
        Assert.All(result.Model.ToFamilyRecords(), record => Assert.False(record.CanonicalProperties.ContainsKey("notes")));
        Assert.Contains(result.Diagnostics, d => d.ReasonCode == "excel.column_dropped_empty_model_wide");

        // FamilyID resolution and a real column are untouched.
        Assert.Contains(result.Model.ToFamilyRecords(), f => f.FamilyID == "50000001");
        Assert.Contains(result.Model.ToFamilyRecords(), f => f.CanonicalProperties.ContainsKey("color"));
    }

    //  Helpers

    private static void MergeRow(InternalExcelModel model, string familyID, params (string Name, string Value)[] properties)
    {
        List<ExcelPropertyValue> propertyValues = properties
            .Select(property => new ExcelPropertyValue(property.Name, [property.Value], []))
            .ToList();

        Dictionary<string, ExcelColumnClassification> classifications = properties.ToDictionary(
            property => property.Name,
            property => ExcelColumnClassification.Descriptive,
            System.StringComparer.OrdinalIgnoreCase);

        model.AddOrMergeFamilyRow(familyID, propertyValues, classifications);
    }
}
