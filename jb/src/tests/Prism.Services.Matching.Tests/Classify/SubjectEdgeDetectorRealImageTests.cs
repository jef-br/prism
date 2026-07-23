using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace PrismCoreTests.Classify;

/// <summary>
/// Integration tests for <see cref="SubjectEdgeDetector"/> against the real camera JPEGs committed in
/// test/datasets/CiMini. The fixture is committed, so these tests always run — including on CI. They must
/// never skip: a missing fixture is a real failure, not a reason to pass green.
///
/// The CiMini JPEGs carry an embedded EXIF (IFD1) thumbnail, so these exercise the fast path where the main
/// image is never decoded.
/// </summary>
public sealed class SubjectEdgeDetectorRealImageTests
{
    // Representative CiMini images: product shots with full background and clean edges.
    private static readonly string[] FixtureImageNames =
    [
        "2021_3024_46_A.jpg",
        "2021_3024_46_B.jpg",
        "23211008_02_A.jpg",
        "23211008_02_B.jpg",
    ];

    //  EXIF fast-path

    [Fact]
    public void ExifFastPath_RealCameraJpeg_ExtractsThumbnail()
    {
        string path = FixtureImage(FixtureImageNames[0]);

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
        foreach (string name in FixtureImageNames)
        {
            string path = FixtureImage(name);

            byte[]? thumb = SubjectEdgeDetector.TryExtractJpegExifThumbnail(path);
            Assert.True(thumb is not null, $"No EXIF thumbnail extracted from {name}.");

            using var ms = new MemoryStream(thumb);
            using var img = Image.Load<Rgba32>(ms);
            Assert.True(img.Width > 0 && img.Height > 0, $"Thumbnail from {name} decoded to zero dimensions.");
        }
    }

    //  Result validity

    [Fact]
    public void Detect_RealCameraJpeg_ReturnsValidResult()
    {
        string path = FixtureImage(FixtureImageNames[0]);

        SubjectEdgeDetectionResult r = SubjectEdgeDetector.Detect(path);

        Assert.InRange(r.IntersectionCount, 0, 4);
        Assert.Equal(r.FullyInFrame, r.IntersectionCount == 0);
    }

    //  Determinism on real images

    [Fact]
    public void Detect_RealCameraJpeg_CalledTwice_SameResult()
    {
        string path = FixtureImage(FixtureImageNames[0]);

        SubjectEdgeDetectionResult r1 = SubjectEdgeDetector.Detect(path);
        SubjectEdgeDetectionResult r2 = SubjectEdgeDetector.Detect(path);

        Assert.Equal(r1, r2);
    }

    //  Helpers

    /// <summary>
    /// Resolves a committed CiMini image. Fails the test when absent — CiMini is in git, so a missing file
    /// means a broken checkout or a renamed fixture, both of which must be loud.
    /// </summary>
    private static string FixtureImage( string fileName )
    {
        string path = Path.Combine(PipelineFixture.ResolveTestFixturePath(), "CiMini", fileName);
        Assert.True(File.Exists(path), $"Committed CiMini fixture image not found: {path}");
        return path;
    }
}
