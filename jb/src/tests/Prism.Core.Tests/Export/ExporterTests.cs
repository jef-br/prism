using System.IO.Compression;
using Xunit;

namespace PrismCoreTests.Export;

/// <summary>
/// Unit tests for <see cref="Exporter"/> — both zip and JSON export modes.
/// Each test that needs a file on disk calls <see cref="WriteTempJpeg"/>.
/// All temp files are cleaned up in <see cref="Dispose"/>.
/// </summary>
public class ExporterTests : IDisposable
{
    private readonly string tempDir = Path.Combine(
        Path.GetTempPath(), "PrismExporterTests_" + Guid.NewGuid().ToString("N"));

    public ExporterTests()
    {
        Directory.CreateDirectory(tempDir);
    }

    // ─── ZIP: manifest.json ────────────────────────────────────────────────────

    [Fact]
    public void Run_ZipFormat_ContainsManifestJson()
    {
        string imgPath = WriteTempJpeg("ok_img.jpg");
        PipelineContext context = MakeContext(
            [MakeInput("ok_img.jpg", imgPath)],
            [MakeLambda("ok_img.jpg", "FAM001", 0)],
            "zip");

        Exporter.Run(context, null!);

        using ZipArchive zip = new(new MemoryStream(context.ExportResult!.ZipBytes!));
        Assert.Contains(zip.Entries, e => e.FullName == "manifest.json");
    }

    // ─── ZIP: OK image in OK/ folder ──────────────────────────────────────────

    [Fact]
    public void Run_ZipFormat_OkImageAppearsInOkFolder()
    {
        string imgPath = WriteTempJpeg("ok_img.jpg");
        PipelineContext context = MakeContext(
            [MakeInput("ok_img.jpg", imgPath)],
            [MakeLambda("ok_img.jpg", "FAM001", 0)],
            "zip");

        Exporter.Run(context, null!);

        using ZipArchive zip = new(new MemoryStream(context.ExportResult!.ZipBytes!));
        Assert.Contains(zip.Entries, e => e.FullName == "OK/FAM001_det0.jpg");
    }

    // ─── ZIP: KO image in KO/ folder ──────────────────────────────────────────

    [Fact]
    public void Run_ZipFormat_KoImageAppearsInKoFolder()
    {
        string imgPath = WriteTempJpeg("ko_img.jpg");
        PipelineContext context = MakeContext(
            [MakeInput("ko_img.jpg", imgPath)],
            [MakeLambda("ko_img.jpg", "FAM001", 0, isKo: true)],
            "zip");

        Exporter.Run(context, null!);

        using ZipArchive zip = new(new MemoryStream(context.ExportResult!.ZipBytes!));
        Assert.Contains(zip.Entries, e => e.FullName == "KO/ko_img.jpg");
    }

    // ─── ZIP: Excel file included ─────────────────────────────────────────────

    [Fact]
    public void Run_ZipFormat_ExcelFileIncluded()
    {
        string imgPath = WriteTempJpeg("ok_img.jpg");
        string xlsPath = WriteTempFile("catalogue.xlsx", [0x50, 0x4B]);
        PipelineContext context = MakeContext(
            [MakeInput("ok_img.jpg", imgPath)],
            [MakeLambda("ok_img.jpg", "FAM001", 0)],
            "zip",
            excelPath: xlsPath);

        Exporter.Run(context, null!);

        using ZipArchive zip = new(new MemoryStream(context.ExportResult!.ZipBytes!));
        Assert.Contains(zip.Entries, e => e.FullName == "catalogue.xlsx");
    }

    // ─── ZIP: KO with no normalized jpg not added to KO/ ─────────────────────

    [Fact]
    public void Run_ZipFormat_KoWithNoNormalizedJpg_NotInZip()
    {
        PipelineContext context = MakeContext(
            [MakeInput("import_fail.jpg", normalizedPath: null)],
            [MakeLambda("import_fail.jpg", "FAM001", 0, isKo: true)],
            "zip");

        Exporter.Run(context, null!);

        using ZipArchive zip = new(new MemoryStream(context.ExportResult!.ZipBytes!));
        Assert.DoesNotContain(zip.Entries, e => e.FullName.StartsWith("KO/"));
    }

    // ─── ZIP: OutputRecord attached to OK lambda ──────────────────────────────

