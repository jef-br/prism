using Xunit;

namespace PrismCoreTests.Generate;

/// <summary>
/// Unit tests for <see cref="ImageGenerator"/> generation-stage decision logic.
/// Records are built inline per test with Family and DetOrder pre-set
/// (as they would be after the Ordered stage). Config is loaded from the deployed
/// Prism_Config.json, so thresholds reflect the real configuration values.
/// <see cref="ImageGenerator.Run"/> returns the new generated records directly.
/// </summary>
public class ImageGeneratorTests {
    //  Generation disabled 

    [Fact]
    public void Run_GenerationDisabled_AllNonKoImagesSkipped() {
        List<ImageRecord_LAMBDA> records =
        [
            MakeLambda("img1.jpg", "FAM001", 0, width: 2000, height: 2000),
            MakeLambda("img2.jpg", "FAM002", 0, width: 2000, height: 2000)
        ];

        IReadOnlyList<ImageRecord_GENERATED> generated = ImageGenerator.Run(records, generationEnabled: false, generationBackendAvailable: false);

        Assert.All(records, r =>
            Assert.Equal(GenerationRouteState.Skipped, r.GenerationRouteState));
        Assert.Empty(generated);
    }

    [Fact]
    public void Run_GenerationDisabled_KoImagesNotTouched() {
        List<ImageRecord_LAMBDA> records = [MakeLambda("ko.jpg", "FAM001", 0, isKo: true)];

        ImageGenerator.Run(records, generationEnabled: false, generationBackendAvailable: false);

        Assert.Equal(GenerationRouteState.NotEvaluated, records[0].GenerationRouteState);
    }

    //  Family above threshold 

    [Fact]
    public void Run_FamilyAboveThreshold_AllSkipped() {
        // MinImagesPerFamily = 1 in config; 2 images = above threshold
        List<ImageRecord_LAMBDA> records =
        [
            MakeLambda("img1.jpg", "FAM001", 0, width: 2000, height: 2000),
            MakeLambda("img2.jpg", "FAM001", 1, width: 2000, height: 2000)
        ];

        IReadOnlyList<ImageRecord_GENERATED> generated = ImageGenerator.Run(records, generationEnabled: true, generationBackendAvailable: false);

        Assert.All(records, r =>
            Assert.Equal(GenerationRouteState.Skipped, r.GenerationRouteState));
        Assert.Empty(generated);
    }

    //  Quality check 

    [Fact]
    public void Run_HeroTooSmall_AllMembersSkippedLowQuality() {
        // 800×800 is below the 1600×1600 minimum in config
        List<ImageRecord_LAMBDA> records = [MakeLambda("small.jpg", "FAM001", 0, width: 800, height: 800)];

        IReadOnlyList<ImageRecord_GENERATED> generated = ImageGenerator.Run(records, generationEnabled: true, generationBackendAvailable: false);

        Assert.Equal(GenerationRouteState.SkippedLowQuality, records[0].GenerationRouteState);
        Assert.Empty(generated);
    }

    [Fact]
    public void Run_UnknownDimensions_TreatedAsQualified() {
        // Width=0, Height=0 means the Imported stage did not record dimensions.
        // Unknown dimensions pass the quality check.
        List<ImageRecord_LAMBDA> records = [MakeLambda("unknown.jpg", "FAM001", 0, width: 0, height: 0)];

        ImageGenerator.Run(records, generationEnabled: true, generationBackendAvailable: false);

        Assert.Equal(GenerationRouteState.Gated, records[0].GenerationRouteState);
    }

    //  Gated path 

    [Fact]
    public void Run_QualifiedHero_BackendUnavailable_RouteStateIsGated() {
        List<ImageRecord_LAMBDA> records = [MakeLambda("hero.jpg", "FAM001", 0, width: 2000, height: 2000)];

        ImageGenerator.Run(records, generationEnabled: true, generationBackendAvailable: false);

        Assert.Equal(GenerationRouteState.Gated, records[0].GenerationRouteState);
    }

    [Fact]
    public void Run_QualifiedHero_GeneratedChildAttachedToHero() {
        List<ImageRecord_LAMBDA> records = [MakeLambda("hero.jpg", "FAM001", 0, width: 2000, height: 2000)];

        ImageGenerator.Run(records, generationEnabled: true, generationBackendAvailable: false);

        Assert.Single(records[0].GeneratedChildren);
        Assert.Equal(GenerationStatus.Gated, records[0].GeneratedChildren[0].Status);
        Assert.Equal("FAM001", records[0].GeneratedChildren[0].SourceFamilyId);
    }

