using Xunit;

namespace PrismCoreTests.Match;

/// <summary>
/// T-3600: a Matching host that cannot read the job temp folder (deployment topology error — Ingest
/// and Matching must be co-deployed on one filesystem) must fail loud with an explicit message, not
/// KO every image with misleading per-image decode errors.
/// </summary>
public sealed class MatchingCoDeploymentGuardTests {

    [Fact]
    public async Task MatchAsync_UnreadableJobTempFolder_ThrowsCoDeploymentError() {
        PrismConfiguration configuration = PrismConfiguration.LoadPrismConfig(
            ConfigLoader.RequireFile(PrismConfiguration.FileName));
        using MatchingService matching = new(configuration);

        string missingFolder = Path.Combine(Path.GetTempPath(), $"PRISM-MISSING-{Guid.NewGuid():N}");

        IngestResult ingest = new() {
            JobID          = Guid.NewGuid(),
            Parameters     = new PrismProcessingParameters(),
            NormalizedImages = [new ImageRecord_INPUT {
                InitialFullName   = "one.jpg",
                ImportStatus      = ImportStatus.Ok,
                NormalizedJpgPath = Path.Combine(missingFolder, "normalized", "000000_one.jpg"),
                NormalizedWidth   = 100,
                NormalizedHeight  = 100
            }],
            FamilyRecords  = [],
            JobTempFolder  = missingFolder
        };

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => matching.MatchAsync(ingest, new LocalArtifactStore(), null, CancellationToken.None));

        Assert.Contains("co-deployed", ex.Message);
        Assert.Contains(missingFolder, ex.Message);
    }
}
