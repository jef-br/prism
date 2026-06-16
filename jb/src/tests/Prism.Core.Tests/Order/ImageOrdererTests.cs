using Xunit;

namespace PrismCoreTests.Order;

/// <summary>
/// Unit tests for <see cref="DetOrderConfig"/> loading and <see cref="ImageOrderer"/> ordering logic.
/// Uses the real DetOrderRules.json and DetOrderKeywordStems.json; records are built inline per test.
/// </summary>
public class ImageOrdererTests
{
    private static readonly string RulesPath  = ResolveConfigPath("Images/Order/DetOrderRules.json");
    private static readonly string StemsPath  = ResolveConfigPath("Images/Order/DetOrderKeywordStems.json");

    // ─── DetOrderConfig.Load contract ─────────────────────────────────────────

    [Fact]
    public void Load_ValidPath_Has19ProductTypesIncludingDefault()
    {
        DetOrderConfig config = DetOrderConfig.Load(RulesPath, StemsPath);

        // 18 named product types + default
        string[] expectedTypes =
        [
            "default",
            "clothing-tops", "clothing-bottoms", "clothing-outerwear", "clothing-dresses",
            "footwear", "bags-accessories",
            "fmcg-packaged-food", "fmcg-personal-care", "beauty-cosmetics",
            "electronics-small", "electronics-large",
            "homeware-soft", "homeware-hard",
            "toys-children", "diy-tools", "gardening", "sports-equipment", "furniture"
        ];

        foreach (string type in expectedTypes)
        {
            Assert.True(config.HasProductType(type), $"Expected product type '{type}' not found in DetOrderRules.json.");
        }
    }

    [Fact]
    public void GetSlots_ClothingTops_Returns8Slots()
    {
        DetOrderConfig config = DetOrderConfig.Load(RulesPath, StemsPath);
        IReadOnlyList<DetSlotRule> slots = config.GetSlots("clothing-tops");
        Assert.Equal(8, slots.Count);
    }

    [Fact]
    public void GetSlots_UnknownProductType_ReturnsDefaultSlots()
    {
        DetOrderConfig config = DetOrderConfig.Load(RulesPath, StemsPath);
        IReadOnlyList<DetSlotRule> unknown = config.GetSlots("does-not-exist");
        IReadOnlyList<DetSlotRule> defaultSlots = config.GetSlots("default");

        Assert.Equal(defaultSlots.Count, unknown.Count);
        for (int i = 0; i < defaultSlots.Count; i++)
        {
            Assert.Equal(defaultSlots[i].SlotIndex, unknown[i].SlotIndex);
            Assert.Equal(defaultSlots[i].Keyword, unknown[i].Keyword);
        }
    }

    // ─── ImageOrderer.Run — basic assignment ──────────────────────────────────

    [Fact]
    public void Run_SingleImage_AssignsDet0WithCorrectFamily()
    {
        // front-packshot qualifies for det0 in default rules.
        PipelineContext context = MakeContext(
            images: [MakeLambda("product_front.jpg", "front-packshot", "FAM001")],
            families: [MakeFamily("FAM001")]);

        ImageOrderer.Run(context);

        ImageRecord_LAMBDA result = context.LambdaRecords[0];
        Assert.Equal("FAM001", result.Family);
        Assert.Equal(0, result.DetOrder);
        Assert.NotNull(result.OrderEvidence);
        Assert.False(result.OrderEvidence!.IsOverflow);
        Assert.Equal("front-packshot", result.OrderEvidence.WinningPhenotype);
    }

    [Fact]
    public void Run_TwoImages_ClearPhenotypeWinner_CorrectOrder()
    {
        // front-packshot → det0, back-packshot → det1 (no competition).
        PipelineContext context = MakeContext(
            images:
            [
                MakeLambda("img_front.jpg", "front-packshot", "FAM001"),
                MakeLambda("img_back.jpg",  "back-packshot",  "FAM001")
            ],
            families: [MakeFamily("FAM001")]);

        ImageOrderer.Run(context);

        ImageRecord_LAMBDA front = context.LambdaRecords.Single(r => r.SelectedPhenotype == "front-packshot");
        ImageRecord_LAMBDA back  = context.LambdaRecords.Single(r => r.SelectedPhenotype == "back-packshot");

        Assert.Equal(0, front.DetOrder);
        Assert.Equal(1, back.DetOrder);
        Assert.Equal("FAM001", front.Family);
        Assert.Equal("FAM001", back.Family);
    }

    // ─── Tie-breakers ─────────────────────────────────────────────────────────

