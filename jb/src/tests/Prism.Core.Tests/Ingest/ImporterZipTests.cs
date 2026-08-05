using System.IO.Compression;
using Xunit;

namespace PrismCoreTests.Ingest;

/// <summary>
/// Tests for <see cref="Importer"/>'s ZIP ingestion path: archives are expanded through the
/// Prism.Lib.Zip triage, image members are normalized like direct inputs, Excel members feed the
/// IEM, and malformed members KO without stopping the batch.
/// </summary>
public class ImporterZipTests : IClassFixture<ImporterFixture> {
    private readonly ImporterFixture fixture;

    public ImporterZipTests(ImporterFixture fixture) {
        this.fixture = fixture;
    }

    [Fact]
    public void ZipWithImagesAndExcel_NormalizesMembersAndBuildsFamilies() {
        string img1 = fixture.WriteNoiseJpeg("zip_member_1.jpg", 600, 600);
        string img2 = fixture.WriteNoiseJpeg("zip_member_2.jpg", 600, 600);
        string zipPath = BuildZip("batch.zip", [(img1, "zip_member_1.jpg"), (img2, "zip_member_2.jpg"), (fixture.CiMiniExcelPath, "ci-mini.xlsx")]);

        ImportStageResult result = fixture.RunImport([], zipRecords: [new InputZipFileRecord { SourceReference = "batch.zip", TempFilePath = zipPath }]);

        Assert.Equal(2, result.NormalizedImages.Count);
        Assert.All(result.NormalizedImages, r => Assert.Equal(ImageSourceKind.ZipMember, r.SourceKind));
        Assert.All(result.NormalizedImages, r => Assert.True(File.Exists(r.NormalizedJpgPath)));
        Assert.NotEmpty(result.FamilyRecords);
        Assert.Empty(result.ImageKoRecords);
    }

    [Fact]
    public void ZipWithCorruptImageMember_RecordsKoAndContinues() {
        string good = fixture.WriteNoiseJpeg("zip_good.jpg", 600, 600);
        byte[] garbage = new byte[4096];
        new Random(23).NextBytes(garbage);
        string bad = fixture.WriteBytes("zip_bad_bytes.jpg", garbage);
        string zipPath = BuildZip("mixed.zip", [(good, "zip_good.jpg"), (bad, "zip_bad.jpg")]);

        ImportStageResult result = fixture.RunImport([], zipRecords: [new InputZipFileRecord { SourceReference = "mixed.zip", TempFilePath = zipPath }]);

        // The corrupt member KOs either at zip triage (header probe) or at normalization —
        // both are valid; what matters is exactly one KO and the good member still lands.
        Assert.Single(result.NormalizedImages);
        Assert.Equal(1, result.ImageKoRecords.Count + result.ZipKoRecords.Count);
    }

    [Fact]
    public void ZipMemberInSubfolder_InitialFullNamePreservesFolderPath() {
        string img = fixture.WriteNoiseJpeg("1.jpg", 600, 600);
        string zipPath = BuildZip("folders.zip", [(img, "26182-Denim-801/1.jpg")]);

        ImportStageResult result = fixture.RunImport([], zipRecords: [new InputZipFileRecord { SourceReference = "folders.zip", TempFilePath = zipPath }]);

        Assert.Single(result.NormalizedImages);
        Assert.Equal("26182-Denim-801/1.jpg", result.NormalizedImages[0].InitialFullName);
    }

    [Fact]
    public void ZipMissingOnDisk_IsSkippedWithoutThrowing() {
        ImportStageResult result = fixture.RunImport([], zipRecords: [new InputZipFileRecord {
            SourceReference = "ghost.zip",
            TempFilePath    = Path.Combine(fixture.TempRoot, "ghost.zip")
        }]);

        Assert.Empty(result.NormalizedImages);
        Assert.Empty(result.ImageKoRecords);
        Assert.Empty(result.ZipKoRecords);
    }

    //  Helpers

    /// <summary>Builds a zip in the fixture temp root from (sourcePath, entryName) pairs.</summary>
    private string BuildZip(string zipFileName, (string SourcePath, string EntryName)[] members) {
        string zipPath = Path.Combine(fixture.TempRoot, zipFileName);
        using FileStream zipStream = new(zipPath, FileMode.Create, FileAccess.Write);
        using ZipArchive archive = new(zipStream, ZipArchiveMode.Create);
        foreach ((string sourcePath, string entryName) in members) {
            archive.CreateEntryFromFile(sourcePath, entryName);
        }
        return zipPath;
    }
}
