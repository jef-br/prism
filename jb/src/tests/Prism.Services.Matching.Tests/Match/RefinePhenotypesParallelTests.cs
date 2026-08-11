using Xunit;

namespace PrismCoreTests.Match;

/// <summary>
/// T-6910: RefinePhenotypes fans out across cores. The refinement chain is safe to parallelize only
/// because every participant is stateless or read-only (all Analyzer_* types are static and fieldless,
/// SubjectDetector/ProductTypeResolver/PhenotypeRuleSet hold readonly config, YoloDetector.Detect
/// serializes on its own RunLock). These tests pin the two properties that a regression there would
/// break first: the counters must not lose increments to a race, and the per-image result must not
/// depend on which thread got there first.
/// </summary>
public sealed class RefinePhenotypesParallelTests {
    // Enough images that Parallel.ForEach genuinely uses multiple threads — a handful would often
    // schedule onto one worker and pass regardless of whether the code is thread-safe.
    private const int ImageCount = 40;

    [Fact]
    public async Task RefinePhenotypes_PhenotypeAssignedCount_MatchesActualAssignments() {
        MatchingResult result = await RunMatching();

        int actuallyAssigned = result.LambdaRecords.Count(r => !r.IsKo && r.SelectedPhenotype is not null);

        // A dropped Interlocked.Increment shows up here and nowhere else — the phenotypes themselves
        // would still be correct, only the reported count would silently undercount.
        Assert.Equal(actuallyAssigned, result.PhenotypeAssignedCount);
    }

    [Fact]
    public async Task RefinePhenotypes_RunTwice_ProducesIdenticalPhenotypes() {
        MatchingResult first = await RunMatching();
        MatchingResult second = await RunMatching();

        Dictionary<string, string?> firstByName = first.LambdaRecords
            .ToDictionary(r => r.InitialFullName!, r => r.SelectedPhenotype, StringComparer.OrdinalIgnoreCase);

        foreach (ImageRecord_LAMBDA record in second.LambdaRecords) {
            Assert.True(firstByName.TryGetValue(record.InitialFullName!, out string? firstPhenotype),
                $"'{record.InitialFullName}' present in one run but not the other.");
            Assert.Equal(firstPhenotype, record.SelectedPhenotype);
        }

        Assert.Equal(first.PhenotypeAssignedCount, second.PhenotypeAssignedCount);
    }

    private static async Task<MatchingResult> RunMatching() {
        PrismConfiguration configuration = PrismConfiguration.LoadPrismConfig(
            ConfigLoader.RequireFile(PrismConfiguration.FileName));
        using MatchingService matching = new(configuration);

        string imagesFolder = Path.Combine(PipelineFixture.ResolveTestFixturePath(), "CiMini");
        string[] imagePaths = Directory.GetFiles(imagesFolder, "*.jpg")
            .OrderBy(p => p, StringComparer.Ordinal)
            .Take(ImageCount)
            .ToArray();

        Assert.True(imagePaths.Length >= 2, $"CiMini fixture at '{imagesFolder}' has too few images to test parallelism.");

        // The CiMini source images stand in for normalized output — the refinement chain only needs a
        // readable JPEG at NormalizedJpgPath, and the co-deployment guard only needs JobTempFolder to
        // exist. SkipClassification keeps CLIP out so the test measures the refinement pass, not ONNX.
        List<ImageRecord_INPUT> images = imagePaths
            .Select(p => new ImageRecord_INPUT {
                InitialFullName = Path.GetFileName(p),
                ImportStatus = ImportStatus.Ok,
                NormalizedJpgPath = p,
                NormalizedWidth = 800,
                NormalizedHeight = 800
            })
            .ToList();

        IngestResult ingest = new() {
            JobID = Guid.NewGuid(),
            Parameters = new PrismProcessingParameters { SkipClassification = true },
            NormalizedImages = images,
            FamilyRecords = [new FamilyIDRecord("99999001")],
            JobTempFolder = imagesFolder
        };

        return await matching.MatchAsync(ingest, new LocalArtifactStore(), null, CancellationToken.None);
    }
}
