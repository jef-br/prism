namespace Prism.Core;

/// <summary>
/// HTTP client implementation of <see cref="IUpscaleService"/>. POSTs the image bytes and scale factor
/// to a remote Upscale host and returns the upscaled JPEG bytes.
/// </summary>
public sealed class HttpUpscaleService : IUpscaleService {
    private readonly HttpClient client;

    public HttpUpscaleService( Uri baseAddress )
        => client = ServiceHttp.CreateClient(baseAddress);

    public HttpUpscaleService( HttpClient client )
        => this.client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<byte[]> UpscaleAsync( byte[] imageBytes, double scaleFactor, CancellationToken cancellationToken ) =>
        await ServiceHttp.PostJson<UpscaleRequest, byte[]>(
            client, PrismServiceRoutes.Upscale, new UpscaleRequest(imageBytes, scaleFactor), cancellationToken);
}
