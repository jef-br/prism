using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace PrismCoreTests.Ingest;

/// <summary>
/// Shared fixture for the Ingest suite. Loads the real Prism_Config.json and ExcelConfig.json once
/// per test class (same PrismConfigLocator resolution production uses), owns a per-run temp root that
/// doubles as job temp root and fixture-image folder, and provides deterministic image factories.
/// Generated images are filled with seeded noise: noise defeats JPEG/PNG compression, so even small
/// canvases stay above the configured Input.Images.filesize.min without shipping binary fixtures.
/// </summary>
public sealed class ImporterFixture : IDisposable {
    private readonly ModelBuilder modelBuilder;

    public PrismConfiguration Configuration { get; }

    /// <summary>Directory holding Prism_Config.json and its sibling config files (HostRules.json, ExcelConfig.json).</summary>
    public string ConfigDirectory { get; }

    /// <summary>Per-run temp root; used as the Importer's jobTempRoot and deleted on dispose.</summary>
    public string TempRoot { get; }

    /// <summary>The committed CiMini Excel fixture, resolved via the same walk PipelineFixture uses.</summary>
    public string CiMiniExcelPath { get; }

    public ImporterFixture() {
        string configPath = PrismConfigLocator.FindPrismConfigPath()
            ?? throw new InvalidOperationException("Prism_Config.json not found — the Ingest suite needs the deployed config next to the test assembly.");

        Configuration   = PrismConfiguration.LoadPrismConfig(configPath);
        ConfigDirectory = Path.GetDirectoryName(configPath)!;
        modelBuilder    = ModelBuilder.FromConfigFile(Path.Combine(ConfigDirectory, "ExcelConfig.json"));
        TempRoot        = Path.Combine(Path.GetTempPath(), $"PRISM-INGEST-TESTS-{Guid.NewGuid():N}");
        Directory.CreateDirectory(TempRoot);
        CiMiniExcelPath = Path.Combine(PipelineFixture.ResolveTestFixturePath(), "CiMini", "ci-mini.xlsx");
    }

    public Importer NewImporter() => new(Configuration, modelBuilder);

    /// <summary>Runs one import job with a fresh job id against this fixture's temp root.</summary>
    public ImportStageResult RunImport(IReadOnlyList<ImageRecord_INPUT> images, IReadOnlyList<InputExcelFileRecord>? excelRecords = null, IReadOnlyList<InputZipFileRecord>? zipRecords = null) {
        return NewImporter().Run(Guid.NewGuid(), images, excelRecords ?? [], zipRecords ?? [], TempRoot);
    }

    public string WriteNoiseJpeg(string fileName, int width, int height) {
        using Image<Rgba32> noise = NewNoiseImage(width, height);
        string path = Path.Combine(TempRoot, fileName);
        noise.SaveAsJpeg(path, new JpegEncoder { Quality = 92 });
        return path;
    }

    /// <summary>Noise JPEG tagged EXIF orientation 6 (rotate 90° CW) — AutoOrient must swap the axes on import.</summary>
    public string WriteExifRotatedJpeg(string fileName, int width, int height) {
        using Image<Rgba32> noise = NewNoiseImage(width, height);
        ExifProfile exif = new();
        exif.SetValue(ExifTag.Orientation, (ushort)6);
        noise.Metadata.ExifProfile = exif;
        string path = Path.Combine(TempRoot, fileName);
        noise.SaveAsJpeg(path, new JpegEncoder { Quality = 92 });
        return path;
    }

    /// <summary>PNG whose left half is opaque noise and right half fully transparent — import must flatten alpha to white.</summary>
    public string WriteHalfTransparentPng(string fileName, int width, int height) {
        using Image<Rgba32> png = NewNoiseImage(width, height);
        for (int y = 0; y < height; y++) {
            for (int x = width / 2; x < width; x++) {
                png[x, y] = new Rgba32(0, 0, 0, 0);
            }
        }
        string path = Path.Combine(TempRoot, fileName);
        png.SaveAsPng(path);
        return path;
    }

    public string WriteBytes(string fileName, byte[] bytes) {
        string path = Path.Combine(TempRoot, fileName);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>A copy of the CiMini Excel under a caller-chosen name, so the original never gets locked.</summary>
    public string CopyCiMiniExcel(string fileName) {
        string path = Path.Combine(TempRoot, fileName);
        File.Copy(CiMiniExcelPath, path, overwrite: true);
        return path;
    }

    private static Image<Rgba32> NewNoiseImage(int width, int height) {
        Random rng = new(421);
        Image<Rgba32> image = new(width, height);
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                image[x, y] = new Rgba32((byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256));
            }
        }
        return image;
    }

    public void Dispose() {
        try {
            Directory.Delete(TempRoot, recursive: true);
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }
}