    [Fact]
    public void Run_ZipFormat_OutputRecordAttachedToOkLambda()
    {
        string imgPath = WriteTempJpeg("ok_img.jpg");
        ImageRecord_LAMBDA lambda = MakeLambda("ok_img.jpg", "FAM001", 0);
        PipelineContext context = MakeContext(
            [MakeInput("ok_img.jpg", imgPath)],
            [lambda],
            "zip");

        Exporter.Run(context, null!);

        Assert.NotNull(lambda.OutputRecord);
        Assert.Equal("FAM001_det0.jpg", lambda.OutputRecord!.FinalFileName);
    }

    // ─── JSON: ZipBytes is null ───────────────────────────────────────────────

    [Fact]
    public void Run_JsonFormat_ZipBytesNull()
    {
        PipelineContext context = MakeContext(
            [MakeInput("ok_img.jpg", null)],
            [MakeLambda("ok_img.jpg", "FAM001", 0)],
            "json");

        Exporter.Run(context, null!);

        Assert.Null(context.ExportResult!.ZipBytes);
    }

    // ─── JSON: ImageRows count matches lambda count ───────────────────────────

    [Fact]
    public void Run_JsonFormat_ManifestImageRowsMatchLambdaCount()
    {
        PipelineContext context = MakeContext(
            [MakeInput("a.jpg", null), MakeInput("b.jpg", null)],
            [MakeLambda("a.jpg", "FAM001", 0), MakeLambda("b.jpg", "FAM001", 1)],
            "json");

        Exporter.Run(context, null!);

        Assert.Equal(2, context.ExportResult!.FinalManifest!.ImageRows.Count);
    }

    // ─── JSON: OK row has FinalFileName ──────────────────────────────────────

    [Fact]
    public void Run_JsonFormat_OkRowHasFinalFileName()
    {
        PipelineContext context = MakeContext(
            [MakeInput("ok_img.jpg", null)],
            [MakeLambda("ok_img.jpg", "FAM001", 0)],
            "json");

        Exporter.Run(context, null!);

        ManifestImageRow row = context.ExportResult!.FinalManifest!.ImageRows[0];
        Assert.Equal("FAM001_det0.jpg", row.FinalFileName);
    }

    // ─── JSON: KO row has null FinalFileName and KoReasonCode set ────────────

    [Fact]
    public void Run_JsonFormat_KoRowHasNullFinalFileName()
    {
        PipelineContext context = MakeContext(
            [MakeInput("ko_img.jpg", null)],
            [MakeLambda("ko_img.jpg", "FAM001", 0, isKo: true, koCode: "MATCH_FAIL")],
            "json");

        Exporter.Run(context, null!);

        ManifestImageRow row = context.ExportResult!.FinalManifest!.ImageRows[0];
        Assert.Null(row.FinalFileName);
        Assert.Equal("MATCH_FAIL", row.KoReasonCode);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    public void Dispose()
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
    }

    private string WriteTempJpeg(string name) => WriteTempFile(name, MinimalJpegBytes);

    private string WriteTempFile(string name, byte[] bytes)
    {
        string path = Path.Combine(tempDir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static PipelineContext MakeContext(
        IReadOnlyList<ImageRecord_INPUT> inputs,
        IReadOnlyList<ImageRecord_LAMBDA> lambdas,
        string format,
        string? excelPath = null)
    {
        List<InputExcelFileRecord> excelRecords = excelPath is not null
            ? [new InputExcelFileRecord { SourceReference = Path.GetFileName(excelPath), TempFilePath = excelPath }]
            : [];

        PipelineContext context = new(
            Guid.NewGuid(),
            imageRecords:   inputs,
            excelRecords:   excelRecords,
            zipFileRecords: [],
            parameters:     new PrismProcessingParameters { Format = format },
            startedAt:      DateTimeOffset.UtcNow);

        foreach (ImageRecord_LAMBDA lambda in lambdas)
            context.LambdaRecords.Add(lambda);

        return context;
    }

    private static ImageRecord_INPUT MakeInput(string name, string? normalizedPath)
    {
        return new ImageRecord_INPUT
        {
            InitialFullName   = name,
            NormalizedJpgPath = normalizedPath,
            ImportStatus      = normalizedPath is not null ? ImportStatus.Ok : ImportStatus.Ko
        };
    }

    private static ImageRecord_LAMBDA MakeLambda(
        string name,
        string familyId,
        int detOrder,
        bool isKo = false,
        string? koCode = null)
    {
        return new ImageRecord_LAMBDA
        {
            InitialFullName = name,
            Family          = familyId,
            DetOrder        = detOrder,
            IsKo            = isKo,
            KoReasonCode    = koCode ?? (isKo ? "TEST_KO" : null)
        };
    }

    // Minimal valid JFIF JPEG: SOI + APP0 + EOI
    private static readonly byte[] MinimalJpegBytes =
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
        0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xD9
    ];
}