    [Fact]
    public void Run_TieBreakerByNgpConfidence_HigherConfidenceWinsDet0()
    {
        // Two front-packshot images competing for det0.
        // Image A has 3 known features; image B has 1 known feature.
        // Image A should win det0; image B has no other qualifying slot so it becomes overflow.
        ImageRecord_LAMBDA imageA = MakeLambda("imgA.jpg", "front-packshot", "FAM001");
        SetFeatureCount(imageA, 3);

        ImageRecord_LAMBDA imageB = MakeLambda("imgB.jpg", "front-packshot", "FAM001");
        SetFeatureCount(imageB, 1);

        PipelineContext context = MakeContext(
            images: [imageA, imageB],
            families: [MakeFamily("FAM001")]);

        ImageOrderer.Run(context);

        // imageA (index 0) wins det0; imageB (index 1) is overflow (det 8+)
        Assert.Equal(0, context.LambdaRecords[0].DetOrder);
        Assert.False(context.LambdaRecords[0].OrderEvidence!.IsOverflow);
        Assert.Equal("ngp-confidence", context.LambdaRecords[0].OrderEvidence!.TieBreakerWon);

        Assert.True(context.LambdaRecords[1].OrderEvidence!.IsOverflow,
            "Losing front-packshot image should be overflow since front-packshot does not qualify for det1.");
    }

    [Fact]
    public void Run_TieBreakerByFilenameHint_MatchingHintWins()
    {
        // Two front-packshot images, same NGP confidence, but imageA has "front" stem in filename.
        // imageA should win det0; imageB becomes overflow (front-packshot not in det1 list).
        ImageRecord_LAMBDA imageA = MakeLambda("product_front.jpg", "front-packshot", "FAM001");
        SetFeatureCount(imageA, 2);

        ImageRecord_LAMBDA imageB = MakeLambda("product_image.jpg", "front-packshot", "FAM001");
        SetFeatureCount(imageB, 2);

        PipelineContext context = MakeContext(
            images: [imageA, imageB],
            families: [MakeFamily("FAM001")]);

        ImageOrderer.Run(context);

        // imageA has "front" hint, should win det0
        Assert.Equal(0, context.LambdaRecords[0].DetOrder);
        Assert.False(context.LambdaRecords[0].OrderEvidence!.IsOverflow);
        Assert.Equal("filename-hint", context.LambdaRecords[0].OrderEvidence!.TieBreakerWon);

        Assert.True(context.LambdaRecords[1].OrderEvidence!.IsOverflow,
            "Losing front-packshot image should be overflow since front-packshot does not qualify for det1.");
    }

    [Fact]
    public void Run_TieBreakerBySourceIndex_LowerIndexWinsOnAllTie()
    {
        // Two front-packshot images, same NGP confidence, no filename hint.
        // Lower source index (index 0) should win det0; the other becomes overflow.
        ImageRecord_LAMBDA imageA = MakeLambda("img1.jpg", "front-packshot", "FAM001");
        SetFeatureCount(imageA, 2);

        ImageRecord_LAMBDA imageB = MakeLambda("img2.jpg", "front-packshot", "FAM001");
        SetFeatureCount(imageB, 2);

        PipelineContext context = MakeContext(
            images: [imageA, imageB],
            families: [MakeFamily("FAM001")]);

        ImageOrderer.Run(context);

        Assert.Equal(0, context.LambdaRecords[0].DetOrder);
        Assert.False(context.LambdaRecords[0].OrderEvidence!.IsOverflow);
        Assert.Equal("source-index", context.LambdaRecords[0].OrderEvidence!.TieBreakerWon);

        Assert.True(context.LambdaRecords[1].OrderEvidence!.IsOverflow,
            "Losing front-packshot image should be overflow since front-packshot does not qualify for det1.");
    }

    // ─── Overflow and edge cases ──────────────────────────────────────────────

    [Fact]
    public void Run_NullPhenotype_AssignedAsOverflowAfterConfiguredSlots()
    {
        // Image with null phenotype cannot qualify for any det slot.
        // It should appear as overflow after the last configured slot (det7 in default = index 7).
        ImageRecord_LAMBDA image = MakeLambda("product.jpg", phenotype: null, familyId: "FAM001");

        PipelineContext context = MakeContext(
            images: [image],
            families: [MakeFamily("FAM001")]);

        ImageOrderer.Run(context);

        Assert.True(context.LambdaRecords[0].OrderEvidence!.IsOverflow);
        Assert.True(context.LambdaRecords[0].DetOrder >= 8,
            $"Expected overflow slot >= 8 (after default det7), got {context.LambdaRecords[0].DetOrder}");
        Assert.Equal("FAM001", context.LambdaRecords[0].Family);
    }

