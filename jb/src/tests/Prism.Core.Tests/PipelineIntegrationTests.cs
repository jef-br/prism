using Xunit;

namespace PrismCoreTests;

/// <summary>
/// End-to-end integration tests for the full PRISM pipeline.
/// These tests exercise the complete pipeline from request to result,
/// validating stage order and manifest shape.
/// </summary>
public class PipelineIntegrationTests
{
    private static readonly string TestFixturePath = ResolveTestFixturePath();

    /// <summary>
    /// Primary acceptance test: all 8 stages present in order, manifest non-empty.
    /// </summary>
    [Fact]
    public async Task SPACINI29_TINY_EndToEnd_VerifiesAllEightStagesInOrder()
    {
        // Arrange
        string tinyImagesPath = Path.Combine(TestFixturePath, "SPACINI29", "TINY");
        string excelPath = Path.Combine(TestFixturePath, "SPACINI29", "SPACINI29-INPUTS.xlsx");

        Assert.True(Directory.Exists(tinyImagesPath), $"Test fixture directory not found: {tinyImagesPath}");
        Assert.True(File.Exists(excelPath), $"Test fixture Excel file not found: {excelPath}");

        var imageFiles = Directory.GetFiles(tinyImagesPath, "*.jpg", SearchOption.TopDirectoryOnly);
        Assert.NotEmpty(imageFiles);

        var imageRecords = imageFiles
            .Select(f => new ImageRecord_INPUT { InitialFullName = Path.GetFileName(f) })
            .ToList();

        var excelRecords = new List<InputExcelFileRecord>
        {
            new() { SourceReference = excelPath, ByteLength = new FileInfo(excelPath).Length }
        };

        var jobRequest = new PrismJobRequest
        {
            JobID = Guid.NewGuid(),
            ClientRequestToken = "test-token-001",
            ImageRecords = imageRecords,
            ExcelRecords = excelRecords,
            ZipFileRecords = [],
            PrismProcessingParameters = new PrismProcessingParameters
            {
                Format = "json",
                Transform = true,
                Generation = false,
                ReturnOriginalImages = false
            }
        };

        // Act
        var prism = new Prism();
        var result = await prism.Process(jobRequest);

        // Assert
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
        string excelPath = Path.Combine(TestFixturePath, "SPACINI29", "SPACINI29-INPUTS.xlsx");

        var imageRecords = Directory.GetFiles(tinyImagesPath, "*.jpg")
            .Take(1)
            .Select(f => new ImageRecord_INPUT { InitialFullName = Path.GetFileName(f) })
            .ToList();

        var jobRequest = new PrismJobRequest
        {
            JobID = Guid.NewGuid(),
            ImageRecords = imageRecords,
            ExcelRecords = [new InputExcelFileRecord { SourceReference = excelPath }],
            ZipFileRecords = [],
            PrismProcessingParameters = new PrismProcessingParameters { Format = "json" }
        };

        var prism = new Prism();
        var result = await prism.Process(jobRequest);

        Assert.NotNull(result);
        Assert.NotNull(result.Manifest);
    }

    /// <summary>
    /// Verifies a completed job always has non-empty RouteSummaries.
    /// </summary>
    [Fact]
    public async Task BatchManifest_AlwaysContainsRouteSummaries()
    {
        string tinyImagesPath = Path.Combine(TestFixturePath, "SPACINI29", "TINY");
        string excelPath = Path.Combine(TestFixturePath, "SPACINI29", "SPACINI29-INPUTS.xlsx");

        var imageRecords = Directory.GetFiles(tinyImagesPath, "*.jpg")
            .Select(f => new ImageRecord_INPUT { InitialFullName = Path.GetFileName(f) })
            .ToList();

        var jobRequest = new PrismJobRequest
        {
            JobID = Guid.NewGuid(),
            ImageRecords = imageRecords,
            ExcelRecords = [new InputExcelFileRecord { SourceReference = excelPath }],
            ZipFileRecords = [],
            PrismProcessingParameters = new PrismProcessingParameters { Format = "json" }
        };

        var prism = new Prism();
        var result = await prism.Process(jobRequest);

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
    // Helpers
    // -------------------------------------------------------------------------

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
            $"Test fixture directory 'jb/Testing' not found. Started from: {assemblyDir}");
    }
}
