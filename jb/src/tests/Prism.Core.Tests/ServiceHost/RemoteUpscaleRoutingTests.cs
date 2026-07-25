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

        // Salient square sized 20% above the smallest upscalable bbox, image 25% larger than the square
        // so saliency detection finds the square, not the frame.
        int squareSide = (int)Math.Ceiling(config.MinOutputWidth / config.MaxUpScaleFactor * 1.2);
        int imageSide = (int)(squareSide * 1.25);
        Assert.True(squareSide >= config.MinInputSizeInPixels,
            $"Config makes the forced-upscale window empty: square {squareSide}px < MinInputSizeInPixels {config.MinInputSizeInPixels}px.");
        Assert.True(squareSide < config.MinOutputWidth,
            $"Config makes the forced-upscale window empty: square {squareSide}px >= MinOutputWidth {config.MinOutputWidth}px.");

        string tempDir = Path.Combine(Path.GetTempPath(), $"prism-upscale-route-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string imagePath = Path.Combine(tempDir, "square.jpg");
        WriteSquareJpeg(imagePath, imageSide, squareSide);

        var counting = new CountingUpscaleService(new HttpUpscaleService(fixture.Client));
        var lambda = new ImageRecord_LAMBDA { InitialFullName = "square.jpg", ImportStatus = ImportStatus.Ok };

        (byte[]? processed, var colorMat) = await ImagePreProcessor.PreprocessAsync(lambda, imagePath, config, counting);
        colorMat?.Dispose();

        Assert.False(lambda.IsKo, $"Image KO'd instead of upscaling: {lambda.KoReasonCode} {lambda.KoSafeMessage}");
        Assert.Equal(1, counting.Calls);
        Assert.NotNull(processed);
        using Image upscaled = Image.Load(processed);
        Assert.True(upscaled.Width > imageSide, $"Upscaled width {upscaled.Width} not larger than input {imageSide}.");
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