    [Fact]
    public void Run_IllustrationTechnicalDrawing_AssignedToDet7()
    {
        // illustration-technical-drawing is in det7 of default rules.
        ImageRecord_LAMBDA image = MakeLambda("technical.jpg", "illustration-technical-drawing", "FAM001");

        PipelineContext context = MakeContext(
            images: [image],
            families: [MakeFamily("FAM001")]);

        ImageOrderer.Run(context);

        Assert.Equal(7, context.LambdaRecords[0].DetOrder);
        Assert.False(context.LambdaRecords[0].OrderEvidence!.IsOverflow);
        Assert.Equal("illustration-technical-drawing", context.LambdaRecords[0].OrderEvidence!.WinningPhenotype);
    }

    [Fact]
    public void Run_KoImageSkipped_NotAssignedDetSlot()
    {
        // KO images must be skipped; Family and DetOrder must remain unset.
        ImageRecord_LAMBDA koImage = MakeLambda("ko.jpg", "front-packshot", "FAM001");
        koImage.IsKo = true;
        koImage.KoReasonCode = "VISUAL_DUPLICATE";

        PipelineContext context = MakeContext(
            images: [koImage],
            families: [MakeFamily("FAM001")]);

        ImageOrderer.Run(context);

        // KO image must not have Family or OrderEvidence written by the orderer.
        Assert.Equal(string.Empty, koImage.Family);
        Assert.Null(koImage.OrderEvidence);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a minimal <see cref="ImageRecord_LAMBDA"/> with MatchEvidence set for ordering.
    /// </summary>
    private static ImageRecord_LAMBDA MakeLambda(string filename, string? phenotype, string familyId)
    {
        var lambda = new ImageRecord_LAMBDA
        {
            InitialFullName   = filename,
            SelectedPhenotype = phenotype,
            MatchEvidence     = new MatchEvidence
            {
                ImageId        = filename,
                SourceFilename = filename,
                FinalFamilyId  = familyId,
                FinalScore     = 1.0,
                IsKo           = false
            }
        };

        return lambda;
    }

    /// <summary>
    /// Creates a minimal <see cref="FamilyRecord"/> with just the FamilyID set.
    /// </summary>
    private static FamilyRecord MakeFamily(string familyId) => new(familyId);

    /// <summary>
    /// Sets a fixed number of non-UNKNOWN features on the image to control NGP confidence.
    /// </summary>
    private static void SetFeatureCount(ImageRecord_LAMBDA lambda, int count)
    {
        for (int i = 0; i < count; i++)
        {
            lambda.Features.Set($"test-feature-{i}", "true", 1.0, "test");
        }
    }

    /// <summary>
    /// Builds a minimal <see cref="PipelineContext"/> populated with the given images and families.
    /// </summary>
    private static PipelineContext MakeContext(
        IReadOnlyList<ImageRecord_LAMBDA> images,
        IReadOnlyList<FamilyRecord> families)
    {
        PipelineContext context = new(
            Guid.NewGuid(),
            imageRecords:   [],
            excelRecords:   [],
            zipFileRecords: [],
            parameters:     new PrismProcessingParameters { Format = "json" },
            startedAt:      DateTimeOffset.UtcNow);

        foreach (ImageRecord_LAMBDA img in images)
            context.LambdaRecords.Add(img);

        // Inject an ImportStageResult so FamilyRecords are available during Run().
        context.ImportResult = new ImportStageResult
        {
            NormalizedImages = [],
            FamilyRecords    = families
        };

        return context;
    }

    /// <summary>
    /// Resolves the absolute path to a config file relative to the core source root.
    /// Walks up from the assembly directory looking for the jb/src/core root.
    /// </summary>
    private static string ResolveConfigPath(string relativeFromCore)
    {
        var assemblyDir = new FileInfo(typeof(ImageOrdererTests).Assembly.Location).DirectoryName
            ?? throw new InvalidOperationException("Cannot determine assembly directory.");

        var current = new DirectoryInfo(assemblyDir);
        while (current.Parent != null)
        {
            string candidate = Path.Combine(current.FullName, "jb", "src", "core", relativeFromCore);
            if (File.Exists(candidate))
                return candidate;
            current = current.Parent;
        }

        // Absolute fallback for local dev machines.
        string fallback = Path.Combine(
            @"c:\Users\JefB\Documents\JBGITROOT\prism\jb\src\core", relativeFromCore);
        if (File.Exists(fallback))
            return fallback;

        throw new FileNotFoundException(
            $"Config file '{relativeFromCore}' not found when walking up from assembly directory.");
    }
}
