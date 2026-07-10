using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace PrismCoreTests.Ingest;

/// <summary>
/// Tests for <see cref="Importer"/>'s direct image records — the path every multipart upload,
/// stream, and local file takes. Multipart and stream inputs reach the Importer as files the API
/// spilled to disk (<see cref="ImageRecord_INPUT.TempFilePath"/> set); local-path inputs carry the
/// path in <see cref="ImageRecord_INPUT.InitialFullName"/>. Covers success normalization (JPEG
/// fast path, PNG alpha flattening, EXIF auto-orientation) and every KO branch (missing file,
/// unsupported extension, byte-size limits, pixel minimum, corrupt bytes), plus the
/// KO-never-stops-the-batch and deterministic-ordering contracts.
/// </summary>
public class ImporterDirectImageTests : IClassFixture<ImporterFixture> {
    private readonly ImporterFixture fixture;

    public ImporterDirectImageTests(ImporterFixture fixture) {
        this.fixture = fixture;
    }

    //  Success paths

    [Theory]
    [InlineData(ImageSourceKind.MultipartUpload)]
    [InlineData(ImageSourceKind.Stream)]
    public void SpilledTempFile_ValidJpeg_IsNormalizedAndKeepsSourceKind(ImageSourceKind sourceKind) {
        string spilled = fixture.WriteNoiseJpeg($"spilled_{sourceKind}.jpg", 600, 600);

        ImportStageResult result = fixture.RunImport([new ImageRecord_INPUT {
            InitialFullName = $"upload_{sourceKind}.jpg",
            TempFilePath    = spilled,
            SourceKind      = sourceKind
        }]);

        ImageRecord_INPUT normalized = Assert.Single(result.NormalizedImages);
        Assert.Equal(ImportStatus.Ok, normalized.ImportStatus);
        Assert.Equal(sourceKind, normalized.SourceKind);
        Assert.Equal(600, normalized.NormalizedWidth);
        Assert.Equal(600, normalized.NormalizedHeight);
        Assert.True(File.Exists(normalized.NormalizedJpgPath), "Normalized JPEG must exist on disk.");
        Assert.Empty(result.ImageKoRecords);
    }

    [Fact]
    public void LocalPath_ValidJpeg_IsNormalizedAsLocalPathKind() {
        string localPath = fixture.WriteNoiseJpeg("local_direct.jpg", 600, 600);

        ImportStageResult result = fixture.RunImport([new ImageRecord_INPUT { InitialFullName = localPath }]);

        ImageRecord_INPUT normalized = Assert.Single(result.NormalizedImages);
        Assert.Equal(ImportStatus.Ok, normalized.ImportStatus);
        Assert.Equal(ImageSourceKind.LocalPath, normalized.SourceKind);
    }

    [Fact]
    public void TransparentPng_IsFlattenedToWhiteJpeg() {
        string pngPath = fixture.WriteHalfTransparentPng("half_transparent.png", 600, 600);

        ImportStageResult result = fixture.RunImport([new ImageRecord_INPUT { InitialFullName = pngPath }]);

        ImageRecord_INPUT normalized = Assert.Single(result.NormalizedImages);
        using Image<Rgb24> jpeg = Image.Load<Rgb24>(normalized.NormalizedJpgPath!);

        // The right half of the source was fully transparent; after compositing it must be white.
        // Sample away from the opaque/transparent seam so JPEG chroma bleed cannot flip the assert.
        Rgb24 flattened = jpeg[jpeg.Width - 20, jpeg.Height / 2];
        Assert.True(flattened.R >= 240 && flattened.G >= 240 && flattened.B >= 240,
            $"Transparent pixels must flatten to white, got ({flattened.R},{flattened.G},{flattened.B}).");
    }

    [Fact]
    public void ExifRotatedJpeg_IsAutoOrientedNotFastPathCopied() {
        // Orientation 6 (rotate 90° CW) disqualifies the conforming-JPEG fast path, so the full
        // decode applies AutoOrient and the normalized axes come out swapped: 600x400 -> 400x600.
        string rotated = fixture.WriteExifRotatedJpeg("exif_rotated.jpg", 600, 400);

        ImportStageResult result = fixture.RunImport([new ImageRecord_INPUT { InitialFullName = rotated }]);

        ImageRecord_INPUT normalized = Assert.Single(result.NormalizedImages);
        Assert.Equal(400, normalized.NormalizedWidth);
        Assert.Equal(600, normalized.NormalizedHeight);
    }

    //  KO branches

