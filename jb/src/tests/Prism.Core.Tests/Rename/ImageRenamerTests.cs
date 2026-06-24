using Xunit;

namespace PrismCoreTests.Rename;

/// <summary>
/// Unit tests for <see cref="ImageRenamer"/> rename-stage logic.
/// Records are built inline per test with Family and DetOrder pre-set
/// (as they would be after the Ordered stage). <see cref="ImageRenamer.Run"/> returns the
/// (OkRenamed, KoAdded) counts directly — there is no shared pipeline context.
/// </summary>
public class ImageRenamerTests
{
    //  OkRenamedCount 

    [Fact]
    public void Run_SingleAcceptedImage_CountsAsRenamed()
    {
        List<ImageRecord_LAMBDA> records = [MakeLambda("img.jpg", "FAM001", 0)];

        (int okRenamed, _) = ImageRenamer.Run(records);

        Assert.Equal(1, okRenamed);
    }

    [Fact]
    public void Run_TwoImagesUniqueDet_BothCounted()
    {
        List<ImageRecord_LAMBDA> records =
        [
            MakeLambda("img1.jpg", "FAM001", 0),
            MakeLambda("img2.jpg", "FAM001", 1)
        ];

        (int okRenamed, _) = ImageRenamer.Run(records);

        Assert.Equal(2, okRenamed);
    }

    [Fact]
    public void Run_MultipleFamilies_CountsAccumulateAcrossFamilies()
    {
        List<ImageRecord_LAMBDA> records =
        [
            MakeLambda("img1.jpg", "FAM001", 0),
            MakeLambda("img2.jpg", "FAM002", 0),
            MakeLambda("img3.jpg", "FAM002", 1)
        ];

        (int okRenamed, _) = ImageRenamer.Run(records);

        Assert.Equal(3, okRenamed);
    }

    //  Collision handling 

    [Fact]
    public void Run_SameDetInSameFamily_KosEntireFamily()
    {
        List<ImageRecord_LAMBDA> records =
        [
            MakeLambda("img1.jpg", "FAM001", 0),
            MakeLambda("img2.jpg", "FAM001", 0)
        ];

        (int okRenamed, int koAdded) = ImageRenamer.Run(records);

        Assert.Equal(0, okRenamed);
        Assert.Equal(2, koAdded);
        Assert.All(records, r =>
        {
            Assert.True(r.IsKo);
            Assert.Equal("RENAME_COLLISION", r.KoReasonCode);
            Assert.NotNull(r.KoSafeMessage);
        });
    }

    [Fact]
    public void Run_CollisionInOneFamilyDoesNotAffectOtherFamily()
    {
        List<ImageRecord_LAMBDA> records =
        [
            MakeLambda("ok.jpg",   "FAM001", 0),   // clean family
            MakeLambda("col1.jpg", "FAM002", 0),   // collision family
            MakeLambda("col2.jpg", "FAM002", 0)    // collision family
        ];

        (int okRenamed, int koAdded) = ImageRenamer.Run(records);

        Assert.Equal(1, okRenamed);
        Assert.Equal(2, koAdded);
        Assert.False(records[0].IsKo);
        Assert.True(records[1].IsKo);
        Assert.True(records[2].IsKo);
    }

    //  KO passthrough 

    [Fact]
    public void Run_AlreadyKoImage_SkippedAndNotCounted()
    {
        List<ImageRecord_LAMBDA> records = [MakeLambda("ko.jpg", "FAM001", 0, isKo: true)];

        (int okRenamed, int koAdded) = ImageRenamer.Run(records);

        Assert.Equal(0, okRenamed);
        Assert.Equal(0, koAdded);
        Assert.Equal("TEST_KO", records[0].KoReasonCode);
    }

    [Fact]
    public void Run_MixOfKoAndAcceptedInBatch_OnlyAcceptedCounted()
    {
        List<ImageRecord_LAMBDA> records =
        [
            MakeLambda("ok.jpg",  "FAM001", 0),
            MakeLambda("ko.jpg",  "FAM002", 0, isKo: true)
        ];

        (int okRenamed, int koAdded) = ImageRenamer.Run(records);

        Assert.Equal(1, okRenamed);
        Assert.Equal(0, koAdded);
    }

    //  Unmatched images (no Family) 

    [Fact]
    public void Run_EmptyFamilyField_Skipped()
    {
        // An image that made it past matching but has no Family set is skipped.
        List<ImageRecord_LAMBDA> records = [new ImageRecord_LAMBDA { InitialFullName = "unmatched.jpg" }];

        (int okRenamed, _) = ImageRenamer.Run(records);

        Assert.Equal(0, okRenamed);
    }

    //  Overflow images 

    [Fact]
    public void Run_OverflowImage_CountedAndNewNameCorrect()
    {
        // Overflow images get det slots >= 8 from the Ordered stage.
        List<ImageRecord_LAMBDA> records = [MakeLambda("extra.jpg", "FAM001", 8)];

        (int okRenamed, _) = ImageRenamer.Run(records);

        Assert.Equal(1, okRenamed);
        Assert.Equal("FAM001_det8.jpg", records[0].NewName);
    }

    //  NewName contract 

    [Fact]
    public void Run_AcceptedImage_NewNameIsCorrectForm()
    {
        List<ImageRecord_LAMBDA> records = [MakeLambda("img.jpg", "SPACINI29", 0)];

        ImageRenamer.Run(records);

        Assert.Equal("SPACINI29_det0.jpg", records[0].NewName);
    }

    [Fact]
    public void Run_PartialCollisionInThreeMemberFamily_KosAllThreeMembers()
    {
        // Images 1 and 2 share det0 (collision); image 3 has unique det1.
        // The entire family must be KO'd, including the clean member.
        List<ImageRecord_LAMBDA> records =
        [
            MakeLambda("img1.jpg", "FAM001", 0),
            MakeLambda("img2.jpg", "FAM001", 0),
            MakeLambda("img3.jpg", "FAM001", 1)
        ];

        (int okRenamed, int koAdded) = ImageRenamer.Run(records);

        Assert.Equal(0, okRenamed);
        Assert.Equal(3, koAdded);
        Assert.All(records, r =>
        {
            Assert.True(r.IsKo);
            Assert.Equal("RENAME_COLLISION", r.KoReasonCode);
        });
    }

    //  Helpers 

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
}
