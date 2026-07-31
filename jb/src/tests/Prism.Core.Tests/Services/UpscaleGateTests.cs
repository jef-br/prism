using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using CvMat = OpenCvSharp.Mat;

namespace PrismCoreTests.Services;

/// <summary>
/// T-4920: the unified upscale gate. Both toggle states aim at the same bar — the FINAL output image
/// reaching MinOutputWidth — and differ only in resampler and cap: ESRGAN to MaxUpScaleFactor when the
/// job opted in, plain Lanczos to MaxLanczosOnlyUpScaleFactor by default, KO past either.
/// <para>
/// Geometry is pinned by putting an exact <see cref="SubjectDetectionResult"/> on the record rather than by
/// crafting pixels the salient detector has to rediscover: promotion overwrites the detected box before
/// the upscale decision runs, so the box under test is exact and the source image can stay blank.
/// </para>
/// </summary>
public class UpscaleGateTests : IDisposable {
    private readonly string tempDir = Path.Combine(Path.GetTempPath(), $"prism-upscale-gate-{Guid.NewGuid():N}");
    private readonly PrismConfiguration config = PrismConfiguration.LoadPrismConfig(
        ConfigLoader.RequireFile(PrismConfiguration.FileName));
    private readonly TransformParameters parameters = TransformParameters.FromConfig();

    public UpscaleGateTests() => Directory.CreateDirectory(this.tempDir);

