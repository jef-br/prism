using Xunit;

namespace PrismCoreTests.Order;

/// <summary>
/// Unit tests for <see cref="DetOrderConfig"/> loading and <see cref="ImageOrderer"/> ordering logic.
/// Uses the real DetOrderRules.json and DetOrderKeywordStems.json; records are built inline per test.
/// <see cref="ImageOrderer.Run"/> takes the LAMBDA list and family records directly.
/// </summary>
public class ImageOrdererTests {
    private static readonly string RulesPath = ResolveConfigPath("config/DetOrderRules.json");
    private static readonly string StemsPath = ResolveConfigPath("config/DetOrderKeywordStems.json");

    //  DetOrderConfig.Load contract 

    [Fact]
    public void Load_ValidPath_Has5ProductTypesIncludingDefault() {
        DetOrderConfig config = DetOrderConfig.Load(RulesPath, StemsPath);

        // 4 named product types + default (collapsed from 18+default by the follow-up ticket
        // to T-4700 — the other 13 now fall back to "default").
        string[] expectedTypes = ["default", "topwear", "bottomwear", "footwear", "bags-accessories"];

        foreach (string type in expectedTypes) {
            Assert.True(config.HasProductType(type), $"Expected product type '{type}' not found in DetOrderRules.json.");
        }
        Assert.False(config.HasProductType("clothing-tops"), "clothing-tops was renamed to topwear.");
        Assert.False(config.HasProductType("furniture"), "furniture was retired; falls back to default.");
    }

    [Fact]
    public void GetSlots_Topwear_Returns8Slots() {
        DetOrderConfig config = DetOrderConfig.Load(RulesPath, StemsPath);
        IReadOnlyList<DetSlotRule> slots = config.GetSlots("topwear");
        Assert.Equal(8, slots.Count);
    }

    [Fact]
    public void GetSlots_UnknownProductType_ReturnsDefaultSlots() {
        DetOrderConfig config = DetOrderConfig.Load(RulesPath, StemsPath);
        IReadOnlyList<DetSlotRule> unknown = config.GetSlots("does-not-exist");
        IReadOnlyList<DetSlotRule> defaultSlots = config.GetSlots("default");

        Assert.Equal(defaultSlots.Count, unknown.Count);
        for (int i = 0; i < defaultSlots.Count; i++) {
            Assert.Equal(defaultSlots[i].SlotIndex, unknown[i].SlotIndex);
            Assert.Equal(defaultSlots[i].Keyword, unknown[i].Keyword);
        }
    }

    //  ImageOrderer.Run — basic assignment 

