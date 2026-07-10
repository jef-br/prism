using Xunit;

namespace PrismCoreTests.Ingest;

/// <summary>
/// Tests for <see cref="Importer"/>'s direct Excel routing: accepted workbooks feed the IEM and
/// produce FamilyRecords; files with unaccepted extensions or missing paths are ignored rather
/// than KO'd (Excel-content failures surface as ExcelDiagnostics, not import KOs).
/// </summary>
public class ImporterExcelRoutingTests : IClassFixture<ImporterFixture> {
    private readonly ImporterFixture fixture;

    public ImporterExcelRoutingTests(ImporterFixture fixture) {
        this.fixture = fixture;
    }

    [Fact]
    public void CiMiniWorkbook_BuildsFamilyRecords() {
        string excelCopy = fixture.CopyCiMiniExcel("routing_ci-mini.xlsx");

        ImportStageResult result = fixture.RunImport([], excelRecords: [new InputExcelFileRecord {
            SourceReference = "ci-mini.xlsx",
            TempFilePath    = excelCopy
        }]);

        Assert.NotEmpty(result.FamilyRecords);
        Assert.All(result.FamilyRecords, family => Assert.False(string.IsNullOrWhiteSpace(family.FamilyID)));
    }

    [Fact]
    public void UnacceptedExtension_IsIgnored() {
        // Same workbook bytes under a non-Excel extension — must be silently skipped.
        string excelCopy = fixture.CopyCiMiniExcel("families.dat");

        ImportStageResult result = fixture.RunImport([], excelRecords: [new InputExcelFileRecord {
            SourceReference = "families.dat",
            TempFilePath    = excelCopy
        }]);

        Assert.Empty(result.FamilyRecords);
    }

    [Fact]
    public void MissingExcelFile_IsIgnoredWithoutThrowing() {
        ImportStageResult result = fixture.RunImport([], excelRecords: [new InputExcelFileRecord {
            SourceReference = "ghost.xlsx",
            TempFilePath    = Path.Combine(fixture.TempRoot, "ghost.xlsx")
        }]);

        Assert.Empty(result.FamilyRecords);
    }
}
