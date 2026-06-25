namespace Prism.Core;

/// <summary>
/// Upscales raw JPEG image bytes by a given scale factor. Can run in-process or as a standalone
/// service reachable at <see cref="PrismServiceRoutes.Upscale"/>.
/// </summary>
public interface IUpscaleService {
    /// <summary>Returns the upscaled JPEG bytes.</summary>
    Task<byte[]> UpscaleAsync( byte[] imageBytes, double scaleFactor, CancellationToken cancellationToken );
}
