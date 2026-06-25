using Xunit;

namespace PrismCoreTests;

/// <summary>
/// End-to-end integration tests for the full PRISM pipeline.
/// These tests exercise the complete pipeline from request to result,
/// validating stage order, manifest shape, and real-data output quality
/// against the SPACINI29/TINY fixture dataset.
/// </summary>
public class PipelineIntegrationTests
{
    private static readonly string TestFixturePath = ResolveTestFixturePath();

    // -------------------------------------------------------------------------
    // Smoke tests (stage order + manifest shape)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Primary acceptance test: all 8 stages present in order, manifest non-empty.
    /// </summary>
    [Fact]
    public async Task SPACINI29_TINY_EndToEnd_VerifiesAllEightStagesInOrder()
    {
        string tinyImagesPath = Path.Combine(TestFixturePath, "SPACINI29", "TINY");
        string excelPath = Path.Combine(TestFixturePath, "SPACINI29", "SPACINI29-INPUTS.xlsx");

        Assert.True(Directory.Exists(tinyImagesPath), $"Test fixture directory not found: {tinyImagesPath}");
        Assert.True(File.Exists(excelPath), $"Test fixture Excel file not found: {excelPath}");
        Assert.NotEmpty(Directory.GetFiles(tinyImagesPath, "*.jpg", SearchOption.TopDirectoryOnly));

        var result = await new PrismService().Process(BuildTinyJobRequest());

        Assert.NotNull(result);
        Assert.Equal("Completed", result.Status);
        Assert.NotNull(result.Manifest);

        var manifest = result.Manifest;
        Assert.NotNull(manifest.RouteSummaries);

        var expectedStages = new[]
        {
            "Imported", "Classified", "Matched", "Ordered",
            "Renamed", "Generated", "Transformed", "Exported"
        };

        Assert.True(manifest.RouteSummaries.Count == 8,
            $"Expected 8 route summaries, got {manifest.RouteSummaries.Count}: {string.Join(", ", manifest.RouteSummaries)}");

        for (int i = 0; i < expectedStages.Length; i++)
        {
            Assert.Contains(expectedStages[i], manifest.RouteSummaries[i]);
        }

        Assert.NotNull(manifest.Summary);
        Assert.True(manifest.Summary.ImageCount > 0,
            $"Expected ImageCount > 0, got {manifest.Summary.ImageCount}");
    }

    /// <summary>
    /// Verifies the pipeline accepts minimal valid input without throwing.
    /// </summary>
    [Fact]
    public async Task PrismJobRequest_WithMinimalInput_AcceptsJob()
    {
        string tinyImagesPath = Path.Combine(TestFixturePath, "SPACINI29", "TINY");
        string excelPath = CopyExcelToTemp(Path.Combine(TestFixturePath, "SPACINI29", "SPACINI29-INPUTS.xlsx"));

        var singleImageRecord = Directory.GetFiles(tinyImagesPath, "*.jpg")
            .Take(1)
            .Select(f => new ImageRecord_INPUT { InitialFullName = Path.GetFileName(f), TempFilePath = f })
            .ToList();

        var jobRequest = new PrismJobRequest
        {
            JobID          = Guid.NewGuid(),
            ImageRecords   = singleImageRecord,
            ExcelRecords   = [new InputExcelFileRecord { SourceReference = excelPath }],
            ZipFileRecords = [],
            PrismProcessingParameters = new PrismProcessingParameters { Format = "json" }
        };

        var result = await new PrismService().Process(jobRequest);

        Assert.NotNull(result);
        Assert.NotNull(result.Manifest);
    }

    /// <summary>
    /// Verifies a completed job always has non-empty RouteSummaries.
    /// </summary>
    [Fact]
    public async Task BatchManifest_AlwaysContainsRouteSummaries()
    {
        var result = await new PrismService().Process(BuildTinyJobRequest());

        Assert.NotNull(result.Manifest);
        Assert.NotNull(result.Manifest.RouteSummaries);
        Assert.NotEmpty(result.Manifest.RouteSummaries);
        Assert.All(result.Manifest.RouteSummaries, s => Assert.False(string.IsNullOrWhiteSpace(s)));
    }

