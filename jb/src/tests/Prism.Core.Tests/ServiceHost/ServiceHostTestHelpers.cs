using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace PrismCoreTests.ServiceHost;

/// <summary>
/// Test helpers for creating minimal real images and test data for service host roundtrip tests.
/// </summary>
internal static class ServiceHostTestHelpers
{
    /// <summary>
    /// Generates a minimal valid JPEG image (small size to keep test fast) and writes it to disk.
    /// Returns the path to the written file.
    /// </summary>
    public static string CreateTestJpeg(string outputPath, int width = 100, int height = 100)
    {
        using Image<Rgb24> image = new(width, height);

        // Fill with a simple gradient to make it non-trivial.
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte r = (byte)(255 * x / width);
                byte g = (byte)(255 * y / height);
                byte b = 128;
                image[x, y] = new Rgb24(r, g, b);
            }
        }

        image.Save(outputPath, new JpegEncoder { Quality = 80 });
        return outputPath;
    }

    /// <summary>
    /// Generates a minimal JPEG image as a byte array (in-memory).
    /// </summary>
    public static byte[] CreateTestJpegBytes(int width = 100, int height = 100)
    {
        using Image<Rgb24> image = new(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte r = (byte)(255 * x / width);
                byte g = (byte)(255 * y / height);
                byte b = 128;
                image[x, y] = new Rgb24(r, g, b);
            }
        }

        using MemoryStream ms = new();
        image.Save(ms, new JpegEncoder { Quality = 80 });
        return ms.ToArray();
    }

    /// <summary>
    /// Creates a minimal valid IngestResult for testing.
    /// If jobTempFolder is not provided, creates a temporary directory.
    /// If createNormalizedJpeg is true, creates and references a real JPEG file.
    /// </summary>
    public static IngestResult CreateMinimalIngestResult(
        string? jobTempFolder = null,
        bool createNormalizedJpeg = true)
    {
        jobTempFolder ??= Path.Combine(Path.GetTempPath(), $"prism-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(jobTempFolder);

        string normalizedPath = "";
        if (createNormalizedJpeg)
        {
            string normalizedDir = Path.Combine(jobTempFolder, "normalized");
            Directory.CreateDirectory(normalizedDir);
            normalizedPath = Path.Combine(normalizedDir, "000000_test.jpg");
            CreateTestJpeg(normalizedPath, 200, 200);
        }

        return new IngestResult
        {
            JobID = Guid.NewGuid(),
            Parameters = new PrismProcessingParameters(),
            NormalizedImages =
            [
                new ImageRecord_INPUT
                {
                    InitialFullName = "test.jpg",
                    ImportStatus = ImportStatus.Ok,
                    NormalizedJpgPath = normalizedPath,
                    NormalizedWidth = 200,
                    NormalizedHeight = 200
                }
            ],
            FamilyRecords = [],
            JobTempFolder = jobTempFolder
        };
    }

    /// <summary>
    /// Creates a minimal MatchingResult with a single OK image. Used for downstream services
    /// (Generate, Transform) that accept MatchingResult as input.
    /// </summary>
    public static MatchingResult CreateMinimalMatchingResult(IngestResult? ingestBase = null)
    {
        ingestBase ??= CreateMinimalIngestResult(createNormalizedJpeg: false);

        return new MatchingResult
        {
            Ingest = ingestBase,
            LambdaRecords =
            [
                new ImageRecord_LAMBDA
                {
                    InitialFullName = "test.jpg",
                    ImportStatus = ImportStatus.Ok,
                    Width = 200,
                    Height = 200,
                    Family = "TEST-001",
                    DetOrder = 0,
                    SelectedPhenotype = "front-packshot"
                }
            ]
        };
    }
}
