using Xunit;

namespace PrismCoreTests;

/// <summary>
/// End-to-end integration tests for the full PRISM pipeline.
/// These tests exercise the complete pipeline from request to result,
/// validating stage order, manifest shape, and real-data output quality
/// against the test/datasets/CiMini committed fixture dataset.
///
/// The pipeline runs live in <see cref="PipelineFixture"/> — three runs shared across every test here.
/// Assertions read cached results; nothing in this class starts a pipeline.
/// </summary>
// Shares a collection with TxConfigureGateTests: the fixture's pipeline run calls
// Tx_util_BgStretch.Configure/Tx_LowContrastEnhancement.Configure on shared static state,
// which would race the gate tests' reset-then-assert-throws sequence if run in parallel.
[Collection("TxStaticConfig")]
public class PipelineIntegrationTests : IClassFixture<PipelineFixture> {
    private readonly PipelineFixture fixture;

    public PipelineIntegrationTests( PipelineFixture fixture ) {
        this.fixture = fixture;
    }

    // -------------------------------------------------------------------------
    // Smoke tests (stage order + manifest shape)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Primary acceptance test: all 8 stages present in order, manifest non-empty.
    /// </summary>
    [Fact]
    public void CiMini_EndToEnd_VerifiesAllEightStagesInOrder() {
        Assert.True(Directory.Exists(fixture.ImagesPath), $"Test fixture directory not found: {fixture.ImagesPath}");
        Assert.True(File.Exists(fixture.ExcelPath), $"Test fixture Excel file not found: {fixture.ExcelPath}");
        Assert.NotEmpty(Directory.GetFiles(fixture.ImagesPath, "*.jpg", SearchOption.TopDirectoryOnly));

        var result = fixture.Default;

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

        for (int i = 0; i < expectedStages.Length; i++) {
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
    public void PrismJobRequest_WithMinimalInput_AcceptsJob() {
        var result = fixture.Minimal;

        Assert.NotNull(result);
        Assert.NotNull(result.Manifest);
    }

    /// <summary>
    /// Verifies a completed job always has non-empty RouteSummaries.
    /// </summary>
    [Fact]
    public void BatchManifest_AlwaysContainsRouteSummaries() {
        var result = fixture.Default;

        Assert.NotNull(result.Manifest);
        Assert.NotNull(result.Manifest.RouteSummaries);
        Assert.NotEmpty(result.Manifest.RouteSummaries);
        Assert.All(result.Manifest.RouteSummaries, s => Assert.False(string.IsNullOrWhiteSpace(s)));
    }

    /// <summary>
    /// Documents the definitive 8-stage order and validates its uniqueness.
    /// </summary>
    [Fact]
    public void ValidateExpectedStageOrder() {
        var expectedStageOrder = new[]
        {
            "Imported", "Classified", "Matched", "Ordered",
            "Renamed", "Generated", "Transformed", "Exported"
        };

        Assert.Equal(8, expectedStageOrder.Length);
        Assert.Equal(expectedStageOrder.Length, new HashSet<string>(expectedStageOrder).Count);
    }

    // -------------------------------------------------------------------------
    // Real-data quality tests (test/datasets/CiMini)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Every input image must appear in either OkImages or KoImages — no silent drops.
    /// </summary>
    [Fact]
    public void CiMini_NoImagesSilentlyDropped() {
        var result = fixture.Default;

        Assert.Equal("Completed", result.Status);
        Assert.Equal(fixture.InputImageCount, result.OkImages.Count + result.KoImages.Count);
    }

    /// <summary>
    /// Contract: any OK image must have a well-formed _det{n} filename with no duplicates.
    /// Vacuously satisfied when all images are KO'd; still guards regressions if matching starts producing OK images.
    /// </summary>
    [Fact]
    public void CiMini_OkImages_HaveWellFormedFinalNames() {
        var result = fixture.Default;

        Assert.Equal("Completed", result.Status);

        Assert.All(result.OkImages, row => {
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
    public void CiMini_KoImages_HaveReasonCode() {
        var result = fixture.Default;

        Assert.Equal("Completed", result.Status);
        Assert.All(result.KoImages, row =>
            Assert.False(string.IsNullOrWhiteSpace(row.KoReasonCode)));
    }

    /// <summary>
    /// Images sharing the same source stem (e.g. 2021_3024_46_A and 2021_3024_46_B)
    /// must resolve to the same FamilyId when both are OK.
    /// </summary>
    [Fact]
    public void CiMini_PairedImages_ShareFamily() {
        var result = fixture.Default;

        Assert.Equal("Completed", result.Status);

        // Group OkImages by stem = filename minus the trailing _A / _B / _C view suffix.
        var byStem = result.OkImages
            .Where(r => r.SourceReference.Contains('_'))
            .GroupBy(r => {
                var stem = Path.GetFileNameWithoutExtension(r.SourceReference);
                int last = stem.LastIndexOf('_');
                return last > 0 ? stem[..last] : stem;
            })
            .Where(g => g.Count() > 1);

        foreach (var group in byStem) {
            var families = group.Select(r => r.Output?.Family).Distinct().ToList();
            Assert.Single(families);
            Assert.False(string.IsNullOrWhiteSpace(families[0]));
        }
    }

    /// <summary>
    /// Requesting ZIP format must produce non-null, non-empty ZipBytes.
    /// </summary>
    [Fact]
    public void CiMini_ZipFormat_ProducesNonEmptyBytes() {
        var result = fixture.Zip;

        Assert.Equal("Completed", result.Status);
        Assert.NotNull(result.ZipBytes);
        Assert.True(result.ZipBytes!.Length > 0);

        string desktopPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PRISM-tiny-test.zip");
        File.WriteAllBytes(desktopPath, result.ZipBytes!);
    }

    /// <summary>
    /// Non-vacuous guard: real OK rows must exist and carry a FamilyID. This is the assertion the other
    /// CiMini tests lack — they are all satisfied when every image is KO. A classification (CLIP)
    /// failure must never KO an image, so filename-token matching can still assign a FamilyID.
    /// </summary>
    [Fact]
    public void CiMini_ImagesAreAssociatedToFamilyId() {
        var result = fixture.Default;

        Assert.Equal("Completed", result.Status);
        Assert.NotEmpty(result.OkImages);

        int withFamily = result.OkImages.Count(r => !string.IsNullOrWhiteSpace(r.Output?.Family));
        Assert.True(withFamily > 0,
            $"Expected OK images associated to a FamilyID; got {withFamily} with a FamilyID of {result.OkImages.Count} OK and {result.KoImages.Count} KO.");

        // A CLIP failure must degrade gracefully, not KO the image.
        Assert.DoesNotContain(result.KoImages, r => r.KoReasonCode == "CLASSIFY_ERROR");
    }

}
