using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace PrismCoreTests.Classify;

/// <summary>
/// Unit tests for <see cref="SubjectEdgeDetector"/>: standalone subject/edge intersection detector.
/// Synthetic JPEG images are created in a per-test temp directory and cleaned up on dispose.
/// All tests exercise the fallback load path; the EXIF thumbnail fast path is covered by
/// integration with real camera images and is not reproducible with ImageSharp-generated JPEGs
/// (which do not embed thumbnails).
/// </summary>
public sealed class SubjectEdgeDetectorTests : IDisposable
{
    // 400×400 gives strip = max(2, int(400 * 0.08)) = 32 px.
    // Corner zone for background sampling = 40 px (10 % of 400).
    private const int W       = 400;
    private const int H       = 400;
    private const int StripPx = 32;   // 8 % of 400
    private const int CornerPx = 40;  // 10 % of 400

    private static readonly Rgba32 White = new(255, 255, 255, 255);
    private static readonly Rgba32 Dark  = new(20,  20,  20,  255);

    private readonly string _tempDir;

    public SubjectEdgeDetectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "prism-sed-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    // ─── No intersection ─────────────────────────────────────────────────────

    [Fact]
    public void Detect_ProductCentred_FullyInFrame()
    {
        // Dark rect inset 80 px on every side — well clear of the 32 px strip.
        string path = CreateJpeg("centred", img =>
        {
            FillAll(img, White);
            Fill(img, 80, 80, 240, 240, Dark);
        });

        EdgeIntersectionResult r = SubjectEdgeDetector.Detect(path);

        Assert.Equal(0,     r.IntersectionCount);
        Assert.True(        r.FullyInFrame);
        Assert.False(       r.IntersectsTop);
        Assert.False(       r.IntersectsBottom);
        Assert.False(       r.IntersectsLeft);
        Assert.False(       r.IntersectsRight);
    }

    // ─── Single edge intersections ────────────────────────────────────────────

    [Fact]
    public void Detect_DarkRectTouchesTopEdge_OnlyTopFlagged()
    {
        // Rect is inset 50 px on each side so it clears the 32 px left/right strips,
        // but starts at y=0 so it contacts the 32 px top strip.
        // Corners remain white for background sampling.
        string path = CreateJpeg("top", img =>
        {
            FillAll(img, White);
            Fill(img, 50, 0, W - 100, 200, Dark);
        });

        EdgeIntersectionResult r = SubjectEdgeDetector.Detect(path);

        Assert.Equal(1, r.IntersectionCount);
        Assert.True(    r.IntersectsTop);
        Assert.False(   r.IntersectsBottom);
        Assert.False(   r.IntersectsLeft);
        Assert.False(   r.IntersectsRight);
    }

    [Fact]
    public void Detect_DarkRectTouchesBottomEdge_OnlyBottomFlagged()
    {
        string path = CreateJpeg("bottom", img =>
        {
            FillAll(img, White);
            Fill(img, 80, 80, 240, H - 80, Dark);   // reaches bottom
        });

        EdgeIntersectionResult r = SubjectEdgeDetector.Detect(path);

        Assert.Equal(1, r.IntersectionCount);
        Assert.True(    r.IntersectsBottom);
        Assert.False(   r.IntersectsTop);
        Assert.False(   r.IntersectsLeft);
        Assert.False(   r.IntersectsRight);
    }

    [Fact]
    public void Detect_DarkRectTouchesLeftEdge_OnlyLeftFlagged()
    {
        string path = CreateJpeg("left", img =>
        {
            FillAll(img, White);
            Fill(img, 0, 80, 200, 240, Dark);   // reaches left edge
            RestoreCorners(img);
        });

        EdgeIntersectionResult r = SubjectEdgeDetector.Detect(path);

        Assert.Equal(1, r.IntersectionCount);
        Assert.True(    r.IntersectsLeft);
        Assert.False(   r.IntersectsTop);
        Assert.False(   r.IntersectsBottom);
        Assert.False(   r.IntersectsRight);
    }

    [Fact]
    public void Detect_DarkRectTouchesRightEdge_OnlyRightFlagged()
    {
        string path = CreateJpeg("right", img =>
        {
            FillAll(img, White);
            Fill(img, W - 200, 80, 200, 240, Dark);   // reaches right edge
            RestoreCorners(img);
        });

        EdgeIntersectionResult r = SubjectEdgeDetector.Detect(path);

        Assert.Equal(1, r.IntersectionCount);
        Assert.True(    r.IntersectsRight);
        Assert.False(   r.IntersectsTop);
        Assert.False(   r.IntersectsBottom);
        Assert.False(   r.IntersectsLeft);
    }

    // ─── All four edges ───────────────────────────────────────────────────────

    [Fact]
    public void Detect_SubjectFillsAllEdges_Count4()
    {
        // Dark everywhere; restore only corner zones so background is detected as white.
        string path = CreateJpeg("all4", img =>
        {
            FillAll(img, Dark);
            RestoreCorners(img);
        });

        EdgeIntersectionResult r = SubjectEdgeDetector.Detect(path);

        Assert.Equal(4, r.IntersectionCount);
        Assert.False(   r.FullyInFrame);
        Assert.True(    r.IntersectsTop);
        Assert.True(    r.IntersectsBottom);
        Assert.True(    r.IntersectsLeft);
        Assert.True(    r.IntersectsRight);
    }

