using Xunit;

namespace PrismCoreTests.Rename;

/// <summary>
/// Unit tests for <see cref="ImageRenamer"/> rename-stage logic.
/// Records are built inline per test with Family and DetOrder pre-set
/// (as they would be after the Ordered stage).
/// </summary>
public class ImageRenamerTests
{
    // ─── OkRenamedCount ───────────────────────────────────────────────────────

    [Fact]
    public void Run_SingleAcceptedImage_CountsAsRenamed()
    {
        PipelineContext context = MakeContext([MakeLambda("img.jpg", "FAM001", 0)]);

        ImageRenamer.Run(context);

        Assert.Equal(1, context.OkRenamedCount);
    }

    [Fact]
    public void Run_TwoImagesUniqueDet_BothCounted()
    {
        PipelineContext context = MakeContext(
        [
            MakeLambda("img1.jpg", "FAM001", 0),
            MakeLambda("img2.jpg", "FAM001", 1)
        ]);

        ImageRenamer.Run(context);

        Assert.Equal(2, context.OkRenamedCount);
    }

    [Fact]
    public void Run_MultipleFamilies_CountsAccumulateAcrossFamilies()
    {
        PipelineContext context = MakeContext(
        [
            MakeLambda("img1.jpg", "FAM001", 0),
            MakeLambda("img2.jpg", "FAM002", 0),
            MakeLambda("img3.jpg", "FAM002", 1)
        ]);

        ImageRenamer.Run(context);

        Assert.Equal(3, context.OkRenamedCount);
    }

    // ─── Collision handling ───────────────────────────────────────────────────

    [Fact]
    public void Run_SameDetInSameFamily_KosEntireFamily()
    {
        PipelineContext context = MakeContext(
        [
            MakeLambda("img1.jpg", "FAM001", 0),
            MakeLambda("img2.jpg", "FAM001", 0)
        ]);

        ImageRenamer.Run(context);

        Assert.Equal(0, context.OkRenamedCount);
        Assert.Equal(2, context.KoRecordCount);
        Assert.All(context.LambdaRecords, r =>
        {
            Assert.True(r.IsKo);
            Assert.Equal("RENAME_COLLISION", r.KoReasonCode);
            Assert.NotNull(r.KoSafeMessage);
        });
    }

    [Fact]
    public void Run_CollisionInOneFamilyDoesNotAffectOtherFamily()
    {
        PipelineContext context = MakeContext(
        [
            MakeLambda("ok.jpg",   "FAM001", 0),   // clean family
            MakeLambda("col1.jpg", "FAM002", 0),   // collision family
            MakeLambda("col2.jpg", "FAM002", 0)    // collision family
        ]);

        ImageRenamer.Run(context);

        Assert.Equal(1, context.OkRenamedCount);
        Assert.Equal(2, context.KoRecordCount);
        Assert.False(context.LambdaRecords[0].IsKo);
        Assert.True(context.LambdaRecords[1].IsKo);
        Assert.True(context.LambdaRecords[2].IsKo);
    }

    // ─── KO passthrough ───────────────────────────────────────────────────────

    [Fact]
    public void Run_AlreadyKoImage_SkippedAndNotCounted()
    {
        PipelineContext context = MakeContext([MakeLambda("ko.jpg", "FAM001", 0, isKo: true)]);

        ImageRenamer.Run(context);

        Assert.Equal(0, context.OkRenamedCount);
        Assert.Equal(0, context.KoRecordCount);
        Assert.Equal("TEST_KO", context.LambdaRecords[0].KoReasonCode);
    }

    [Fact]
    public void Run_MixOfKoAndAcceptedInBatch_OnlyAcceptedCounted()
    {
        PipelineContext context = MakeContext(
        [
            MakeLambda("ok.jpg",  "FAM001", 0),
            MakeLambda("ko.jpg",  "FAM002", 0, isKo: true)
        ]);

        ImageRenamer.Run(context);

        Assert.Equal(1, context.OkRenamedCount);
        Assert.Equal(0, context.KoRecordCount);
    }

    // ─── Unmatched images (no Family) ────────────────────────────────────────

    [Fact]
    public void Run_EmptyFamilyField_Skipped()
    {
        // An image that made it past matching but has no Family set is skipped.
        var unmatched = new ImageRecord_LAMBDA { InitialFullName = "unmatched.jpg" };
        PipelineContext context = MakeContext([unmatched]);

        ImageRenamer.Run(context);

        Assert.Equal(0, context.OkRenamedCount);
    }

    // ─── Overflow images ──────────────────────────────────────────────────────

    [Fact]
    public void Run_OverflowImage_CountedAndNewNameCorrect()
    {
        // Overflow images get det slots >= 8 from the Ordered stage.
        PipelineContext context = MakeContext([MakeLambda("extra.jpg", "FAM001", 8)]);

        ImageRenamer.Run(context);

        Assert.Equal(1, context.OkRenamedCount);
        Assert.Equal("FAM001_det8.jpg", context.LambdaRecords[0].NewName);
    }

    // ─── NewName contract ─────────────────────────────────────────────────────

    [Fact]
    public void Run_AcceptedImage_NewNameIsCorrectForm()
    {
        PipelineContext context = MakeContext([MakeLambda("img.jpg", "SPACINI29", 0)]);

        ImageRenamer.Run(context);

        Assert.Equal("SPACINI29_det0.jpg", context.LambdaRecords[0].NewName);
    }

    [Fact]
    public void Run_PartialCollisionInThreeMemberFamily_KosAllThreeMembers()
    {
        // Images 1 and 2 share det0 (collision); image 3 has unique det1.
        // The entire family must be KO'd, including the clean member.
        PipelineContext context = MakeContext(
        [
            MakeLambda("img1.jpg", "FAM001", 0),
            MakeLambda("img2.jpg", "FAM001", 0),
            MakeLambda("img3.jpg", "FAM001", 1)
        ]);

        ImageRenamer.Run(context);

        Assert.Equal(0, context.OkRenamedCount);
        Assert.Equal(3, context.KoRecordCount);
        Assert.All(context.LambdaRecords, r =>
        {
            Assert.True(r.IsKo);
            Assert.Equal("RENAME_COLLISION", r.KoReasonCode);
        });
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a minimal <see cref="ImageRecord_LAMBDA"/> with Family and DetOrder set,
    /// as they would be after the Ordered stage.
    /// </summary>
    private static ImageRecord_LAMBDA MakeLambda(
        string filename,
        string familyId,
        int detOrder,
        bool isKo = false)
    {
        return new ImageRecord_LAMBDA
        {
            InitialFullName = filename,
            Family          = familyId,
            DetOrder        = detOrder,
            IsKo            = isKo,
            KoReasonCode    = isKo ? "TEST_KO" : null
        };
    }

    /// <summary>
    /// Builds a minimal <see cref="PipelineContext"/> populated with the given lambda records.
    /// </summary>
    private static PipelineContext MakeContext(IReadOnlyList<ImageRecord_LAMBDA> images)
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

        return context;
    }
}
