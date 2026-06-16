using Xunit;

namespace PrismCoreTests.Generate;

/// <summary>
/// Unit tests for <see cref="ImageGenerator"/> generation-stage decision logic.
/// Records are built inline per test with Family and DetOrder pre-set
/// (as they would be after the Ordered stage). Config is loaded from the deployed
/// Prism_Config.json, so thresholds reflect the real configuration values.
/// </summary>
public class ImageGeneratorTests
{
    // ─── Generation disabled ──────────────────────────────────────────────────

    [Fact]
    public void Run_GenerationDisabled_AllNonKoImagesSkipped()
    {
        PipelineContext context = MakeContext(
        [
            MakeLambda("img1.jpg", "FAM001", 0, width: 2000, height: 2000),
            MakeLambda("img2.jpg", "FAM002", 0, width: 2000, height: 2000)
        ],
        generationEnabled: false);

        ImageGenerator.Run(context);

        Assert.All(context.LambdaRecords, r =>
            Assert.Equal(GenerationRouteState.Skipped, r.GenerationRouteState));
        Assert.Equal(0, context.GeneratedCount);
    }

    [Fact]
    public void Run_GenerationDisabled_KoImagesNotTouched()
    {
        PipelineContext context = MakeContext(
        [
            MakeLambda("ko.jpg", "FAM001", 0, isKo: true)
        ],
        generationEnabled: false);

        ImageGenerator.Run(context);

        Assert.Equal(GenerationRouteState.NotEvaluated, context.LambdaRecords[0].GenerationRouteState);
    }

    // ─── Family above threshold ───────────────────────────────────────────────

    [Fact]
    public void Run_FamilyAboveThreshold_AllSkipped()
    {
        // MinImagesPerFamily = 1 in config; 2 images = above threshold
        PipelineContext context = MakeContext(
        [
            MakeLambda("img1.jpg", "FAM001", 0, width: 2000, height: 2000),
            MakeLambda("img2.jpg", "FAM001", 1, width: 2000, height: 2000)
        ]);

        ImageGenerator.Run(context);

        Assert.All(context.LambdaRecords, r =>
            Assert.Equal(GenerationRouteState.Skipped, r.GenerationRouteState));
        Assert.Equal(0, context.GeneratedCount);
    }

    // ─── Quality check ────────────────────────────────────────────────────────

    [Fact]
    public void Run_HeroTooSmall_AllMembersSkippedLowQuality()
    {
        // 800×800 is below the 1600×1600 minimum in config
        PipelineContext context = MakeContext(
        [
            MakeLambda("small.jpg", "FAM001", 0, width: 800, height: 800)
        ]);

        ImageGenerator.Run(context);

        Assert.Equal(GenerationRouteState.SkippedLowQuality, context.LambdaRecords[0].GenerationRouteState);
        Assert.Equal(0, context.GeneratedCount);
    }

    [Fact]
    public void Run_UnknownDimensions_TreatedAsQualified()
    {
        // Width=0, Height=0 means the Imported stage did not record dimensions.
        // Unknown dimensions pass the quality check.
        PipelineContext context = MakeContext(
        [
            MakeLambda("unknown.jpg", "FAM001", 0, width: 0, height: 0)
        ]);

        ImageGenerator.Run(context);

        Assert.Equal(GenerationRouteState.Gated, context.LambdaRecords[0].GenerationRouteState);
    }

    // ─── Gated path ──────────────────────────────────────────────────────────

    [Fact]
    public void Run_QualifiedHero_BackendUnavailable_RouteStateIsGated()
    {
        PipelineContext context = MakeContext(
        [
            MakeLambda("hero.jpg", "FAM001", 0, width: 2000, height: 2000)
        ]);

        ImageGenerator.Run(context);

        Assert.Equal(GenerationRouteState.Gated, context.LambdaRecords[0].GenerationRouteState);
    }

