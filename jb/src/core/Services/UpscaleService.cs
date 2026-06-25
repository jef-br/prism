namespace Prism.Core;

/// <summary>In-process <see cref="IUpscaleService"/> backed by <see cref="ImageUpscaler"/>.</summary>
public sealed class UpscaleService : IUpscaleService {
    public Task<byte[]> UpscaleAsync( byte[] imageBytes, double scaleFactor, CancellationToken _ ) =>
        Task.FromResult(ImageUpscaler.Upscale(imageBytes, scaleFactor));
}
