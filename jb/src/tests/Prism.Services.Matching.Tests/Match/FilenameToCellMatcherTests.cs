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

    //  T-5110: cell scanning finds filenames inside free text, not just whole-cell paths

    [Fact]
    public void TryMatch_FreeTextCellListingSeveralUrls_MatchesEachFilename() {
        // CiMini's real marketing-description cell shape: a sentence, then several comma-separated
        // URLs. Previously the whole cell was treated as one path, its "basename" (text after the
        // final '/') was the last URL's extensionless tail, and the extension guard rejected the
        // entire cell — none of the 7 filenames ever got indexed.
        FilenameToCellMatcher matcher = new();
        const string cellValue =
            "Pictures are here: http://example.test/100267_1.jpg, http://example.test/100267_2.jpg, " +
            "http://example.test/100267_3.jpg, http://example.test/100267_7";
        FamilyIDRecord family = FamilyWithProperty("91337133", "description", cellValue);

        (MatchEvidence? e1, _) = matcher.TryMatch(MakeLambda("100267_1.jpg"), [family]);
        (MatchEvidence? e2, _) = matcher.TryMatch(MakeLambda("100267_2.jpg"), [family]);
        (MatchEvidence? e3, _) = matcher.TryMatch(MakeLambda("100267_3.jpg"), [family]);

        Assert.NotNull(e1);
        Assert.Equal("91337133", e1!.FinalFamilyId);
        Assert.NotNull(e2);
        Assert.Equal("91337133", e2!.FinalFamilyId);
        Assert.NotNull(e3);
        Assert.Equal("91337133", e3!.FinalFamilyId);
    }

    [Fact]
    public void TryMatch_ExtensionLessTokenInFilenameCell_Matches() {
        // "100267_7" is listed with no extension, but shares its cell with several ".jpg" siblings —
        // that context is what makes it a filename reference, not a bare word.
        FilenameToCellMatcher matcher = new();
        const string cellValue = "See http://example.test/100267_6.jpg and http://example.test/100267_7";
        FamilyIDRecord family = FamilyWithProperty("91337133", "description", cellValue);

        (MatchEvidence? evidence, _) = matcher.TryMatch(MakeLambda("100267_7.jpg"), [family]);

        Assert.NotNull(evidence);
        Assert.Equal("91337133", evidence!.FinalFamilyId);
    }

    [Fact]
    public void TryMatch_DoubleSpaceAndSuffixInRealFilename_MatchesViaCollapsedPrefix() {
        // The cell names "100267_6.jpg"; the real file on disk is "100267_6  - BW001_c.jpg" (extra
        // " - BW001_c" and a double space — neither is in any Excel cell). Exact and collapsed-equal
        // lookups both miss; the collapsed-prefix fallback is what carries this row.
        FilenameToCellMatcher matcher = new();
        const string cellValue =
            "Pictures: http://example.test/100267_1.jpg, http://example.test/100267_6.jpg";
        FamilyIDRecord family = FamilyWithProperty("91337133", "description", cellValue);

        (MatchEvidence? evidence, _) = matcher.TryMatch(MakeLambda("100267_6  - BW001_c.jpg"), [family]);

        Assert.NotNull(evidence);
        Assert.Equal("91337133", evidence!.FinalFamilyId);
    }

    [Fact]
    public void TryMatch_BareAlphabeticWordInCell_NeverIndexed() {
        // "Pictures" and "here" are plain prose, not filename references, extension or not.
        FilenameToCellMatcher matcher = new();
        FamilyIDRecord family = FamilyWithProperty("91337133", "description", "Pictures are here: nothing.jpg");

        Assert.Null(matcher.TryMatch(MakeLambda("Pictures.jpg"), [family]).Evidence);
        Assert.Null(matcher.TryMatch(MakeLambda("here.jpg"), [family]).Evidence);
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
