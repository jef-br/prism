using System.IO.Compression;
using Xunit;

namespace PrismCoreTests;

/// <summary>
/// Runs each distinct pipeline configuration exactly once and caches the result, so the assertions in
/// PipelineIntegrationTests read cached output instead of re-running the full 8-stage pipeline per test.
///
/// Every test in that class previously called `new PrismService().Process(...)`, which reloads the 146 MB
/// CLIP model and the 37 MB YOLO model into fresh ONNX sessions before running any inference. Eleven tests
/// meant eleven full runs — 97% of the suite's wall clock — even though only three request shapes exist.
///
/// A single PrismService is shared across the three runs: the API registers it via AddSingleton and calls
/// Process once per job, so reuse here mirrors production rather than inventing a new lifecycle.
/// </summary>
public sealed class PipelineFixture : IAsyncLifetime {
    private readonly List<string> tempExcelCopies = [];
    private PrismService? prism;

    /// <summary>The shared PrismService instance — reuse this in other test classes (e.g. MatchLite
    /// tests) via <see cref="Xunit.IClassFixture{TFixture}"/> instead of constructing a fresh one,
    /// which would reload the 146 MB CLIP and 37 MB YOLO models.</summary>
    public PrismService Prism => prism!;

    /// <summary>Absolute path to test/datasets, resolved by walking up from the test assembly.</summary>
    public string FixturePath { get; } = ResolveTestFixturePath();

    private static readonly string[] ImageExtensions = [".jpg", ".png"];

    /// <summary>Directory holding the committed CiMini images and Brackets-Complete.xlsx.</summary>
    public string ImagesPath => Path.Combine(FixturePath, "CiMini");

    /// <summary>The committed CiMini Excel fixture, in its original (uncopied) location.</summary>
    public string ExcelPath => Path.Combine(ImagesPath, "Brackets-Complete.xlsx");

    /// <summary>The committed "3 images.zip" archive, submitted alongside the loose images by the Default and Zip runs.</summary>
    public string ZipPath => Path.Combine(ImagesPath, "3 images.zip");

    /// <summary>Count of images fed to the Default and Zip runs: every loose .jpg/.png (including subfolders) plus the zip archive's members. Minimal submits one loose image only and is not covered by this count.</summary>
    public int InputImageCount => AllImageFilePaths().Count + CountZipMembers();

    /// <summary>All CiMini images, JSON format, transform on. Shared by most CiMini_* tests.</summary>
    public PrismJobResult Default { get; private set; } = null!;

    /// <summary>Same inputs as <see cref="Default"/> but requesting ZIP output, so ZipBytes is populated.</summary>
    public PrismJobResult Zip { get; private set; } = null!;

    /// <summary>A single image plus the Excel — the minimal accepted job shape.</summary>
    public PrismJobResult Minimal { get; private set; } = null!;

    /// <summary>Loads the ONNX models once, then runs the three distinct pipeline configurations.</summary>
    public async Task InitializeAsync() {
        prism = new PrismService();
        Default = RequireCompleted(await prism.Process(BuildDefaultJobRequest()), "Default");
        Zip = RequireCompleted(await prism.Process(BuildZipJobRequest()), "Zip");
        Minimal = RequireCompleted(await prism.Process(BuildMinimalJobRequest()), "Minimal");
    }

    // A job that comes back Failed used to be handed to the tests as if it were a result, so all seven
    // CiMini assertions failed with "Expected Completed, Actual Failed" and the actual exception —
    // carried on FailureReason — was silently discarded. Fail here instead, with the reason attached, so
    // a broken run says what broke rather than making the next reader re-derive it.
    private static PrismJobResult RequireCompleted(PrismJobResult result, string configuration) {
        if (string.Equals(result.Status, "Completed", StringComparison.Ordinal)) return result;

        throw new InvalidOperationException(
            $"PipelineFixture '{configuration}' job did not complete. Status={result.Status}. " +
            $"FailureReason={result.FailureReason ?? "(none reported)"}");
    }

