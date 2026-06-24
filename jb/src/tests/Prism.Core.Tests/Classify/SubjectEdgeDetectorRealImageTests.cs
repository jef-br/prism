using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace PrismCoreTests.Classify;

/// <summary>
/// Integration tests for <see cref="SubjectEdgeDetector"/> against real camera JPEG files
/// from the jb/testing/ fixture directory. Tests are skipped gracefully when the fixture
/// directory is not present (e.g., CI without test assets).
/// </summary>
public sealed class SubjectEdgeDetectorRealImageTests
{
    // Representative fixture images: product shots with full background and clean edges.
    private static readonly string[] FixtureRelPaths =
    [
        @"jb\Testing\SPACINI29\RAW IMAGES\20213024_46_A.jpg",
        @"jb\Testing\SPACINI29\RAW IMAGES\20213024_46_B.jpg",
        @"jb\Testing\SPACINI29\RAW IMAGES\23211008_02_A.jpg",
        @"jb\Testing\SPACINI29\RAW IMAGES\23231096_35_A.jpg",
    ];

    //  EXIF fast-path 

    [Fact]
    public void ExifFastPath_RealCameraJpeg_ExtractsThumbnail()
    {
        string? path = FindFixture(FixtureRelPaths[0]);
        if (path is null)
        {
            // Fixture images not present in this environment — skip silently.
            return;
        }

        byte[]? thumb = SubjectEdgeDetector.TryExtractJpegExifThumbnail(path);

        Assert.NotNull(thumb);
        Assert.True(thumb.Length > 0, "Extracted thumbnail must not be empty.");

        // Verify the bytes are a valid JPEG (FF D8 header).
        Assert.Equal(0xFF, thumb[0]);
        Assert.Equal(0xD8, thumb[1]);
    }

    [Fact]
    public void ExifFastPath_AllFixtureImages_EachExtractsThumbnail()
    {
        string? root = FindRepoRoot();
        if (root is null) return;

        foreach (string rel in FixtureRelPaths)
        {
            string path = Path.Combine(root, rel);
            if (!File.Exists(path)) continue;

            byte[]? thumb = SubjectEdgeDetector.TryExtractJpegExifThumbnail(path);
            Assert.NotNull(thumb);

            using var ms = new MemoryStream(thumb);
            using var img = Image.Load<Rgba32>(ms);
            Assert.True(img.Width > 0 && img.Height > 0, $"Thumbnail from {rel} decoded to zero dimensions.");
        }
    }

    //  Result validity 

    [Fact]
    public void Detect_RealCameraJpeg_ReturnsValidResult()
    {
        string? path = FindFixture(FixtureRelPaths[0]);
        if (path is null) return;

        EdgeIntersectionResult r = SubjectEdgeDetector.Detect(path);

        Assert.InRange(r.IntersectionCount, 0, 4);
        Assert.Equal(r.FullyInFrame, r.IntersectionCount == 0);
    }

    //  Determinism on real images 

    [Fact]
    public void Detect_RealCameraJpeg_CalledTwice_SameResult()
    {
        string? path = FindFixture(FixtureRelPaths[0]);
        if (path is null) return;

        EdgeIntersectionResult r1 = SubjectEdgeDetector.Detect(path);
        EdgeIntersectionResult r2 = SubjectEdgeDetector.Detect(path);

        Assert.Equal(r1, r2);
    }

    //  Helpers 

    private static string? FindFixture(string repoRelativePath)
    {
        string? root = FindRepoRoot();
        if (root is null) return null;
        string full = Path.Combine(root, repoRelativePath);
        return File.Exists(full) ? full : null;
    }

    /// <summary>
    /// Walks up from the test binary output directory until it finds the repo root
    /// (identified by AGENT-TICKETS.md). Returns null when not found.
    /// </summary>
    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AGENT-TICKETS.md")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
