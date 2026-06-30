using System.Linq;
using Xunit;

namespace PrismCoreTests.Excel;

/// <summary>
/// Unit tests for <see cref="ModelBuilder"/> multilingual, token-based header detection and
/// FamilyID-column resolution (header-name OR 8-digit-unique cell pattern).
/// </summary>
public class ModelBuilderHeaderDetectionTests
{
    //  Multilingual header detection + PK by name

    [Fact]
    public void FrenchMultiWordHeader_DetectedAndFamilyResolvedByName()
    {
        ModelBuilder builder = BuildBuilder();
        ExcelWorkbook workbook = Workbook("fr",
            ["Family ID Veepee", "EAN / BARCODE", "Reference-colour", "Composition"],
            ["12345678", "7350080719990", "ABC-blue", "coton"],
            ["12345679", "7350080719991", "ABC-red", "laine"]);

        ExcelModelBuildResult result = builder.BuildFromWorkbooks([workbook]);
        var families = result.Model.ToFamilyRecords();

        Assert.Equal(2, families.Count);
        Assert.Contains(families, f => f.FamilyID == "12345678");
        Assert.DoesNotContain(result.Diagnostics, d => d.ReasonCode == "excel.header_not_found");
    }

    [Fact]
    public void SpanishHeader_VeepeeTokenResolvesFamilyId()
    {
        ModelBuilder builder = BuildBuilder();
        ExcelWorkbook workbook = Workbook("es",
            ["REFERENCIA VEEPEE", "MODELO", "Color", "CUIDADOS"],
            ["24211511", "M100", "azul", "lavar a mano"],
            ["24211512", "M200", "rojo", "lavar a mano"]);

        ExcelModelBuildResult result = builder.BuildFromWorkbooks([workbook]);
        var families = result.Model.ToFamilyRecords();

        Assert.Equal(2, families.Count);
        Assert.Contains(families, f => f.FamilyID == "24211511");
    }

    [Fact]
    public void HeaderOnRow17_DetectedWithinSearchSpace()
    {
        ModelBuilder builder = BuildBuilder();

        // 16 leading metadata rows (single innocuous cell), header on zero-based row 16, then data.
        var rows = new List<string[]>();
        for (int i = 0; i < 16; i++) rows.Add(["Mia Tomazzi spring report"]);
        rows.Add(["Family ID", "EAN", "Color"]);
        rows.Add(["88888888", "123", "blue"]);
        rows.Add(["88888889", "124", "red"]);

        ExcelModelBuildResult result = builder.BuildFromWorkbooks([Workbook("ma23", [.. rows])]);
        var families = result.Model.ToFamilyRecords();

        Assert.Contains(families, f => f.FamilyID == "88888888");
    }

    //  PK by cell pattern (foreign / unrecognized header name)

    [Fact]
    public void UnrecognizedKeyHeader_FamilyIdResolvedByEightDigitUniqueCellPattern()
    {
        ModelBuilder builder = BuildBuilder();
        ExcelWorkbook workbook = Workbook("pattern",
            ["Color", "Material", "Codpro"],
            ["blue", "cotton", "20000001"],
            ["red", "wool", "20000002"]);

        ExcelModelBuildResult result = builder.BuildFromWorkbooks([workbook]);
        var families = result.Model.ToFamilyRecords();

        Assert.Equal(2, families.Count);
        Assert.Contains(families, f => f.FamilyID == "20000001");
    }

    [Fact]
    public void EightDigitColumnWithDuplicates_NotConfirmedAsFamilyId()
    {
        ModelBuilder builder = BuildBuilder();
        ExcelWorkbook workbook = Workbook("dup",
            ["Color", "Material", "Codpro"],
            ["blue", "cotton", "20000001"],
            ["red", "wool", "20000001"]); // non-unique → fails the uniqueness rule

        ExcelModelBuildResult result = builder.BuildFromWorkbooks([workbook]);

        Assert.Empty(result.Model.ToFamilyRecords());
        Assert.Contains(result.Diagnostics, d => d.ReasonCode == "excel.primary_key_column_not_found");
    }

    //  Cross-file collation + cross-language column canonicalization

    [Fact]
    public void SameFamilyIdAcrossTwoFiles_CollatedIntoOneRecord()
    {
        ModelBuilder builder = BuildBuilder();
        ExcelWorkbook fileA = Workbook("a", ["Family ID", "Color"], ["30000001", "blue"]);
        ExcelWorkbook fileB = Workbook("b", ["FamilyId", "Composition"], ["30000001", "cotton"]);

        ExcelModelBuildResult result = builder.BuildFromWorkbooks([fileA, fileB]);
        var families = result.Model.ToFamilyRecords();

        Assert.Single(families);
        FamilyIDRecord record = families[0];
        Assert.True(record.CanonicalProperties.ContainsKey("color"));
        Assert.True(record.CanonicalProperties.ContainsKey("material"));
    }

