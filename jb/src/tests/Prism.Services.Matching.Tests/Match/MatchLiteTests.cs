using Xunit;

namespace PrismCoreTests.Match;

/// <summary>
/// Tests for <see cref="PrismService.MatchLite"/> — the filename-only matching route with no image
/// bytes read and no CLIP classification. Two kinds of coverage:
/// <list type="bullet">
/// <item>Black-box tests against the real CiMini fixture via the shared <see cref="PipelineFixture"/>,
/// calling <c>MatchLite</c> exactly as the API does.</item>
/// <item>Sequence-level tests that exercise the same stage order <c>MatchLite</c> runs
/// (<see cref="ImageOrderer.Run"/> → <see cref="ImageRenamer.Run"/> → <see cref="ImageOrderer.CompactDetOrder"/>)
/// on synthetic records, matching the style of <c>Order/ImageOrdererTests.cs</c>. A genuine same-family
/// <c>DetOrder</c> collision cannot be constructed through <see cref="ImageOrderer.Run"/> today — every
/// slot claim and the overflow counter both guarantee a unique <c>DetOrder</c> per image in a family — so
/// the collision-detection fix added to <c>MatchLite</c> is defense-in-depth parity with the full
/// pipeline rather than a currently-exploitable gap; these tests verify the sequence still produces
/// correct output, not a forced collision.
/// </list>
/// </summary>
public class MatchLiteTests : IClassFixture<PipelineFixture> {
    private readonly PipelineFixture fixture;

    public MatchLiteTests(PipelineFixture fixture) {
        this.fixture = fixture;
    }

    //  Black-box: real CiMini fixture

    [Fact]
    public void MatchLite_CiMiniFixture_MatchesImagesAndProducesDet0BasedNames() {
        List<ImageRecord_INPUT> imageInputs = Directory
            .GetFiles(fixture.ImagesPath, "*.jpg", SearchOption.TopDirectoryOnly)
            .Select(f => new ImageRecord_INPUT { InitialFullName = Path.GetFileName(f) })
            .ToList();

        List<InputExcelFileRecord> excelInputs =
        [
            new InputExcelFileRecord { SourceReference = "ci-mini.xlsx", TempFilePath = fixture.CopyExcelToTemp() }
        ];

        MatchOnlyResult result = fixture.Prism.MatchLite(imageInputs, excelInputs);

        Assert.True(result.Matched > 0, "Expected at least one CiMini image to match via filename-only Bracket 1-3/5 matching.");

        // Compaction (DetOrderGapsAllowed=false by default) must renumber every family from det0,
        // never leaving the first image at det8+ (the pre-compaction overflow starting point).
        IEnumerable<string> matchedNames = result.FileNameMap.Values.Where(n => n is not null)!;
        Assert.Contains(matchedNames, name => name!.Contains("_det0.jpg", StringComparison.Ordinal));
    }

    [Fact]
    public void MatchLite_FilenameOnlyNoImageBytesOnDisk_StillMatchesByFilename() {
        // MatchLite's contract is filename-only: it must not need the image bytes on disk at all.
        List<ImageRecord_INPUT> imageInputs = Directory
            .GetFiles(fixture.ImagesPath, "*.jpg", SearchOption.TopDirectoryOnly)
            .Select(f => new ImageRecord_INPUT { InitialFullName = Path.GetFileName(f) })
            .Take(1)
            .ToList();

        Assert.NotEmpty(imageInputs);

        List<InputExcelFileRecord> excelInputs =
        [
            new InputExcelFileRecord { SourceReference = "ci-mini.xlsx", TempFilePath = fixture.CopyExcelToTemp() }
        ];

        // No TempFilePath set on the image input and no bytes read — this must not throw.
        MatchOnlyResult result = fixture.Prism.MatchLite(imageInputs, excelInputs);

        Assert.Single(result.FileNameMap);
    }

    //  Sequence-level: ImageOrderer.Run -> ImageRenamer.Run -> CompactDetOrder (synthetic)

    [Fact]
    public void MatchLiteSequence_NoSelectedPhenotype_AllImagesOverflowAndCompactFromDetZero() {
        // MatchLite never runs CLIP/refinement, so SelectedPhenotype stays null for every image —
        // exactly like this synthetic input — and BuildCandidates skips all of them (overflow only).
        List<ImageRecord_LAMBDA> records =
        [
            MakeLambda("product_front.jpg", "FAM001"),
            MakeLambda("product_back.jpg",  "FAM001"),
            MakeLambda("product_side.jpg",  "FAM001")
        ];

        ImageOrderer.Run(records, [new FamilyIDRecord("FAM001")]);
        (int okRenamed, int koAdded) = ImageRenamer.Run(records);
        ImageOrderer.CompactDetOrder(records);

        Assert.Equal(3, okRenamed);
        Assert.Equal(0, koAdded);
        Assert.All(records, r => Assert.NotNull(r.OrderEvidence));
        Assert.All(records, r => Assert.True(r.OrderEvidence!.IsOverflow));

        List<int> detOrders = records.Select(r => r.DetOrder).OrderBy(d => d).ToList();
        Assert.Equal([0, 1, 2], detOrders);
    }

    [Fact]
    public void MatchLiteSequence_MultipleFamilies_EachCompactsIndependentlyFromDetZero() {
        List<ImageRecord_LAMBDA> records =
        [
            MakeLambda("a1.jpg", "FAM001"),
            MakeLambda("a2.jpg", "FAM001"),
            MakeLambda("b1.jpg", "FAM002")
        ];

        ImageOrderer.Run(records, [new FamilyIDRecord("FAM001"), new FamilyIDRecord("FAM002")]);
        (int okRenamed, int koAdded) = ImageRenamer.Run(records);
        ImageOrderer.CompactDetOrder(records);

        Assert.Equal(3, okRenamed);
        Assert.Equal(0, koAdded);

        List<int> fam1DetOrders = records.Where(r => r.Family == "FAM001").Select(r => r.DetOrder).OrderBy(d => d).ToList();
        List<int> fam2DetOrders = records.Where(r => r.Family == "FAM002").Select(r => r.DetOrder).OrderBy(d => d).ToList();
        Assert.Equal([0, 1], fam1DetOrders);
        Assert.Equal([0], fam2DetOrders);
    }

    //  Helpers

    /// <summary>Creates a minimal matched-but-unphenotyped LAMBDA, as MatchLite would after ImageMatcher.Run.</summary>
    private static ImageRecord_LAMBDA MakeLambda(string filename, string familyId) {
        return new ImageRecord_LAMBDA {
            InitialFullName = filename,
            MatchEvidence = new MatchEvidence {
                ImageId = filename,
                SourceFilename = filename,
                FinalFamilyId = familyId,
                FinalScore = 1.0,
                IsKo = false
            }
        };
    }
}