    public void Dispose() {
        try { Directory.Delete(this.tempDir, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task BboxAlreadyClearsTheBar_NoUpscaleAtAll() {
        // 900px bbox is well past the 740px a 800px canvas needs, so neither resampler should run.
        ImageRecord_LAMBDA lambda = Lambda(BoxOf(50, 50, 900, 900), intersects: false);
        RecordingUpscaleService recorder = new();

        (byte[]? processed, CvMat? mat) = await Preprocess(lambda, 1200, 1200, allowEsrgan: true, recorder);
        mat?.Dispose();

        Assert.False(lambda.IsKo);
        Assert.Equal(0, recorder.Calls);
        Assert.Equal(1200, WidthOf(processed));
        Assert.Equal(900, lambda.BoundingBox!.Value.Width);
    }

    [Fact]
    public async Task ToggleOff_UsesLanczosLocally_AndReachesTheBar() {
        ImageRecord_LAMBDA lambda = Lambda(BoxOf(140, 140, 620, 620), intersects: false);
        RecordingUpscaleService recorder = new();

        (byte[]? processed, CvMat? mat) = await Preprocess(lambda, 900, 900, allowEsrgan: false, recorder);
        mat?.Dispose();

        Assert.False(lambda.IsKo, $"{lambda.KoReasonCode}: {lambda.KoSafeMessage}");
        Assert.Equal(0, recorder.Calls);   // the ESRGAN service must not be reached on the default path
        Assert.True(WidthOf(processed) > 900, "Lanczos path did not enlarge the image.");
        AssertFinalSizeReachesBar(lambda, processed);
    }

    [Fact]
    public async Task ToggleOn_RoutesToTheEsrganService() {
        ImageRecord_LAMBDA lambda = Lambda(BoxOf(140, 140, 620, 620), intersects: false);
        RecordingUpscaleService recorder = new();

        (byte[]? processed, CvMat? mat) = await Preprocess(lambda, 900, 900, allowEsrgan: true, recorder);
        mat?.Dispose();

        Assert.False(lambda.IsKo, $"{lambda.KoReasonCode}: {lambda.KoSafeMessage}");
        Assert.Equal(1, recorder.Calls);
        AssertFinalSizeReachesBar(lambda, processed);
    }

    // The Lanczos-only cap is only ever reachable on the bleed route. On the centre-and-stretch route a
    // bbox at the MinInputSizeInPixels floor (570) needs 740/570 = 1.30x, which is already inside the
    // 1.33x cap — so no zero-intersection image can be KO'd by it at the current config values.
    [Fact]
    public async Task ToggleOff_BleedImageNeedingMoreThanTheLanczosCap_IsKoWithTheToggleNamed() {
        ImageRecord_LAMBDA lambda = Lambda(BoxOf(0, 40, 700, 500), intersects: true);
        RecordingUpscaleService recorder = new();

        // Shorter side 590 needs 800/590 = 1.36x: past the 1.33x Lanczos cap, inside the 1.42x ESRGAN cap.
        (byte[]? processed, CvMat? mat) = await Preprocess(lambda, 900, 590, allowEsrgan: false, recorder);
        mat?.Dispose();

        Assert.True(lambda.IsKo);
        Assert.Equal("PREPROCESS_UPSCALE_EXCEEDED", lambda.KoReasonCode);
        Assert.Contains("Enable ESRGAN upscaling", lambda.KoSafeMessage);
        Assert.Null(processed);
        Assert.Equal(0, recorder.Calls);
    }

    [Fact]
    public async Task ToggleOn_SameBleedImage_IsWithinTheEsrganCapAndProcesses() {
        ImageRecord_LAMBDA lambda = Lambda(BoxOf(0, 40, 700, 500), intersects: true);
        RecordingUpscaleService recorder = new();

        (byte[]? processed, CvMat? mat) = await Preprocess(lambda, 900, 590, allowEsrgan: true, recorder);
        mat?.Dispose();

        Assert.False(lambda.IsKo, $"{lambda.KoReasonCode}: {lambda.KoSafeMessage}");
        Assert.Equal(1, recorder.Calls);
        AssertFinalSizeReachesBar(lambda, processed);
    }

    [Fact]
    public async Task ToggleOn_PastTheEsrganCap_IsKoWithoutNamingTheToggle() {
        ImageRecord_LAMBDA lambda = Lambda(BoxOf(0, 40, 700, 480), intersects: true);
        RecordingUpscaleService recorder = new();

        // Shorter side 520 needs 800/520 = 1.54x — past both caps.
        (byte[]? processed, CvMat? mat) = await Preprocess(lambda, 900, 520, allowEsrgan: true, recorder);
        mat?.Dispose();

        Assert.True(lambda.IsKo);
        Assert.Equal("PREPROCESS_UPSCALE_EXCEEDED", lambda.KoReasonCode);
        Assert.DoesNotContain("Enable ESRGAN upscaling", lambda.KoSafeMessage);
        Assert.Null(processed);
    }

    // The pre-existing too-small KO survives the rewrite, and it now measures the promoted box.
    [Fact]
    public async Task SubjectBelowMinimumInputSize_KeepsTheTooSmallKo() {
        ImageRecord_LAMBDA lambda = Lambda(BoxOf(100, 100, 400, 400), intersects: false);

        (byte[]? processed, CvMat? mat) = await Preprocess(lambda, 900, 900, allowEsrgan: true, new RecordingUpscaleService());
        mat?.Dispose();

        Assert.True(lambda.IsKo);
        Assert.Equal("PREPROCESS_TOO_SMALL", lambda.KoReasonCode);
        Assert.Null(processed);
    }

    // The defect T-4900 was built on: enlarging the bytes without enlarging the geometry left Transform
    // cropping original-resolution coordinates out of a bigger image.
    [Fact]
    public async Task Upscale_MovesTheBoundingBoxIntoTheEnlargedCoordinateSpace() {
        ImageRecord_LAMBDA lambda = Lambda(BoxOf(140, 140, 620, 620), intersects: false);

        (byte[]? processed, CvMat? mat) = await Preprocess(lambda, 900, 900, allowEsrgan: false, new RecordingUpscaleService());
        mat?.Dispose();

        BoundingBox box = lambda.BoundingBox!.Value;
        int enlargedWidth = WidthOf(processed);
        Assert.True(box.Width > 620, $"Bounding box stayed at {box.Width}px while the image grew to {enlargedWidth}px.");
        // The box grew by the same factor as the pixels — measured against the image that came back,
        // not against the scale the code computed, so a wrong scale cannot satisfy both sides.
        Assert.Equal((int)Math.Round(620.0 * enlargedWidth / 900), box.Width);
        Assert.True(box.Right <= enlargedWidth, $"Bounding box right edge {box.Right} runs past the {enlargedWidth}px image.");
    }

    //  Helpers

    private Task<(byte[]? bytes, CvMat? colorMat)> Preprocess(
        ImageRecord_LAMBDA lambda, int imageWidth, int imageHeight, bool allowEsrgan, IUpscaleService remoteUpscale) {
        string path = Path.Combine(this.tempDir, $"{Guid.NewGuid():N}.jpg");
        WriteBlankJpeg(path, imageWidth, imageHeight);
        return ImagePreProcessor.PreprocessAsync(
            lambda, path, this.config, this.parameters, null, allowEsrgan, remoteUpscale, CancellationToken.None);
    }

    private void AssertFinalSizeReachesBar(ImageRecord_LAMBDA lambda, byte[]? processed) {
        using Image image = Image.Load(processed!);
        int finalSize = FinalOutputSize.LongestDimension(lambda, image.Width, image.Height, this.parameters.Crop.WhiteSpaceMargin);
        Assert.True(finalSize >= this.config.MinOutputWidth,
            $"Final output would be {finalSize}px, below the {this.config.MinOutputWidth}px bar the upscale targeted.");
    }

    private static int WidthOf(byte[]? jpeg) {
        using Image image = Image.Load(jpeg!);
        return image.Width;
    }

    private static void WriteBlankJpeg(string path, int width, int height) {
        using Image<Rgb24> image = new(width, height, new Rgb24(255, 255, 255));
        image.Save(path, new JpegEncoder { Quality = 90 });
    }

    private static BoundingBox BoxOf(int x, int y, int width, int height) => new() {
        X = x, Y = y, Width = width, Height = height, Left = x, Top = y, Right = x + width, Bottom = y + height
    };

    private static ImageRecord_LAMBDA Lambda(BoundingBox box, bool intersects) => new() {
        InitialFullName = "img.jpg",
        ImportStatus = ImportStatus.Ok,
        Subject = new SubjectDetectionResult {
            Producer = "classical-cv",
            IsWholeFrameFallback = false,
            Confidence = 0.9,
            IntersectsLeft = intersects,
            Box = box
        }
    };

    /// <summary>Stands in for the Real-ESRGAN host: counts calls and resizes without touching the GPU.</summary>
    private sealed class RecordingUpscaleService : IUpscaleService {
        private int calls;

        public int Calls => this.calls;

        public Task<byte[]> UpscaleAsync(byte[] imageBytes, double scaleFactor, CancellationToken cancellationToken) {
            Interlocked.Increment(ref this.calls);
            using CvMat src = Cv2.ImDecode(imageBytes, ImreadModes.Color);
            using CvMat dst = new();
            Cv2.Resize(src, dst, new OpenCvSharp.Size((int)Math.Round(src.Cols * scaleFactor), (int)Math.Round(src.Rows * scaleFactor)),
                interpolation: InterpolationFlags.Lanczos4);
            Cv2.ImEncode(".jpg", dst, out byte[] result);
            return Task.FromResult(result);
        }
    }
}
