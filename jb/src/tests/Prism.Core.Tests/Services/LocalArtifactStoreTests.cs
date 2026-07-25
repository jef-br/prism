using Xunit;

namespace PrismCoreTests.Services;

/// <summary>
/// Direct unit tests for <see cref="LocalArtifactStore"/>: job-folder layout, LAMBDA document
/// save/load roundtrip, missing-document behavior, image-id sanitization into filesystem-safe
/// document stems, overwrite semantics, and concurrent writes.
/// </summary>
public sealed class LocalArtifactStoreTests : IDisposable {
    private readonly string tempRoot;
    private readonly LocalArtifactStore store;

    public LocalArtifactStoreTests() {
        tempRoot = Path.Combine(Path.GetTempPath(), $"PRISM-STORE-TESTS-{Guid.NewGuid():N}");
        store = new LocalArtifactStore(tempRoot);
    }

    [Fact]
    public void JobFolder_CreatesDirectoryNamedAfterJobId() {
        Guid jobId = Guid.NewGuid();

        string folder = store.JobFolder(jobId);

        Assert.Equal(Path.Combine(tempRoot, jobId.ToString("N")), folder);
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public void SaveThenLoad_RoundtripsTheLambdaDocument() {
        Guid jobId = Guid.NewGuid();
        ImageRecord_LAMBDA lambda = MakeLambda("roundtrip.jpg", "90861025", detOrder: 3);

        store.SaveLambdaDocument(jobId, "roundtrip.jpg", lambda);
        ImageRecord_LAMBDA? loaded = store.LoadLambdaDocument(jobId, "roundtrip.jpg");

        Assert.NotNull(loaded);
        Assert.Equal("roundtrip.jpg", loaded.InitialFullName);
        Assert.Equal("90861025", loaded.MatchEvidence!.FinalFamilyId);
        Assert.Equal(3, loaded.DetOrder);
    }

    [Fact]
    public void Load_MissingDocument_ReturnsNull() {
        Assert.Null(store.LoadLambdaDocument(Guid.NewGuid(), "never-saved.jpg"));
    }

    [Fact]
    public void LambdaDocumentPath_SanitizesInvalidCharactersInImageId() {
        string path = store.LambdaDocumentPath(Guid.NewGuid(), "a<b>:c.jpg");

        Assert.Equal("a_b_c.json", Path.GetFileName(path));
    }

    [Fact]
    public void LambdaDocumentPath_UsesFileStemOfPathLikeImageIds() {
        string path = store.LambdaDocumentPath(Guid.NewGuid(), "families/90861025.jpg");

        Assert.Equal("90861025.json", Path.GetFileName(path));
        Assert.Equal("lambda", Path.GetFileName(Path.GetDirectoryName(path)));
    }

    [Fact]
    public void LambdaDocumentPath_EmptyStem_FallsBackToImage() {
        string path = store.LambdaDocumentPath(Guid.NewGuid(), ".jpg");

        Assert.Equal("image.json", Path.GetFileName(path));
    }

    [Fact]
    public void Save_SameImageId_OverwritesThePreviousDocument() {
        Guid jobId = Guid.NewGuid();

        store.SaveLambdaDocument(jobId, "twice.jpg", MakeLambda("twice.jpg", "11111111", detOrder: 0));
        store.SaveLambdaDocument(jobId, "twice.jpg", MakeLambda("twice.jpg", "22222222", detOrder: 1));
        ImageRecord_LAMBDA? loaded = store.LoadLambdaDocument(jobId, "twice.jpg");

        Assert.Equal("22222222", loaded!.MatchEvidence!.FinalFamilyId);
        Assert.Equal(1, loaded.DetOrder);
    }

    [Fact]
    public void ConcurrentSaves_OfDistinctImageIds_AllLoadBack() {
        Guid jobId = Guid.NewGuid();

        Parallel.For(0, 24, i => store.SaveLambdaDocument(jobId, $"img_{i}.jpg", MakeLambda($"img_{i}.jpg", "90861025", detOrder: i)));

        for (int i = 0; i < 24; i++) {
            ImageRecord_LAMBDA? loaded = store.LoadLambdaDocument(jobId, $"img_{i}.jpg");
            Assert.NotNull(loaded);
            Assert.Equal(i, loaded.DetOrder);
        }
    }

    //  Helpers

    private static ImageRecord_LAMBDA MakeLambda(string filename, string familyId, int detOrder) {
        return new ImageRecord_LAMBDA {
            InitialFullName = filename,
            DetOrder = detOrder,
            MatchEvidence = new MatchEvidence {
                ImageId = filename,
                SourceFilename = filename,
                FinalFamilyId = familyId,
                FinalScore = 1.0,
                IsKo = false
            }
        };
    }

    public void Dispose() {
        try {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch (IOException) {
        }
        catch (UnauthorizedAccessException) {
        }
    }
}
