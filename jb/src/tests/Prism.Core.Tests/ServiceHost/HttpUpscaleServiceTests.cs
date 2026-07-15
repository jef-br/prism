using SixLabors.ImageSharp;
using Xunit;

namespace PrismCoreTests.ServiceHost;

/// <summary>
/// Real-HTTP roundtrip tests for HttpUpscaleService against Prism.ServiceHost.
/// Exercises the HTTP client POSTing raw JPEG bytes and a scale factor to the remote upscale service.
/// </summary>
[Collection("Service Host")]
public class HttpUpscaleServiceTests
{
    private readonly ServiceHostFixture fixture;

    public HttpUpscaleServiceTests(ServiceHostFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task UpscaleAsync_WithValidJpegBytes_ReturnsUpscaledJpegBytes()
    {
        // Arrange
        var client = new HttpUpscaleService(fixture.Client);
        byte[] inputJpeg = ServiceHostTestHelpers.CreateTestJpegBytes(100, 100);
        double scaleFactor = 2.0;

        // Act
        byte[] upscaledBytes = await client.UpscaleAsync(inputJpeg, scaleFactor, CancellationToken.None);

        // Assert
        Assert.NotNull(upscaledBytes);
        Assert.NotEmpty(upscaledBytes);
        Assert.True(upscaledBytes.Length > 0, "Upscaled image should return non-empty bytes.");
    }

    [Fact]
    public async Task UpscaleAsync_UpscaledBytesDecodeAsValidImage()
    {
        // Arrange
        var client = new HttpUpscaleService(fixture.Client);
        byte[] inputJpeg = ServiceHostTestHelpers.CreateTestJpegBytes(100, 100);
        double scaleFactor = 2.0;

        // Act
        byte[] upscaledBytes = await client.UpscaleAsync(inputJpeg, scaleFactor, CancellationToken.None);

        // Assert
        // Attempt to load the upscaled bytes as an image; should succeed without throwing.
        using MemoryStream ms = new(upscaledBytes);
        Image decodedImage = await Image.LoadAsync(ms);
        Assert.NotNull(decodedImage);

        // Roughly verify the dimensions increased (may not be exactly 2x due to encoder/decoder variance,
        // but should be notably larger than the input).
        int inputArea = 100 * 100;
        int outputArea = decodedImage.Width * decodedImage.Height;
        Assert.True(outputArea > inputArea, $"Upscaled image area {outputArea} should exceed input area {inputArea}");
    }

    [Fact]
    public async Task UpscaleAsync_SmallScaleFactor_StillReturnsValidImage()
    {
        // Arrange
        var client = new HttpUpscaleService(fixture.Client);
        byte[] inputJpeg = ServiceHostTestHelpers.CreateTestJpegBytes(200, 200);
        double scaleFactor = 1.5;

        // Act
        byte[] upscaledBytes = await client.UpscaleAsync(inputJpeg, scaleFactor, CancellationToken.None);

        // Assert
        Assert.NotNull(upscaledBytes);
        Assert.NotEmpty(upscaledBytes);

        using MemoryStream ms = new(upscaledBytes);
        Image decodedImage = await Image.LoadAsync(ms);
        Assert.NotNull(decodedImage);
    }

    [Fact]
    public async Task UpscaleAsync_HealthEndpoint_ReturnsOk()
    {
        // Arrange
        var httpClient = fixture.Client;

        // Act
        HttpResponseMessage response = await httpClient.GetAsync("/prism-service/upscale/health");

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Health endpoint returned {response.StatusCode}");
    }
}
