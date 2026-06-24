using Xunit;

namespace PrismCoreTests.Order;

/// <summary>
/// Unit tests for <see cref="DetOrderConfig"/> loading and <see cref="ImageOrderer"/> ordering logic.
/// Uses the real DetOrderRules.json and DetOrderKeywordStems.json; records are built inline per test.
/// <see cref="ImageOrderer.Run"/> takes the LAMBDA list and family records directly.
/// </summary>
public class ImageOrdererTests
{
    private static readonly string RulesPath  = ResolveConfigPath("config/DetOrderRules.json");
    private static readonly string StemsPath  = ResolveConfigPath("config/DetOrderKeywordStems.json");

    //  DetOrderConfig.Load contract 

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

    //  ImageOrderer.Run — basic assignment 

    [Fact]
    public void Run_SingleImage_AssignsDet0WithCorrectFamily()
    {
        // front-packshot qualifies for det0 in default rules.
        List<ImageRecord_LAMBDA> records = [MakeLambda("product_front.jpg", "front-packshot", "FAM001")];

        ImageOrderer.Run(records, [MakeFamily("FAM001")]);

        ImageRecord_LAMBDA result = records[0];
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
        List<ImageRecord_LAMBDA> records =
        [
            MakeLambda("img_front.jpg", "front-packshot", "FAM001"),
            MakeLambda("img_back.jpg",  "back-packshot",  "FAM001")
        ];

        ImageOrderer.Run(records, [MakeFamily("FAM001")]);

        ImageRecord_LAMBDA front = records.Single(r => r.SelectedPhenotype == "front-packshot");
        ImageRecord_LAMBDA back  = records.Single(r => r.SelectedPhenotype == "back-packshot");

        Assert.Equal(0, front.DetOrder);
        Assert.Equal(1, back.DetOrder);
        Assert.Equal("FAM001", front.Family);
        Assert.Equal("FAM001", back.Family);
    }

    //  Tie-breakers 

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

        List<ImageRecord_LAMBDA> records = [imageA, imageB];

        ImageOrderer.Run(records, [MakeFamily("FAM001")]);

        // imageA (index 0) wins det0; imageB (index 1) is overflow (det 8+)
        Assert.Equal(0, records[0].DetOrder);
        Assert.False(records[0].OrderEvidence!.IsOverflow);
        Assert.Equal("ngp-confidence", records[0].OrderEvidence!.TieBreakerWon);

        Assert.True(records[1].OrderEvidence!.IsOverflow,
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

        List<ImageRecord_LAMBDA> records = [imageA, imageB];

        ImageOrderer.Run(records, [MakeFamily("FAM001")]);

        // imageA has "front" hint, should win det0
        Assert.Equal(0, records[0].DetOrder);
        Assert.False(records[0].OrderEvidence!.IsOverflow);
        Assert.Equal("filename-hint", records[0].OrderEvidence!.TieBreakerWon);

        Assert.True(records[1].OrderEvidence!.IsOverflow,
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

        List<ImageRecord_LAMBDA> records = [imageA, imageB];

        ImageOrderer.Run(records, [MakeFamily("FAM001")]);

        Assert.Equal(0, records[0].DetOrder);
        Assert.False(records[0].OrderEvidence!.IsOverflow);
        Assert.Equal("source-index", records[0].OrderEvidence!.TieBreakerWon);

        Assert.True(records[1].OrderEvidence!.IsOverflow,
            "Losing front-packshot image should be overflow since front-packshot does not qualify for det1.");
    }

    //  Overflow and edge cases 

    [Fact]
    public void Run_NullPhenotype_AssignedAsOverflowAfterConfiguredSlots()
    {
        // Image with null phenotype cannot qualify for any det slot.
        // It should appear as overflow after the last configured slot (det7 in default = index 7).
        List<ImageRecord_LAMBDA> records = [MakeLambda("product.jpg", phenotype: null, familyId: "FAM001")];

        ImageOrderer.Run(records, [MakeFamily("FAM001")]);

        Assert.True(records[0].OrderEvidence!.IsOverflow);
        Assert.True(records[0].DetOrder >= 8,
            $"Expected overflow slot >= 8 (after default det7), got {records[0].DetOrder}");
        Assert.Equal("FAM001", records[0].Family);
    }

    [Fact]
    public void Run_IllustrationTechnicalDrawing_AssignedToDet7()
    {
        // illustration-technical-drawing is in det7 of default rules.
        List<ImageRecord_LAMBDA> records = [MakeLambda("technical.jpg", "illustration-technical-drawing", "FAM001")];

        ImageOrderer.Run(records, [MakeFamily("FAM001")]);

        Assert.Equal(7, records[0].DetOrder);
        Assert.False(records[0].OrderEvidence!.IsOverflow);
        Assert.Equal("illustration-technical-drawing", records[0].OrderEvidence!.WinningPhenotype);
    }

    [Fact]
    public void Run_KoImageSkipped_NotAssignedDetSlot()
    {
        // KO images must be skipped; Family and DetOrder must remain unset.
        ImageRecord_LAMBDA koImage = MakeLambda("ko.jpg", "front-packshot", "FAM001");
        koImage.IsKo = true;
        koImage.KoReasonCode = "VISUAL_DUPLICATE";

        List<ImageRecord_LAMBDA> records = [koImage];

        ImageOrderer.Run(records, [MakeFamily("FAM001")]);

        // KO image must not have Family or OrderEvidence written by the orderer.
        Assert.Equal(string.Empty, koImage.Family);
        Assert.Null(koImage.OrderEvidence);
    }

    //  Helpers 

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
    /// Creates a minimal <see cref="FamilyIDRecord"/> with just the FamilyID set.
    /// </summary>
    private static FamilyIDRecord MakeFamily(string familyId) => new(familyId);

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