    [Fact]
    public void Run_QualifiedHero_GeneratedCountIncremented() {
        List<ImageRecord_LAMBDA> records = [MakeLambda("hero.jpg", "FAM001", 0, width: 2000, height: 2000)];

        IReadOnlyList<ImageRecord_GENERATED> generated = ImageGenerator.Run(records, generationEnabled: true, generationBackendAvailable: false);

        Assert.Single(generated);
    }

    //  KO passthrough 

    [Fact]
    public void Run_KoImageNotCountedInFamilySize_FamilyProceedsToGeneration() {
        // 1 non-KO + 1 KO = effective family size of 1 → proceed to generation path
        List<ImageRecord_LAMBDA> records =
        [
            MakeLambda("hero.jpg", "FAM001", 0, width: 2000, height: 2000),
            MakeLambda("ko.jpg",   "FAM001", 1, isKo: true)
        ];

        IReadOnlyList<ImageRecord_GENERATED> generated = ImageGenerator.Run(records, generationEnabled: true, generationBackendAvailable: false);

        Assert.Equal(GenerationRouteState.Gated, records[0].GenerationRouteState);
        Assert.Equal(GenerationRouteState.NotEvaluated, records[1].GenerationRouteState);
        Assert.Single(generated);
    }

    //  Multi-family isolation 

    [Fact]
    public void Run_MixedFamilies_EachEvaluatedIndependently() {
        // FAM001 has 1 image → proceeds to gated generation.
        // FAM002 has 2 images → skipped (above threshold).
        List<ImageRecord_LAMBDA> records =
        [
            MakeLambda("fam1a.jpg", "FAM001", 0, width: 2000, height: 2000),
            MakeLambda("fam2a.jpg", "FAM002", 0, width: 2000, height: 2000),
            MakeLambda("fam2b.jpg", "FAM002", 1, width: 2000, height: 2000)
        ];

        IReadOnlyList<ImageRecord_GENERATED> generated = ImageGenerator.Run(records, generationEnabled: true, generationBackendAvailable: false);

        Assert.Equal(GenerationRouteState.Gated, records[0].GenerationRouteState);
        Assert.Equal(GenerationRouteState.Skipped, records[1].GenerationRouteState);
        Assert.Equal(GenerationRouteState.Skipped, records[2].GenerationRouteState);
        Assert.Single(generated);
    }

    //  Models.Generation.UseIt

    [Fact]
    public void Run_BackendUnavailable_IsTheShippedBehaviour_GatedPlaceholderRecord() {
        // Models.Generation.UseIt ships false, so this is what every job produces today. Pinned as the
        // baseline the toggle must not have moved.
        List<ImageRecord_LAMBDA> records = [MakeLambda("hero.jpg", "FAM001", 0, width: 2000, height: 2000)];

        IReadOnlyList<ImageRecord_GENERATED> generated =
            ImageGenerator.Run(records, generationEnabled: true, generationBackendAvailable: false);

        Assert.Single(generated);
        Assert.Equal(GenerationStatus.Gated, generated[0].Status);
        Assert.Equal(GenerationRouteState.Gated, records[0].GenerationRouteState);
    }

    [Fact]
    public void Run_BackendAvailable_SkipsPlaceholderCreation() {
        // Why Generation must not default to true: with no real inference wired up, flipping it skips
        // the Gated-placeholder branch and the family silently produces nothing at all.
        List<ImageRecord_LAMBDA> records = [MakeLambda("hero.jpg", "FAM001", 0, width: 2000, height: 2000)];

        IReadOnlyList<ImageRecord_GENERATED> generated =
            ImageGenerator.Run(records, generationEnabled: true, generationBackendAvailable: true);

        Assert.Empty(generated);
        Assert.Empty(records[0].GeneratedChildren);
    }

    //  Helpers

    /// <summary>
    /// Creates a minimal <see cref="ImageRecord_LAMBDA"/> with Family, DetOrder, and dimensions set,
    /// as they would be after the Ordered stage.
    /// </summary>
    private static ImageRecord_LAMBDA MakeLambda(
        string filename,
        string familyId,
        int detOrder,
        int width = 2000,
        int height = 2000,
        bool isKo = false) {
        return new ImageRecord_LAMBDA {
            InitialFullName = filename,
            Family = familyId,
            DetOrder = detOrder,
            Width = width,
            Height = height,
            IsKo = isKo,
            KoReasonCode = isKo ? "TEST_KO" : null
        };
    }
}