    [Fact]
    public void MissingFile_KoCorruptWithoutThrowing() {
        ImportStageResult result = fixture.RunImport([new ImageRecord_INPUT {
            InitialFullName = Path.Combine(fixture.TempRoot, "ghost.jpg")
        }]);

        ImportKoRecord ko = Assert.Single(result.ImageKoRecords);
        Assert.Equal(ImportKoRecord.CorruptImageReason, ko.ReasonCode);
        Assert.Empty(result.NormalizedImages);
    }

    [Fact]
    public void UnsupportedExtension_KoUnsupportedFormat() {
        string txtPath = fixture.WriteBytes("notes.txt", "not an image"u8.ToArray());

        ImportStageResult result = fixture.RunImport([new ImageRecord_INPUT { InitialFullName = txtPath }]);

        ImportKoRecord ko = Assert.Single(result.ImageKoRecords);
        Assert.Equal(ImportKoRecord.UnsupportedFormatReason, ko.ReasonCode);
    }

    [Fact]
    public void BelowMinimumBytes_KoFileTooSmall() {
        string valid = fixture.WriteNoiseJpeg("too_small_bytes.jpg", 600, 600);

        ImportStageResult result = fixture.RunImport([new ImageRecord_INPUT {
            InitialFullName = valid,
            ByteLength      = fixture.Configuration.MinBytesPerImg - 1
        }]);

        ImportKoRecord ko = Assert.Single(result.ImageKoRecords);
        Assert.Equal(ImportKoRecord.FileTooSmallReason, ko.ReasonCode);
    }

    [Fact]
    public void AboveMaximumBytes_KoFileTooLarge() {
        string valid = fixture.WriteNoiseJpeg("too_large_bytes.jpg", 600, 600);

        ImportStageResult result = fixture.RunImport([new ImageRecord_INPUT {
            InitialFullName = valid,
            ByteLength      = fixture.Configuration.MaxBytesPerImg + 1
        }]);

        ImportKoRecord ko = Assert.Single(result.ImageKoRecords);
        Assert.Equal(ImportKoRecord.FileTooLargeReason, ko.ReasonCode);
    }

    [Fact]
    public void BelowMinimumPixels_KoImageTooSmall() {
        // 200px on the longest side is under Input.Images.MINIMUM_SIZE_IN_PIXELS; the noise fill
        // keeps the encoded file above the byte minimum so only the pixel gate can fire.
        string tiny = fixture.WriteNoiseJpeg("tiny_pixels.jpg", 200, 200);

        ImportStageResult result = fixture.RunImport([new ImageRecord_INPUT { InitialFullName = tiny }]);

        ImportKoRecord ko = Assert.Single(result.ImageKoRecords);
        Assert.Equal(ImportKoRecord.ImageTooSmallReason, ko.ReasonCode);
    }

    [Fact]
    public void CorruptJpegBytes_KoCorrupt() {
        byte[] garbage = new byte[4096];
        new Random(7).NextBytes(garbage);
        string corrupt = fixture.WriteBytes("corrupt.jpg", garbage);

        ImportStageResult result = fixture.RunImport([new ImageRecord_INPUT { InitialFullName = corrupt }]);

        ImportKoRecord ko = Assert.Single(result.ImageKoRecords);
        Assert.Equal(ImportKoRecord.CorruptImageReason, ko.ReasonCode);
        Assert.True(ko.BatchContinues);
    }

    //  Batch contracts

    [Fact]
    public void MixedBatch_KoDoesNotStopTheBatch() {
        string good = fixture.WriteNoiseJpeg("mixed_good.jpg", 600, 600);
        byte[] garbage = new byte[4096];
        new Random(11).NextBytes(garbage);
        string bad = fixture.WriteBytes("mixed_bad.jpg", garbage);

        ImportStageResult result = fixture.RunImport([
            new ImageRecord_INPUT { InitialFullName = good },
            new ImageRecord_INPUT { InitialFullName = bad }
        ]);

        Assert.Single(result.NormalizedImages);
        Assert.Single(result.ImageKoRecords);
    }

    [Fact]
    public void NormalizedImages_AreSortedDeterministicallyByOriginalName() {
        string source = fixture.WriteNoiseJpeg("sort_source.jpg", 600, 600);

        // Same bytes under three names, fed out of order — output must come back a, b, c (T-2820).
        ImportStageResult result = fixture.RunImport([
            new ImageRecord_INPUT { InitialFullName = "c_sort.jpg", TempFilePath = source },
            new ImageRecord_INPUT { InitialFullName = "a_sort.jpg", TempFilePath = source },
            new ImageRecord_INPUT { InitialFullName = "b_sort.jpg", TempFilePath = source }
        ]);

        Assert.Equal(["a_sort.jpg", "b_sort.jpg", "c_sort.jpg"],
            result.NormalizedImages.Select(r => r.InitialFullName).ToList());
    }
}