    [Fact]
    public void Run_SingleImage_AssignsDet0WithCorrectFamily() {
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
    public void Run_TwoImages_ClearPhenotypeWinner_CorrectOrder() {
        // front-packshot → det0, back-packshot → det1 (no competition).
        List<ImageRecord_LAMBDA> records =
        [
            MakeLambda("img_front.jpg", "front-packshot", "FAM001"),
            MakeLambda("img_back.jpg",  "back-packshot",  "FAM001")
        ];

        ImageOrderer.Run(records, [MakeFamily("FAM001")]);

        ImageRecord_LAMBDA front = records.Single(r => r.SelectedPhenotype == "front-packshot");
        ImageRecord_LAMBDA back = records.Single(r => r.SelectedPhenotype == "back-packshot");

        Assert.Equal(0, front.DetOrder);
        Assert.Equal(1, back.DetOrder);
        Assert.Equal("FAM001", front.Family);
        Assert.Equal("FAM001", back.Family);
    }

    [Fact]
    public void Run_RefinedProductTypeId_WinsOverValueSniffing() {
        // The refinement chain (Analyzer_ProductType) resolved footwear from the IEM; the orderer
        // must adopt it for the whole family instead of sniffing canonical property values.
        ImageRecord_LAMBDA image = MakeLambda("shoe_front.jpg", "front-packshot", "FAM001");
        image.ProductTypeId = "footwear";

        List<ImageRecord_LAMBDA> records = [image];
        ImageOrderer.Run(records, [MakeFamily("FAM001")]);

        Assert.Equal("footwear", records[0].ProductTypeId);
    }

    //  Tie-breakers

    [Fact]
    public void Run_TieBreakerByNgpConfidence_HigherConfidenceWinsDet0() {
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
    public void Run_TieBreakerByFilenameHint_MatchingHintWins() {
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
    public void Run_TieBreakerByFilenameOrdinal_LowerOrdinalWinsWhenConfidenceAndHintTie() {
        // Two front-packshot images, same NGP confidence, neither filename-hinted: the sort settles it on
        // filename ordinal ("img1" before "img2") and never reaches the source index, so the evidence must
        // name the level that actually decided rather than the one below it.
        ImageRecord_LAMBDA imageA = MakeLambda("img1.jpg", "front-packshot", "FAM001");
        SetFeatureCount(imageA, 2);

        ImageRecord_LAMBDA imageB = MakeLambda("img2.jpg", "front-packshot", "FAM001");
        SetFeatureCount(imageB, 2);

        List<ImageRecord_LAMBDA> records = [imageA, imageB];

        ImageOrderer.Run(records, [MakeFamily("FAM001")]);

        Assert.Equal(0, imageA.DetOrder);
        Assert.False(imageA.OrderEvidence!.IsOverflow);
        Assert.Equal("filename-ordinal", imageA.OrderEvidence!.TieBreakerWon);

        Assert.True(imageB.OrderEvidence!.IsOverflow,
            "Losing front-packshot image should be overflow since front-packshot does not qualify for det1.");
    }

    [Fact]
    public void Run_TieBreakerBySourceIndex_IdenticalFilenamesFallBackToImportOrder() {
        // Two images can genuinely share a filename — a ZIP holding folderA/img.jpg and folderB/img.jpg keeps
        // only the leaf name — and that is the one case where the sort exhausts every signal and lands on
        // import order.
        ImageRecord_LAMBDA first = MakeLambda("img.jpg", "front-packshot", "FAM001");
        SetFeatureCount(first, 2);

        ImageRecord_LAMBDA second = MakeLambda("img.jpg", "front-packshot", "FAM001");
        SetFeatureCount(second, 2);

        List<ImageRecord_LAMBDA> records = [first, second];

        ImageOrderer.Run(records, [MakeFamily("FAM001")]);

        Assert.Equal(0, first.DetOrder);
        Assert.False(first.OrderEvidence!.IsOverflow);
        Assert.Equal("source-index", first.OrderEvidence!.TieBreakerWon);

        Assert.True(second.OrderEvidence!.IsOverflow);
    }

    [Fact]
    public void Run_TieBreaker_NamesTheLevelThatBeatTheClosestRival_NotAFarBehindCompetitor() {
        // T-3900 counter-example. Three front-packshot images compete for det0:
        //   winner    — 5 known features, filename hint "front"
        //   closest   — 5 known features, no hint → the real rival; only the hint separates it from winner
        //   farBehind — 2 known features, no hint → lost on confidence, never threatened anyone
        // Reporting "ngp-confidence" (because farBehind's confidence differs from the winner's) would name a
        // level the close race never reached — the filename hint is what actually won det0.
        ImageRecord_LAMBDA winner = MakeLambda("product_front.jpg", "front-packshot", "FAM001");
        SetFeatureCount(winner, 5);

        ImageRecord_LAMBDA closest = MakeLambda("product_shot.jpg", "front-packshot", "FAM001");
        SetFeatureCount(closest, 5);

        ImageRecord_LAMBDA farBehind = MakeLambda("product_extra.jpg", "front-packshot", "FAM001");
        SetFeatureCount(farBehind, 2);

        List<ImageRecord_LAMBDA> records = [winner, closest, farBehind];

        ImageOrderer.Run(records, [MakeFamily("FAM001")]);

        Assert.Equal(0, winner.DetOrder);
        Assert.False(winner.OrderEvidence!.IsOverflow);
        Assert.Equal("filename-hint", winner.OrderEvidence!.TieBreakerWon);
    }

    [Fact]
    public void Run_TieBreaker_ImageHoldingAnEarlierSlot_IsNotARivalForALaterSlot() {
        // closeup-image qualifies for both det3 and det7 in the default rules, so both images produce a det7
        // candidate too. strong wins det3 on confidence, which takes it out of the det7 race — but the old
        // full-list rescan still reported it as the competitor weak beat there. Its det7 candidate outranks
        // weak's and so sorts ahead of it: scanning forward from the winner never reaches it. Nothing was
        // still contesting det7 → "none".
        ImageRecord_LAMBDA strong = MakeLambda("shot_a.jpg", "closeup-image", "FAM001");
        SetFeatureCount(strong, 3);

        ImageRecord_LAMBDA weak = MakeLambda("shot_b.jpg", "closeup-image", "FAM001");
        SetFeatureCount(weak, 1);

        List<ImageRecord_LAMBDA> records = [strong, weak];

        ImageOrderer.Run(records, [MakeFamily("FAM001")]);

        Assert.Equal(3, strong.DetOrder);
        Assert.Equal("ngp-confidence", strong.OrderEvidence!.TieBreakerWon);

        Assert.Equal(7, weak.DetOrder);
        Assert.False(weak.OrderEvidence!.IsOverflow);
        Assert.Equal("none", weak.OrderEvidence!.TieBreakerWon);
    }

    [Fact]
    public void Run_TieBreaker_AlreadyAssignedRivalInsideTheBlock_IsSkippedNotReported() {
        // The other half of that rule — here the forward scan really does reach the stale rival, so only the
        // already-assigned guard can exclude it. Hints are per-slot: detailShot is hinted for det3 ("detail"),
        // labelShot for det7 ("label"), and confidence is tied, so each wins its own slot on its own hint. In
        // the det7 block labelShot therefore sorts first with detailShot right behind it — inside the scan's
        // reach, but already holding det3. Reporting "filename-hint" for det7 would name a race that never
        // happened: detailShot left it the moment it took det3.
        ImageRecord_LAMBDA detailShot = MakeLambda("x_detail.jpg", "closeup-image", "FAM001");
        SetFeatureCount(detailShot, 2);

        ImageRecord_LAMBDA labelShot = MakeLambda("y_label.jpg", "closeup-image", "FAM001");
        SetFeatureCount(labelShot, 2);

        List<ImageRecord_LAMBDA> records = [detailShot, labelShot];

        ImageOrderer.Run(records, [MakeFamily("FAM001")]);

        Assert.Equal(3, detailShot.DetOrder);
        Assert.Equal("filename-hint", detailShot.OrderEvidence!.TieBreakerWon);

        Assert.Equal(7, labelShot.DetOrder);
        Assert.False(labelShot.OrderEvidence!.IsOverflow);
        Assert.Equal("none", labelShot.OrderEvidence!.TieBreakerWon);
    }

    //  Overflow and edge cases 

    [Fact]
    public void Run_NullPhenotype_AssignedAsOverflowAfterConfiguredSlots() {
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
    public void Run_IllustrationTechnicalDrawing_AssignedToDet7() {
        // illustration-technical-drawing is in det7 of default rules.
        List<ImageRecord_LAMBDA> records = [MakeLambda("technical.jpg", "illustration-technical-drawing", "FAM001")];

        ImageOrderer.Run(records, [MakeFamily("FAM001")]);

        Assert.Equal(7, records[0].DetOrder);
        Assert.False(records[0].OrderEvidence!.IsOverflow);
        Assert.Equal("illustration-technical-drawing", records[0].OrderEvidence!.WinningPhenotype);
    }

    [Fact]
    public void Run_KoImageSkipped_NotAssignedDetSlot() {
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

    //  CompactDetOrder — gap policy

    [Fact]
    public void CompactDetOrder_OverflowIndices_RenumberedToContiguousFromZeroPreservingOrder() {
        // Three overflow images in one family at det8, det9, det10 → expect det0, det1, det2 in the
        // same relative order (compaction closes gaps, never reorders).
        ImageRecord_LAMBDA a = MakeLambda("a.jpg", null, "FAM001"); a.Family = "FAM001"; a.DetOrder = 8;
        ImageRecord_LAMBDA b = MakeLambda("b.jpg", null, "FAM001"); b.Family = "FAM001"; b.DetOrder = 9;
        ImageRecord_LAMBDA c = MakeLambda("c.jpg", null, "FAM001"); c.Family = "FAM001"; c.DetOrder = 10;

        ImageOrderer.CompactDetOrder([a, b, c]);

        Assert.Equal(0, a.DetOrder);
        Assert.Equal(1, b.DetOrder);
        Assert.Equal(2, c.DetOrder);
    }

    [Fact]
    public void CompactDetOrder_MultipleFamilies_EachRenumberedIndependentlyFromZero() {
        ImageRecord_LAMBDA a = MakeLambda("a.jpg", null, "FAM001"); a.Family = "FAM001"; a.DetOrder = 8;
        ImageRecord_LAMBDA b = MakeLambda("b.jpg", null, "FAM001"); b.Family = "FAM001"; b.DetOrder = 9;
        ImageRecord_LAMBDA c = MakeLambda("c.jpg", null, "FAM002"); c.Family = "FAM002"; c.DetOrder = 8;

        ImageOrderer.CompactDetOrder([a, b, c]);

        Assert.Equal(0, a.DetOrder);
        Assert.Equal(1, b.DetOrder);
        Assert.Equal(0, c.DetOrder);
    }

    [Fact]
    public void CompactDetOrder_GapBetweenSemanticSlots_ClosedWithoutReordering() {
        // A family holding det2 and det5 (det0/1 empty) → det0, det1, order preserved.
        ImageRecord_LAMBDA side = MakeLambda("side.jpg", "side-packshot", "FAM001"); side.Family = "FAM001"; side.DetOrder = 2;
        ImageRecord_LAMBDA bottom = MakeLambda("bottom.jpg", "bottom-packshot", "FAM001"); bottom.Family = "FAM001"; bottom.DetOrder = 5;

        ImageOrderer.CompactDetOrder([side, bottom]);

        Assert.Equal(0, side.DetOrder);
        Assert.Equal(1, bottom.DetOrder);
    }

    //  Overflow ordering policy

    [Fact]
    public void Run_Overflow_DetailHintedImage_ClosesTheRanks() {
        // CiMini regression: a DETAIL-named image with no qualifying phenotype must never jump
        // ahead of the family's main shots — its "detail" hint anchors it after the unhinted ones.
        ImageRecord_LAMBDA main1 = MakeLambda("24211507_CARDIGAN_76_MAGENTA_B.jpg", null, "FAM001");
        ImageRecord_LAMBDA main2 = MakeLambda("CARDIGAN_MAGENTA76_A.jpg", null, "FAM001");
        ImageRecord_LAMBDA detail = MakeLambda("CARDIGAN_MAGENTA76_DETAIL.jpg", null, "FAM001");

        List<ImageRecord_LAMBDA> records = [detail, main1, main2];
        ImageOrderer.Run(records, [MakeFamily("FAM001")]);
        ImageOrderer.CompactDetOrder(records);

        Assert.True(detail.DetOrder > main1.DetOrder);
        Assert.True(detail.DetOrder > main2.DetOrder);
        Assert.Equal(2, detail.DetOrder);
    }

    [Fact]
    public void Run_Overflow_OnModelImagesRankBeforePackshot() {
        // A packshot (hero-is-human FALSE) is less valuable than the product on a human model.
        ImageRecord_LAMBDA packshot = MakeLambda("Pareo Exotica.jpg", null, "FAM001");
        packshot.Features.Set("hero-is-human", "FALSE", 0.6, "yolo");

        ImageRecord_LAMBDA onModel1 = MakeLambda("Pareo_exotica_F1.jpg", null, "FAM001");
        onModel1.Features.Set("hero-is-human", "TRUE", 0.9, "yolo");

        ImageRecord_LAMBDA onModel2 = MakeLambda("Pareo_exotica_F2.jpg", null, "FAM001");
        onModel2.Features.Set("hero-is-human", "TRUE", 0.9, "yolo");

        List<ImageRecord_LAMBDA> records = [packshot, onModel1, onModel2];
        ImageOrderer.Run(records, [MakeFamily("FAM001")]);
        ImageOrderer.CompactDetOrder(records);

        Assert.Equal(0, onModel1.DetOrder);
        Assert.Equal(1, onModel2.DetOrder);
        Assert.Equal(2, packshot.DetOrder);
    }

    [Fact]
    public void Run_Overflow_FrontHintedImage_StaysFirst() {
        // A front-hinted overflow image must still lead unhinted siblings.
        ImageRecord_LAMBDA front = MakeLambda("product_front.jpg", null, "FAM001");
        ImageRecord_LAMBDA other = MakeLambda("product_extra.jpg", null, "FAM001");

        List<ImageRecord_LAMBDA> records = [other, front];
        ImageOrderer.Run(records, [MakeFamily("FAM001")]);
        ImageOrderer.CompactDetOrder(records);

        Assert.Equal(0, front.DetOrder);
        Assert.Equal(1, other.DetOrder);
    }

    [Fact]
    public void CompactDetOrder_KoImagesExcludedFromRenumbering() {
        ImageRecord_LAMBDA ok = MakeLambda("ok.jpg", null, "FAM001"); ok.Family = "FAM001"; ok.DetOrder = 8;
        ImageRecord_LAMBDA ko = MakeLambda("ko.jpg", null, "FAM001"); ko.Family = "FAM001"; ko.DetOrder = 9; ko.IsKo = true;

        ImageOrderer.CompactDetOrder([ok, ko]);

        Assert.Equal(0, ok.DetOrder);
        Assert.Equal(9, ko.DetOrder); // untouched
    }

    //  Helpers

    /// <summary>
    /// Creates a minimal <see cref="ImageRecord_LAMBDA"/> with MatchEvidence set for ordering.
    /// </summary>
    private static ImageRecord_LAMBDA MakeLambda(string filename, string? phenotype, string familyId) {
        var lambda = new ImageRecord_LAMBDA {
            InitialFullName = filename,
            SelectedPhenotype = phenotype,
            MatchEvidence = new MatchEvidence {
                ImageId = filename,
                SourceFilename = filename,
                FinalFamilyId = familyId,
                FinalScore = 1.0,
                IsKo = false
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
    private static void SetFeatureCount(ImageRecord_LAMBDA lambda, int count) {
        for (int i = 0; i < count; i++) {
            lambda.Features.Set($"test-feature-{i}", "true", 1.0, "test");
        }
    }

    /// <summary>
    /// Resolves the absolute path to a config file relative to the core source root.
    /// Walks up from the assembly directory looking for the jb/src/core root.
    /// </summary>
    private static string ResolveConfigPath(string relativeFromCore) {
        var assemblyDir = new FileInfo(typeof(ImageOrdererTests).Assembly.Location).DirectoryName
            ?? throw new InvalidOperationException("Cannot determine assembly directory.");

        var current = new DirectoryInfo(assemblyDir);
        while (current.Parent != null) {
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