    [Fact]
    public void Run_QualifiedHero_GeneratedChildAttachedToHero()
    {
        PipelineContext context = MakeContext(
        [
            MakeLambda("hero.jpg", "FAM001", 0, width: 2000, height: 2000)
        ]);

        ImageGenerator.Run(context);

        Assert.Single(context.LambdaRecords[0].GeneratedChildren);
        Assert.Equal(GenerationStatus.Gated, context.LambdaRecords[0].GeneratedChildren[0].Status);
        Assert.Equal("FAM001", context.LambdaRecords[0].GeneratedChildren[0].SourceFamilyId);
    }

    [Fact]
    public void Run_QualifiedHero_GeneratedCountIncremented()
    {
        PipelineContext context = MakeContext(
        [
            MakeLambda("hero.jpg", "FAM001", 0, width: 2000, height: 2000)
        ]);

        ImageGenerator.Run(context);

        Assert.Equal(1, context.GeneratedCount);
        Assert.Single(context.GeneratedRecords);
    }

    // ─── KO passthrough ───────────────────────────────────────────────────────

    [Fact]
    public void Run_KoImageNotCountedInFamilySize_FamilyProceedsToGeneration()
    {
        // 1 non-KO + 1 KO = effective family size of 1 → proceed to generation path
        PipelineContext context = MakeContext(
        [
            MakeLambda("hero.jpg", "FAM001", 0, width: 2000, height: 2000),
            MakeLambda("ko.jpg",   "FAM001", 1, isKo: true)
        ]);

        ImageGenerator.Run(context);

        Assert.Equal(GenerationRouteState.Gated,         context.LambdaRecords[0].GenerationRouteState);
        Assert.Equal(GenerationRouteState.NotEvaluated,  context.LambdaRecords[1].GenerationRouteState);
        Assert.Equal(1, context.GeneratedCount);
    }

    // ─── Multi-family isolation ───────────────────────────────────────────────

    [Fact]
    public void Run_MixedFamilies_EachEvaluatedIndependently()
    {
        // FAM001 has 1 image → proceeds to gated generation.
        // FAM002 has 2 images → skipped (above threshold).
        PipelineContext context = MakeContext(
        [
            MakeLambda("fam1a.jpg", "FAM001", 0, width: 2000, height: 2000),
            MakeLambda("fam2a.jpg", "FAM002", 0, width: 2000, height: 2000),
            MakeLambda("fam2b.jpg", "FAM002", 1, width: 2000, height: 2000)
        ]);

        ImageGenerator.Run(context);

        Assert.Equal(GenerationRouteState.Gated,   context.LambdaRecords[0].GenerationRouteState);
        Assert.Equal(GenerationRouteState.Skipped, context.LambdaRecords[1].GenerationRouteState);
        Assert.Equal(GenerationRouteState.Skipped, context.LambdaRecords[2].GenerationRouteState);
        Assert.Equal(1, context.GeneratedCount);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

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
        bool isKo = false)
    {
        return new ImageRecord_LAMBDA
        {
            InitialFullName = filename,
            Family          = familyId,
            DetOrder        = detOrder,
            Width           = width,
            Height          = height,
            IsKo            = isKo,
            KoReasonCode    = isKo ? "TEST_KO" : null
        };
    }

    /// <summary>
    /// Builds a minimal <see cref="PipelineContext"/> populated with the given lambda records.
    /// </summary>
    private static PipelineContext MakeContext(
        IReadOnlyList<ImageRecord_LAMBDA> images,
        bool generationEnabled = true)
    {
        PipelineContext context = new(
            Guid.NewGuid(),
            imageRecords:   [],
            excelRecords:   [],
            zipFileRecords: [],
            parameters:     new PrismProcessingParameters { Format = "json", Generation = generationEnabled },
            startedAt:      DateTimeOffset.UtcNow);

        foreach (ImageRecord_LAMBDA img in images)
            context.LambdaRecords.Add(img);

        return context;
    }
}
