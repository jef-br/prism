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

    /// <summary>Absolute path to test/datasets, resolved by walking up from the test assembly.</summary>
    public string FixturePath { get; } = ResolveTestFixturePath();

    /// <summary>Directory holding the committed CiMini images and ci-mini.xlsx.</summary>
    public string ImagesPath => Path.Combine(FixturePath, "CiMini");

    /// <summary>The committed CiMini Excel fixture, in its original (uncopied) location.</summary>
    public string ExcelPath => Path.Combine(ImagesPath, "ci-mini.xlsx");

    /// <summary>Count of loose input .jpg files fed to the Default and Zip runs.</summary>
    public int InputImageCount => Directory.GetFiles(ImagesPath, "*.jpg", SearchOption.TopDirectoryOnly).Length;

    /// <summary>All CiMini images, JSON format, transform on. Shared by most CiMini_* tests.</summary>
    public PrismJobResult Default { get; private set; } = null!;

    /// <summary>Same inputs as <see cref="Default"/> but requesting ZIP output, so ZipBytes is populated.</summary>
    public PrismJobResult Zip { get; private set; } = null!;

    /// <summary>A single image plus the Excel — the minimal accepted job shape.</summary>
    public PrismJobResult Minimal { get; private set; } = null!;

    /// <summary>Loads the ONNX models once, then runs the three distinct pipeline configurations.</summary>
    public async Task InitializeAsync() {
        prism = new PrismService();
        Default = await prism.Process(BuildDefaultJobRequest());
        Zip     = await prism.Process(BuildZipJobRequest());
        Minimal = await prism.Process(BuildMinimalJobRequest());
    }

    /// <summary>Releases the ONNX sessions and removes the temp Excel copies made for each run.</summary>
    public Task DisposeAsync() {
        prism?.Dispose();

        foreach (string path in tempExcelCopies) {
            if (File.Exists(path)) File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>All loose CiMini images + the Excel, JSON output, transform enabled.</summary>
    private PrismJobRequest BuildDefaultJobRequest() {
        return BuildJobRequest(AllImageRecords(), new PrismProcessingParameters {
            Format = "json",
            Transform = true,
            Generation = false,
            ReturnOriginalImages = false
        });
    }

    /// <summary>Identical inputs to the default run, but ZIP output so result.ZipBytes is produced.</summary>
    private PrismJobRequest BuildZipJobRequest() {
        return BuildJobRequest(AllImageRecords(), new PrismProcessingParameters {
            Format = "zip",
            Transform = true,
            Generation = false,
            ReturnOriginalImages = false
        });
    }

    /// <summary>One image + the Excel — exercises the minimal-input acceptance path.</summary>
    private PrismJobRequest BuildMinimalJobRequest() {
        return BuildJobRequest(AllImageRecords().Take(1).ToList(), new PrismProcessingParameters { Format = "json" });
    }

    /// <summary>
    /// TempFilePath carries the full disk path so the Importer can read each file; InitialFullName keeps the
    /// bare filename so token matching works on real names.
    /// </summary>
    private List<ImageRecord_INPUT> AllImageRecords() {
        return Directory.GetFiles(ImagesPath, "*.jpg", SearchOption.TopDirectoryOnly)
            .Select(f => new ImageRecord_INPUT { InitialFullName = Path.GetFileName(f), TempFilePath = f })
            .ToList();
    }

    private PrismJobRequest BuildJobRequest( List<ImageRecord_INPUT> imageRecords, PrismProcessingParameters parameters ) {
        string excelPath = CopyExcelToTemp();

        return new PrismJobRequest {
            JobID = Guid.NewGuid(),
            ImageRecords = imageRecords,
            ExcelRecords = [new InputExcelFileRecord { SourceReference = excelPath, ByteLength = new FileInfo(excelPath).Length }],
            ZipFileRecords = [],
            PrismProcessingParameters = parameters
        };
    }

    /// <summary>
    /// Copies the fixture Excel to a unique temp file so the importer can read it even when the original is
    /// held open elsewhere (e.g. opened in Excel). The Importer reads from SourceReference.
    /// </summary>
    private string CopyExcelToTemp() {
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
    internal static string ResolveTestFixturePath() {
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