    // ─── Run-length noise filter ──────────────────────────────────────────────

    [Fact]
    public void Detect_SingleIsolatedPixelAtEachBorder_NotFlagged()
    {
        // One dark pixel on each edge — run of 1, below MinRunLength (3) → not counted.
        string path = CreateJpeg("isolated", img =>
        {
            FillAll(img, White);
            img[W / 2, 0]      = Dark;   // top edge centre
            img[W / 2, H - 1]  = Dark;   // bottom edge centre
            img[0,     H / 2]  = Dark;   // left edge centre
            img[W - 1, H / 2]  = Dark;   // right edge centre
        });

        EdgeIntersectionResult r = SubjectEdgeDetector.Detect(path);

        Assert.Equal(0, r.IntersectionCount);
        Assert.True(    r.FullyInFrame);
    }

    [Fact]
    public void Detect_TwoPixelHorizontalRunAtTopEdge_NotFlagged()
    {
        // Run of exactly 2 consecutive dark pixels — below MinRunLength (3), not counted.
        string path = CreateJpeg("run2", img =>
        {
            FillAll(img, White);
            img[W / 2,     0] = Dark;
            img[W / 2 + 1, 0] = Dark;
        });

        EdgeIntersectionResult r = SubjectEdgeDetector.Detect(path);

        Assert.False(r.IntersectsTop);
        Assert.Equal(0, r.IntersectionCount);
    }

    [Fact]
    public void Detect_CheckerboardPatternInTopStrip_NotFlagged()
    {
        // Alternating white-dark columns in the top strip (middle section only, clear of the
        // 40 px corner zones so background sampling always sees pure white).
        // Every dark pixel is isolated — run of 1, below MinRunLength (3).
        // Despite ~40 % raw dark coverage the run-length filter keeps fgRunPixels at 0.
        // Uses PNG so lossless encoding preserves single-pixel runs.
        string path = CreatePng("checker", img =>
        {
            FillAll(img, White);
            for (int y = 0; y < StripPx; y++)
                for (int x = CornerPx; x < W - CornerPx; x += 2)
                    img[x, y] = Dark;   // skip x=0-39 and x=360-399 to keep corners white
        });

        EdgeIntersectionResult r = SubjectEdgeDetector.Detect(path);

        Assert.False(r.IntersectsTop);
    }

    // ─── Determinism ─────────────────────────────────────────────────────────

    [Fact]
    public void Detect_SameFileTwice_ReturnsIdenticalResult()
    {
        string path = CreateJpeg("determ", img =>
        {
            FillAll(img, White);
            Fill(img, 0, 0, W, 200, Dark);
            RestoreCorners(img);
        });

        EdgeIntersectionResult r1 = SubjectEdgeDetector.Detect(path);
        EdgeIntersectionResult r2 = SubjectEdgeDetector.Detect(path);

        Assert.Equal(r1, r2);
    }

    [Fact]
    public void Detect_InMemoryOverload_MatchesFileOverload()
    {
        // The Image<Rgba32> overload (used by ImageFeatureAnalyzer) must produce
        // the same result as the file-path overload.
        string path = CreateJpeg("overload", img =>
        {
            FillAll(img, White);
            Fill(img, 80, 80, 240, H - 80, Dark);   // bottom intersection only
        });

        EdgeIntersectionResult fromFile = SubjectEdgeDetector.Detect(path);

        using var img = Image.Load<Rgba32>(path);
        EdgeIntersectionResult fromImage = SubjectEdgeDetector.Detect(img);

        Assert.Equal(fromFile, fromImage);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private string CreateJpeg(string name, Action<Image<Rgba32>> paint)
    {
        string path = Path.Combine(_tempDir, $"{name}.jpg");
        using var img = new Image<Rgba32>(W, H);
        paint(img);
        img.SaveAsJpeg(path);
        return path;
    }

    private string CreatePng(string name, Action<Image<Rgba32>> paint)
    {
        string path = Path.Combine(_tempDir, $"{name}.png");
        using var img = new Image<Rgba32>(W, H);
        paint(img);
        img.SaveAsPng(path);
        return path;
    }

    private static void FillAll(Image<Rgba32> img, Rgba32 color)
        => Fill(img, 0, 0, img.Width, img.Height, color);

    private static void Fill(Image<Rgba32> img, int x, int y, int width, int height, Rgba32 color)
    {
        int endX = Math.Min(x + width,  img.Width);
        int endY = Math.Min(y + height, img.Height);
        for (int py = y; py < endY; py++)
            for (int px = x; px < endX; px++)
                img[px, py] = color;
    }

    /// <summary>
    /// Repaints the four 40×40 corner zones to white so background sampling always
    /// sees a clean white background, even when dark content extends into those zones.
    /// </summary>
    private static void RestoreCorners(Image<Rgba32> img)
    {
        Fill(img, 0,             0,              CornerPx, CornerPx, White); // TL
        Fill(img, img.Width  - CornerPx, 0,     CornerPx, CornerPx, White); // TR
        Fill(img, 0,             img.Height - CornerPx, CornerPx, CornerPx, White); // BL
        Fill(img, img.Width  - CornerPx, img.Height - CornerPx, CornerPx, CornerPx, White); // BR
    }
}