    /// <summary>Releases the ONNX sessions and removes the temp Excel copies made for each run.</summary>
    public Task DisposeAsync() {
        prism?.Dispose();

        foreach (string path in tempExcelCopies) {
            if (File.Exists(path)) File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>All loose CiMini images + the zip archive + the Excel, JSON output, transform enabled.</summary>
    private PrismJobRequest BuildDefaultJobRequest() {
        return BuildJobRequest(AllImageRecords(), AllZipRecords(), new PrismProcessingParameters {
            Format = "json",
            Transform = true,
            Generation = false,
            ReturnOriginalImages = false
        });
    }

    /// <summary>Identical inputs to the default run, but ZIP output so result.ZipBytes is produced.</summary>
    private PrismJobRequest BuildZipJobRequest() {
        return BuildJobRequest(AllImageRecords(), AllZipRecords(), new PrismProcessingParameters {
            Format = "zip",
            Transform = true,
            Generation = false,
            ReturnOriginalImages = false
        });
    }

    /// <summary>One image + the Excel — exercises the minimal-input acceptance path.</summary>
    private PrismJobRequest BuildMinimalJobRequest() {
        return BuildJobRequest(AllImageRecords().Take(1).ToList(), [], new PrismProcessingParameters { Format = "json" });
    }

    /// <summary>
    /// TempFilePath carries the full disk path so the Importer can read each file; InitialFullName carries
    /// the path relative to <see cref="ImagesPath"/> (forward slashes, matching the multipart/ZIP-entry
    /// convention) so subfolder images still expose a folder path to FolderNameEnricher.
    /// </summary>
    private List<ImageRecord_INPUT> AllImageRecords() {
        return AllImageFilePaths()
            .Select(f => new ImageRecord_INPUT {
                InitialFullName = Path.GetRelativePath(ImagesPath, f).Replace('\\', '/'),
                TempFilePath = f
            })
            .ToList();
    }

    // One unfiltered recursive scan, then filter — matches Get-PrismJobInputFiles' traversal (a single
    // Get-ChildItem -Recurse then a Where-Object) so the two paths submit images in the same order.
    // Per-extension Directory.GetFiles calls would enumerate every .jpg before any .png instead.
    private List<string> AllImageFilePaths() =>
        Directory.GetFiles(ImagesPath, "*", SearchOption.AllDirectories)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();

    /// <summary>The "3 images.zip" archive as a single zip input record.</summary>
    private List<InputZipFileRecord> AllZipRecords() =>
        [new InputZipFileRecord { SourceReference = Path.GetFileName(ZipPath), TempFilePath = ZipPath }];

    private int CountZipMembers() {
        using ZipArchive archive = ZipFile.OpenRead(ZipPath);
        return archive.Entries.Count;
    }

    private PrismJobRequest BuildJobRequest(List<ImageRecord_INPUT> imageRecords, List<InputZipFileRecord> zipRecords, PrismProcessingParameters parameters) {
        string excelPath = CopyExcelToTemp();

        return new PrismJobRequest {
            JobID = Guid.NewGuid(),
            ImageRecords = imageRecords,
            ExcelRecords = [new InputExcelFileRecord { SourceReference = excelPath, ByteLength = new FileInfo(excelPath).Length }],
            ZipFileRecords = zipRecords,
            PrismProcessingParameters = parameters
        };
    }

    /// <summary>
    /// Copies the fixture Excel to a unique temp file so the importer can read it even when the original is
    /// held open elsewhere (e.g. opened in Excel). The Importer reads from SourceReference.
    /// Public so other test classes sharing this fixture (e.g. MatchLite tests) can request their own copy.
    /// </summary>
    public string CopyExcelToTemp() {
        string tempPath = Path.Combine(Path.GetTempPath(), $"PRISM-TEST-{Guid.NewGuid():N}.xlsx");
        File.Copy(ExcelPath, tempPath, overwrite: true);
        tempExcelCopies.Add(tempPath);
        return tempPath;
    }

    /// <summary>
    /// Walks up from the test assembly to the repo's test/datasets folder, identified by the committed CiMini
    /// fixture. No hardcoded absolute path, so it resolves on any checkout (CI runner included).
    /// Throws rather than returning null: CiMini is committed, so a missing fixture is a real failure.
    /// </summary>
    public static string ResolveTestFixturePath() {
        string assemblyDir = new FileInfo(typeof(PipelineFixture).Assembly.Location).DirectoryName
            ?? throw new InvalidOperationException("Cannot determine assembly directory");

        for (DirectoryInfo? current = new(assemblyDir); current is not null; current = current.Parent) {
            string candidate = Path.Combine(current.FullName, "test", "datasets");
            if (Directory.Exists(Path.Combine(candidate, "CiMini"))) return candidate;
        }

        throw new DirectoryNotFoundException(
            $"Test fixture directory 'test/datasets' (with CiMini) not found walking up from: {assemblyDir}");
    }
}
