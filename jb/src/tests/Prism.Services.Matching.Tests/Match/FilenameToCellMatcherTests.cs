using Xunit;

namespace PrismCoreTests.Match;

/// <summary>
/// Unit tests for <see cref="FilenameToCellMatcher"/> — the last-resort bracket that assigns an image
/// to the unique FamilyID whose Excel row names that exact image file in any cell.
/// </summary>
public class FilenameToCellMatcherTests {
    [Fact]
    public void TryMatch_ImagePathCellNamesImage_ReturnsEvidence() {
        FilenameToCellMatcher matcher = new();
        FamilyIDRecord family = FamilyWithProperty("92836758", "imagepath", "/medias (3)/92836758_det815.jpg");
        ImageRecord_LAMBDA record = MakeLambda("92836758_det815.jpg");

        (MatchEvidence? evidence, _) = matcher.TryMatch(record, [family]);

        Assert.NotNull(evidence);
        Assert.Equal("92836758", evidence!.FinalFamilyId);
        Assert.False(evidence.IsKo);
        Assert.Equal("FilenameToCellMatcher", evidence.AcceptedMatcherName);
        Assert.Equal(1.0, evidence.FinalScore);
    }

    [Fact]
    public void TryMatch_PlainFilenameCell_ReturnsEvidence() {
        FilenameToCellMatcher matcher = new();
        FamilyIDRecord family = FamilyWithProperty("11112222", "productimage1", "WB113068-BEIGE32_(1).jpg");
        ImageRecord_LAMBDA record = MakeLambda("WB113068-BEIGE32_(1).jpg");

        (MatchEvidence? evidence, _) = matcher.TryMatch(record, [family]);

        Assert.NotNull(evidence);
        Assert.Equal("11112222", evidence!.FinalFamilyId);
    }

    [Fact]
    public void TryMatch_UrlCell_ReturnsEvidence() {
        FilenameToCellMatcher matcher = new();
        FamilyIDRecord family = FamilyWithProperty("33334444", "url", "https://cdn.example.com/x/AB12.jpg");
        ImageRecord_LAMBDA record = MakeLambda("AB12.jpg");

        (MatchEvidence? evidence, _) = matcher.TryMatch(record, [family]);

        Assert.NotNull(evidence);
        Assert.Equal("33334444", evidence!.FinalFamilyId);
    }

    [Fact]
    public void TryMatch_SameFilenameInTwoFamilies_ReturnsNull() {
        FilenameToCellMatcher matcher = new();
        FamilyIDRecord famA = FamilyWithProperty("10000001", "imagepath", "/a/AB12.jpg");
        FamilyIDRecord famB = FamilyWithProperty("10000002", "imagepath", "/b/AB12.jpg");
        ImageRecord_LAMBDA record = MakeLambda("AB12.jpg");

        (MatchEvidence? evidence, List<CandidateSummary> tied) = matcher.TryMatch(record, [famA, famB]);

        Assert.Null(evidence); // ambiguous: same filename in two families
        Assert.Equal(2, tied.Count);
    }

    [Fact]
    public void TryMatch_NoCellNamesImage_ReturnsNull() {
        FilenameToCellMatcher matcher = new();
        FamilyIDRecord family = FamilyWithProperty("20000001", "imagepath", "/medias/XY99.jpg");
        ImageRecord_LAMBDA record = MakeLambda("AB12.jpg");

        Assert.Null(matcher.TryMatch(record, [family]).Evidence);
    }

    [Fact]
    public void TryMatch_NonImageCellEqualToStem_DoesNotFalseMatch() {
        // A bare SKU cell "AB12" (no image extension) must not match image "AB12.jpg".
        FilenameToCellMatcher matcher = new();
        FamilyIDRecord family = FamilyWithProperty("20000002", "sku", "AB12");
        ImageRecord_LAMBDA record = MakeLambda("AB12.jpg");

        Assert.Null(matcher.TryMatch(record, [family]).Evidence);
    }

    //  Helpers

    private static ImageRecord_LAMBDA MakeLambda(string filename) =>
        new() { InitialFullName = filename };

    private static FamilyIDRecord FamilyWithProperty(string familyId, string propName, string propValue) {
        FamilyIDRecord family = new(familyId);
        family.MergeProperty(
            new ExcelPropertyValue(propName, [propValue], []),
            ExcelColumnClassification.Descriptive);
        return family;
    }
}