    [Fact]
    public void ForeignLanguageColumn_CanonicalizedToEnglishId()
    {
        ModelBuilder builder = BuildBuilder();
        ExcelWorkbook workbook = Workbook("c1", ["Family ID", "Couleur"], ["40000001", "bleu"]);

        ExcelModelBuildResult result = builder.BuildFromWorkbooks([workbook]);
        FamilyIDRecord record = result.Model.ToFamilyRecords()[0];

        Assert.True(record.CanonicalProperties.ContainsKey("color"));
    }

    //  AUTOMAT2 transition: refco-style key column is identified, then rows fail PK validation

    [Fact]
    public void RefcoStyleKey_ColumnIdentified_RowsReportedInvalidNotHeaderMissing()
    {
        ModelBuilder builder = BuildBuilder();
        ExcelWorkbook workbook = Workbook("automat",
            ["REFERENCIA VEEPEE", "Color"],
            ["1234567890-01", "azul"],
            ["1234567890-02", "rojo"]);

        ExcelModelBuildResult result = builder.BuildFromWorkbooks([workbook]);

        Assert.Empty(result.Model.ToFamilyRecords());
        Assert.DoesNotContain(result.Diagnostics, d => d.ReasonCode == "excel.header_not_found");
        Assert.DoesNotContain(result.Diagnostics, d => d.ReasonCode == "excel.primary_key_column_not_found");
        Assert.Contains(result.Diagnostics, d => d.ReasonCode == "excel.invalid_primary_key");
    }

    //  Builders

    private static ModelBuilder BuildBuilder()
    {
        return new ModelBuilder(BuildExcelConfig(), BuildTranslationConfig());
    }

    private static ExcelWorkbook Workbook(string name, params string[][] rows)
    {
        var worksheetRows = rows
            .Select((cells, index) => new ExcelWorksheetRow(index, cells))
            .ToList();
        ExcelWorksheet worksheet = new($"{name}.xlsx", name, worksheetRows);
        return new ExcelWorkbook($"{name}.xlsx", [worksheet]);
    }

    private static ExcelConfig BuildExcelConfig()
    {
        return new ExcelConfig
        {
            RecordPrimaryKey = "FamilyID",
            HeaderRowIndicators =
            [
                "familyid", "ean", "refco", "model", "color", "material", "description",
                "producttype", "category", "brand", "gender", "season", "size", "weight",
                "style", "washinginstructions", "ngp", "label", "sku"
            ],
            HeaderRowSearchSpace = new HeaderRowSearchSpace { FirstRow = 0, LastRow = 20, FirstColumn = 0, LastColumn = 20 },
            FamilyIDProperties = new FamilyIdProperties { IsNumeric = true, Length = 8 },
            HeaderDetection = new HeaderDetectionConfig
            {
                MinimumMatchedColumnRatio = 0.4,
                MaximumEditDistanceRatio = 0.12,
                EditDistanceOneConfidence = 0.75,
                EditDistanceTwoConfidence = 0.5
            },
            ColumnValidity = new ColumnValidityConfig { MinimumUsefulValueRatio = 0.2 },
            DuplicateColumnHandling = new DuplicateColumnHandlingConfig { OverlapRatioForMerge = 0.2 },
            ColumnClassification = new ColumnClassificationConfig { CategoricalMaximumUniqueValues = 100, CategoricalMaximumValueLength = 20 }
        };
    }

    private static TranslationConfig BuildTranslationConfig()
    {
        return new TranslationConfig
        {
            HeaderGroups =
            [
                new HeaderGroup { Id = "familyid", Terms = ["familyid", "family", "famille", "veepee"] },
                new HeaderGroup { Id = "ean", Terms = ["ean", "barcode"] },
                new HeaderGroup { Id = "refco", Terms = ["refco", "reference", "referencia"] },
                new HeaderGroup { Id = "model", Terms = ["modelo", "model"] },
                new HeaderGroup { Id = "color", Terms = ["color", "colour", "couleur"] },
                new HeaderGroup { Id = "material", Terms = ["material", "composition", "composicion"] },
                new HeaderGroup { Id = "description", Terms = ["description", "descripcion", "designation"] },
                new HeaderGroup { Id = "producttype", Terms = ["type", "tipo"] },
                new HeaderGroup { Id = "category", Terms = ["category", "categorie", "seccion"] },
                new HeaderGroup { Id = "washinginstructions", Terms = ["cuidados", "entretien", "conseils", "washing"] },
                new HeaderGroup { Id = "ngp", Terms = ["ngp"] }
            ],
            StopWords = new StopWordConfig
            {
                General = ["de", "la", "le", "les", "of", "the", "and", "a"],
                Domain = ["color", "style", "size", "model", "product"]
            }
        };
    }
}