    /// <summary>
    /// Documents the definitive 8-stage order and validates its uniqueness.
    /// </summary>
    [Fact]
    public void ValidateExpectedStageOrder()
    {
        var expectedStageOrder = new[]
        {
            "Imported", "Classified", "Matched", "Ordered",
            "Renamed", "Generated", "Transformed", "Exported"
        };

        Assert.Equal(8, expectedStageOrder.Length);
        Assert.Equal(expectedStageOrder.Length, new HashSet<string>(expectedStageOrder).Count);
    }

    // -------------------------------------------------------------------------
    // Real-data quality tests (SPACINI29/TINY)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Every input image must appear in either OkImages or KoImages — no silent drops.
    /// </summary>
    [Fact]
    public async Task SPACINI29_TINY_NoImagesSilentlyDropped()
    {
        int inputCount = Directory.GetFiles(
            Path.Combine(TestFixturePath, "SPACINI29", "TINY"), "*.jpg",
            SearchOption.TopDirectoryOnly).Length;

        var result = await new PrismService().Process(BuildTinyJobRequest());

        Assert.Equal("Completed", result.Status);
        Assert.Equal(inputCount, result.OkImages.Count + result.KoImages.Count);
    }

    /// <summary>
    /// Contract: any OK image must have a well-formed _det{n} filename with no duplicates.
    /// Vacuously satisfied when all images are KO'd; still guards regressions if matching starts producing OK images.
    /// </summary>
    [Fact]
    public async Task SPACINI29_TINY_OkImages_HaveWellFormedFinalNames()
    {
        var result = await new PrismService().Process(BuildTinyJobRequest());

        Assert.Equal("Completed", result.Status);

        Assert.All(result.OkImages, row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Output?.FinalFileName));
            Assert.Matches(@"_det\d+\.\w+$", row.Output!.FinalFileName!);
        });

        var finalNames = result.OkImages.Select(r => r.Output?.FinalFileName).ToList();
        Assert.Equal(finalNames.Count, finalNames.Distinct().Count());
    }

    /// <summary>
    /// Every KO image must have a documented rejection reason code — undocumented rejections are a pipeline defect.
    /// </summary>
    [Fact]
    public async Task SPACINI29_TINY_KoImages_HaveReasonCode()
    {
        var result = await new PrismService().Process(BuildTinyJobRequest());

        Assert.Equal("Completed", result.Status);
        Assert.All(result.KoImages, row =>
            Assert.False(string.IsNullOrWhiteSpace(row.KoReasonCode)));
    }

    /// <summary>
    /// Images sharing the same source stem (e.g. 20213024_46_A and 20213024_46_B)
    /// must resolve to the same FamilyId when both are OK.
    /// </summary>
    [Fact]
    public async Task SPACINI29_TINY_PairedImages_ShareFamily()
    {
        var result = await new PrismService().Process(BuildTinyJobRequest());

        Assert.Equal("Completed", result.Status);

        // Group OkImages by stem = filename minus the trailing _A / _B / _C view suffix.
        var byStem = result.OkImages
            .Where(r => r.SourceReference.Contains('_'))
            .GroupBy(r =>
            {
                var stem = Path.GetFileNameWithoutExtension(r.SourceReference);
                int last = stem.LastIndexOf('_');
                return last > 0 ? stem[..last] : stem;
            })
            .Where(g => g.Count() > 1);

        foreach (var group in byStem)
        {
            var families = group.Select(r => r.Output?.Family).Distinct().ToList();
            Assert.Single(families);
            Assert.False(string.IsNullOrWhiteSpace(families[0]));
        }
    }

    /// <summary>
    /// Requesting ZIP format must produce non-null, non-empty ZipBytes.
    /// </summary>
    [Fact]
    public async Task SPACINI29_TINY_ZipFormat_ProducesNonEmptyBytes()
    {
        var request = BuildTinyJobRequest(new PrismProcessingParameters
        {
            Format               = "zip",
            Transform            = true,
            Generation           = false,
            ReturnOriginalImages = false
        });

        var result = await new PrismService().Process(request);

        Assert.Equal("Completed", result.Status);
        Assert.NotNull(result.ZipBytes);
        Assert.True(result.ZipBytes!.Length > 0);
    }

    /// <summary>
    /// Non-vacuous guard: real OK rows must exist and carry a FamilyID. This is the assertion the other
    /// SPACINI29 tests lack — they are all satisfied when every image is KO. A classification (CLIP)
    /// failure must never KO an image, so filename-token matching can still assign a FamilyID.
    /// </summary>
    [Fact]
    public async Task SPACINI29_TINY_ImagesAreAssociatedToFamilyId()
    {
        var result = await new PrismService().Process(BuildTinyJobRequest());

        Assert.Equal("Completed", result.Status);
        Assert.NotEmpty(result.OkImages);

        int withFamily = result.OkImages.Count(r => !string.IsNullOrWhiteSpace(r.Output?.Family));
        Assert.True(withFamily > 0,
            $"Expected OK images associated to a FamilyID; got {withFamily} with a FamilyID of {result.OkImages.Count} OK and {result.KoImages.Count} KO.");

        // A CLIP failure must degrade gracefully, not KO the image.
        Assert.DoesNotContain(result.KoImages, r => r.KoReasonCode == "CLASSIFY_ERROR");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a job request using all images from SPACINI29/TINY and the SPACINI29 Excel.
    /// TempFilePath carries the full disk path so the Importer can read each file;
    /// InitialFullName keeps the bare filename so token matching works on real names.
    /// </summary>
    private static PrismJobRequest BuildTinyJobRequest(PrismProcessingParameters? parameters = null)
    {
        string tinyImagesPath = Path.Combine(TestFixturePath, "SPACINI29", "TINY");
        string excelPath      = CopyExcelToTemp(Path.Combine(TestFixturePath, "SPACINI29", "SPACINI29-INPUTS.xlsx"));

        var imageRecords = Directory.GetFiles(tinyImagesPath, "*.jpg", SearchOption.TopDirectoryOnly)
            .Select(f => new ImageRecord_INPUT { InitialFullName = Path.GetFileName(f), TempFilePath = f })
            .ToList();

        return new PrismJobRequest
        {
            JobID              = Guid.NewGuid(),
            ImageRecords       = imageRecords,
            ExcelRecords       = [new InputExcelFileRecord { SourceReference = excelPath, ByteLength = new FileInfo(excelPath).Length }],
            ZipFileRecords     = [],
            PrismProcessingParameters = parameters ?? new PrismProcessingParameters
            {
                Format               = "json",
                Transform            = true,
                Generation           = false,
                ReturnOriginalImages = false
            }
        };
    }

    /// <summary>
    /// Copies the fixture Excel to a unique temp file so the importer can read it even when the
    /// original is held open elsewhere (e.g. opened in Excel). The Importer reads from SourceReference.
    /// </summary>
    private static string CopyExcelToTemp(string excelPath)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"PRISM-TEST-{Guid.NewGuid():N}.xlsx");
        File.Copy(excelPath, tempPath, overwrite: true);
        return tempPath;
    }

    /// <summary>
    /// Resolves the test fixture path by walking up from the assembly location.
    /// </summary>
    private static string ResolveTestFixturePath()
    {
        var assemblyDir = new FileInfo(typeof(PipelineIntegrationTests).Assembly.Location).DirectoryName
            ?? throw new InvalidOperationException("Cannot determine assembly directory");

        var current = new DirectoryInfo(assemblyDir);
        while (current.Parent != null)
        {
            var candidate = Path.Combine(current.FullName, "jb", "Testing");
            if (Directory.Exists(candidate))
                return candidate;
            current = current.Parent;
        }

        var fallback = @"c:\Users\JefB\Documents\JBGITROOT\prism\jb\Testing";
        if (Directory.Exists(fallback))
            return fallback;

        throw new DirectoryNotFoundException(
            $"Test fixture directory 'jb/testing' not found. Started from: {assemblyDir}");
    }
}
