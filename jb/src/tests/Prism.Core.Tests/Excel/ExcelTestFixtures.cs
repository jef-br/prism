using System.Linq;

namespace PrismCoreTests.Excel;

/// <summary>
/// Shared builders for Excel unit tests: an in-memory <see cref="ModelBuilder"/> with a representative
/// config, and a helper that wraps rows of string cells into an <see cref="ExcelWorkbook"/>.
/// </summary>
internal static class ExcelTestFixtures {
    public static ModelBuilder BuildBuilder() {
        return new ModelBuilder(BuildExcelConfig(), BuildTranslationConfig());
    }

    public static ExcelWorkbook Workbook(string name, params string[][] rows) {
        var worksheetRows = rows
            .Select((cells, index) => new ExcelWorksheetRow(index, cells))
            .ToList();
        ExcelWorksheet worksheet = new($"{name}.xlsx", name, worksheetRows);
        return new ExcelWorkbook($"{name}.xlsx", [worksheet]);
    }

    public static ExcelConfig BuildExcelConfig() {
        return new ExcelConfig {
            RecordPrimaryKey = "FamilyID",
            HeaderRowIndicators =
            [
                "familyid", "ean", "refco", "model", "color", "material", "description",
                "producttype", "category", "brand", "gender", "season", "size", "weight",
                "style", "washinginstructions", "ngp", "label", "sku", "notes"
            ],
            HeaderRowSearchSpace = new HeaderRowSearchSpace { FirstRow = 0, LastRow = 20, FirstColumn = 0, LastColumn = 20 },
            FamilyIDProperties = new FamilyIdProperties { IsNumeric = true, Length = 8 },
            HeaderDetection = new HeaderDetectionConfig {
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

    public static TranslationConfig BuildTranslationConfig() {
        return new TranslationConfig {
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
                new HeaderGroup { Id = "notes", Terms = ["notes", "note", "remarks"] },
                new HeaderGroup { Id = "ngp", Terms = ["ngp"] }
            ],
            StopWords = new StopWordConfig {
                General = ["de", "la", "le", "les", "of", "the", "and", "a"],
                Domain = ["color", "style", "size", "model", "product"]
            }
        };
    }
}
