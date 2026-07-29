using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace PrismCoreTests.ServiceHost;

/// <summary>
/// Proves the PRISM_UPSCALE_URL seam end-to-end: when TransformService's preprocessor decides an image
/// needs upscaling and a remote IUpscaleService is routed in, the bytes go over real HTTP to the Upscale
/// host instead of the local static session. Image sizing is derived from live config so the upscale
/// branch is forced deterministically (salient bbox below MinOutputWidth, within MaxUpScaleFactor).
/// </summary>
[Collection("Service Host")]
public class RemoteUpscaleRoutingTests {
    private readonly ServiceHostFixture fixture;

    public RemoteUpscaleRoutingTests(ServiceHostFixture fixture) {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Preprocess_BelowMinimumImage_RoutesUpscaleThroughRemoteHost() {
        PrismConfiguration config = PrismConfiguration.LoadPrismConfig(
            ConfigLoader.RequireFile(PrismConfiguration.FileName));

        double margin = TransformParameters.FromConfig().Crop.WhiteSpaceMargin;

        // Salient square sized 20% above the smallest upscalable bbox, image 25% larger than the square
        // so saliency detection finds the square, not the frame.
        int squareSide = (int)Math.Ceiling(config.MinOutputWidth / config.MaxUpScaleFactor * 1.2);
        int imageSide = (int)(squareSide * 1.25);
        Assert.True(squareSide >= config.MinInputSizeInPixels,
            $"Config makes the forced-upscale window empty: square {squareSide}px < MinInputSizeInPixels {config.MinInputSizeInPixels}px.");
        Assert.True(FinalOutputSize.CenterAndStretchCanvasSize(squareSide, margin) < config.MinOutputWidth,
            $"Config makes the forced-upscale window empty: a {squareSide}px bbox already yields a "
            + $"{FinalOutputSize.CenterAndStretchCanvasSize(squareSide, margin)}px canvas, at or above MinOutputWidth {config.MinOutputWidth}px.");

        string tempDir = Path.Combine(Path.GetTempPath(), $"prism-upscale-route-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string imagePath = Path.Combine(tempDir, "square.jpg");
        WriteSquareJpeg(imagePath, imageSide, squareSide);

        var counting = new CountingUpscaleService(new HttpUpscaleService(fixture.Client));
        var lambda = new ImageRecord_LAMBDA { InitialFullName = "square.jpg", ImportStatus = ImportStatus.Ok };

        // allowEsrganUpscale: true — the remote host is the ESRGAN host, so the Lanczos-only default
        // would never reach it. No seed: the shadow-accounting toggle is irrelevant to routing here.
        (byte[]? processed, OpenCvSharp.Mat? colorMat) = await ImagePreProcessor.PreprocessAsync(
            lambda, imagePath, config, TransformParameters.FromConfig(), null, true, counting);
        colorMat?.Dispose();

        Assert.False(lambda.IsKo, $"Image KO'd instead of upscaling: {lambda.KoReasonCode} {lambda.KoSafeMessage}");
        Assert.Equal(1, counting.Calls);
        Assert.NotNull(processed);
        using Image upscaled = Image.Load(processed);
        Assert.True(upscaled.Width > imageSide, $"Upscaled width {upscaled.Width} not larger than input {imageSide}.");

        // The unified bar (T-4920): geometry travels with the pixels, so the box left on the record must
        // now yield a final canvas at or above MinOutputWidth — the point of upscaling in the first place.
        int finalSize = FinalOutputSize.LongestDimension(lambda, upscaled.Width, upscaled.Height, margin);
        Assert.True(finalSize >= config.MinOutputWidth,
            $"Upscale left the final output at {finalSize}px, below the {config.MinOutputWidth}px bar it targeted.");
    }

    private static void WriteSquareJpeg(string path, int imageSide, int squareSide) {
        using Image<Rgb24> image = new(imageSide, imageSide, new Rgb24(255, 255, 255));
        int offset = (imageSide - squareSide) / 2;
        for (int y = offset; y < offset + squareSide; y++)
            for (int x = offset; x < offset + squareSide; x++)
                image[x, y] = new Rgb24(10, 10, 10);
        image.Save(path, new JpegEncoder { Quality = 90 });
    }
}
