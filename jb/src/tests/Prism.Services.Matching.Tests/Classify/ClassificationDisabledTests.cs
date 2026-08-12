using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace PrismCoreTests.Classify;

/// <summary>
/// Models.classification.UseIt off: MatchingService never loads the CLIP session or its prompt catalogue,
/// so ClassificationService receives nulls for both. That is the same "not ready" state the service has
/// always reported when the model file is physically absent — every tagging call is a no-op and
/// MatchAsync's existing <c>doClassify</c> guard skips the whole classification pass unchanged.
/// </summary>
public class ClassificationDisabledTests {
    private readonly PrismConfiguration configuration = PrismConfiguration.LoadPrismConfig(
        ConfigLoader.RequireFile(PrismConfiguration.FileName));

    [Fact]
    public void NullClassifier_ReportsNotReady() {
        using ClassificationService service = new(null, null, this.configuration);

        Assert.False(service.IsReady);
    }

    [Fact]
    public void NotReady_ApplyClipTagsBatch_LeavesTheLambdaUntouched() {
        using ClassificationService service = new(null, null, this.configuration);
        using Image<Rgba32> image = new(64, 64, new Rgba32(255, 255, 255));
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "img.jpg" };

        service.ApplyClipTagsBatch([(image, lambda)], 0.5, 0.25);

        Assert.Empty(lambda.Tags.Influential);
        Assert.Empty(lambda.Tags.Trivial);
        // Untouched is the point: the phenotype rules treat a never-set feature exactly as a measured
        // UNKNOWN, so nothing downstream needs to know why CLIP produced no value.
        Assert.Equal("UNKNOWN", lambda.Features.GetValue("product-type-label"));
    }

    [Fact]
    public void NotReady_ApplyClipTags_LeavesTheLambdaUntouched() {
        using ClassificationService service = new(null, null, this.configuration);
        using Image<Rgba32> image = new(64, 64, new Rgba32(255, 255, 255));
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "img.jpg" };

        service.ApplyClipTags(image, lambda, 0.5, 0.25);

        Assert.Empty(lambda.Tags.Influential);
        Assert.Empty(lambda.Tags.Trivial);
    }

    [Fact]
    public void NotReady_DeduplicationStillWorks() {
        // Visual dedup is perceptual-hash only — it never touched CLIP, so switching the model off must
        // not disable it.
        // Same file name in two folders with the same hash — the shape VisualHasher calls a duplicate.
        using ClassificationService service = new(null, null, this.configuration);
        ImageRecord_INPUT first = new() { InitialFullName = "one/a.jpg", ImportStatus = ImportStatus.Ok };
        ImageRecord_INPUT second = new() { InitialFullName = "two/a.jpg", ImportStatus = ImportStatus.Ok };

        IReadOnlyList<DedupGroup> groups = service.FindDuplicates([(first, (UInt128)42), (second, (UInt128)42)]);

        DedupGroup group = Assert.Single(groups);
        Assert.Single(group.Duplicates);
    }
}
