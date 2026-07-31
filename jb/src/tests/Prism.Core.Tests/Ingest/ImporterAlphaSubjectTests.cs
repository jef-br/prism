using Xunit;

namespace PrismCoreTests.Ingest;

/// <summary>
/// T-4830: the ingress alpha-capture path. When a source image carries a real alpha channel, the
/// Imported stage must measure a <see cref="SubjectDetectionResult"/> from it before flattening transparency
/// onto white — an exact, free subject mask that beats the classical-CV heuristic. Covers the box
/// measured from a known opaque region, the fully-opaque no-signal case, per-edge bleed intersects, and
/// the non-alpha control (plain JPEG) staying unaffected. Canvases stay at or above
/// Input.Images.MINIMUM_SIZE_IN_PIXELS (570px longest side) so the pixel-minimum KO never masks what's
/// under test.
/// </summary>
public class ImporterAlphaSubjectTests : IClassFixture<ImporterFixture> {
    private readonly ImporterFixture fixture;

    public ImporterAlphaSubjectTests(ImporterFixture fixture) {
        this.fixture = fixture;
    }

    [Fact]
    public void TransparentBorderOpaqueCenter_YieldsExpectedSubjectBox() {
        // 600x600 canvas, opaque only in [150,450)x[180,420) — an asymmetric interior rectangle well
        // clear of every edge, so this also proves the box axes are not swapped.
        string path = fixture.WriteAlphaShapePng("alpha_box.png", 600, 600,
            (x, y) => x >= 150 && x < 450 && y >= 180 && y < 420);

        ImportStageResult result = fixture.RunImport([new ImageRecord_INPUT { InitialFullName = path }]);

        ImageRecord_INPUT normalized = Assert.Single(result.NormalizedImages);
        Assert.NotNull(normalized.Subject);
        SubjectDetectionResult subject = normalized.Subject!;

        Assert.Equal("alpha", subject.Producer);
        Assert.Equal(1.0, subject.Confidence);
        Assert.False(subject.HasHardShadowEvidence);
        Assert.False(subject.IsWholeFrameFallback);

        Assert.Equal(150, subject.Box.X);
        Assert.Equal(180, subject.Box.Y);
        Assert.Equal(300, subject.Box.Width);
        Assert.Equal(240, subject.Box.Height);
        Assert.Equal(subject.Box.X, subject.Box.Left);
        Assert.Equal(subject.Box.Y, subject.Box.Top);
        Assert.Equal(450, subject.Box.Right);
        Assert.Equal(420, subject.Box.Bottom);

        Assert.False(subject.IntersectsTop);
        Assert.False(subject.IntersectsBottom);
        Assert.False(subject.IntersectsLeft);
        Assert.False(subject.IntersectsRight);
        Assert.NotNull(subject.MaskPng);
        Assert.NotEmpty(subject.MaskPng!);
    }

    [Fact]
    public void FullyOpaquePng_YieldsNullOrWholeFrameFallback() {
        // No pixel is ever transparent: the alpha channel carries no usable signal. Depending on
        // whether the PNG encoder even keeps an alpha channel for a fully-opaque image, the Importer
        // may see no alpha at all (Subject null) or a full-canvas opaque region (whole-frame fallback) —
        // both mean "behave exactly like no detection."
        string path = fixture.WriteAlphaShapePng("alpha_full_opaque.png", 600, 600, (x, y) => true);

        ImportStageResult result = fixture.RunImport([new ImageRecord_INPUT { InitialFullName = path }]);

        ImageRecord_INPUT normalized = Assert.Single(result.NormalizedImages);
        Assert.True(normalized.Subject is null || normalized.Subject.IsWholeFrameFallback,
            "A fully-opaque source must yield either no Subject or IsWholeFrameFallback = true.");
    }

    [Fact]
    public void PlainJpeg_FastPath_ProducesNoSubject() {
        string path = fixture.WriteNoiseJpeg("alpha_control_fastpath.jpg", 600, 600);

        ImportStageResult result = fixture.RunImport([new ImageRecord_INPUT { InitialFullName = path }]);

        ImageRecord_INPUT normalized = Assert.Single(result.NormalizedImages);
        Assert.Null(normalized.Subject);
    }

    [Fact]
    public void PlainJpeg_FullDecodePath_ProducesNoSubject() {
        // EXIF orientation 6 disqualifies the conforming-JPEG fast path, forcing the full decode
        // through LoadImageWithExifOrientation — JPEG never carries alpha, so Subject must stay null
        // on this path too, not just the fast path.
        string path = fixture.WriteExifRotatedJpeg("alpha_control_fulldecode.jpg", 600, 400);

        ImportStageResult result = fixture.RunImport([new ImageRecord_INPUT { InitialFullName = path }]);

        ImageRecord_INPUT normalized = Assert.Single(result.NormalizedImages);
        Assert.Null(normalized.Subject);
    }

    [Fact]
    public void AlphaShapeBleedingOffRightEdge_SetsOnlyRightIntersectFlag() {
        // 700x500 canvas, opaque in [500,700)x[100,300) — runs off the right edge only, well clear of
        // the other three.
        string path = fixture.WriteAlphaShapePng("alpha_right_bleed.png", 700, 500,
            (x, y) => x >= 500 && x < 700 && y >= 100 && y < 300);

        ImportStageResult result = fixture.RunImport([new ImageRecord_INPUT { InitialFullName = path }]);

        ImageRecord_INPUT normalized = Assert.Single(result.NormalizedImages);
        Assert.NotNull(normalized.Subject);
        SubjectDetectionResult subject = normalized.Subject!;

        Assert.True(subject.IntersectsRight);
        Assert.False(subject.IntersectsTop);
        Assert.False(subject.IntersectsBottom);
        Assert.False(subject.IntersectsLeft);
        Assert.Equal(700, subject.Box.Right);
    }
}
