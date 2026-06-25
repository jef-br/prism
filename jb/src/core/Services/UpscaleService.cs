namespace Prism.Core;

/// <summary>
/// In-process <see cref="IUpscaleService"/> backed by <see cref="ImageUpscaler"/>.
/// Call <see cref="Create"/> at startup to initialize the GPU session when a DirectML adapter is present.
/// </summary>
public sealed class UpscaleService : IUpscaleService {
    private UpscaleService() { }

    /// <summary>
    /// Resolves the Real-ESRGAN model asset and initializes the GPU session when a DirectML adapter is
    /// present. When no hardware DirectML adapter is detected the CPU Lanczos4 fallback is used without
    /// loading any model. Throws <see cref="PrismConfigurationException"/> when DirectML is available but
    /// the model asset cannot be located.
    /// </summary>
    public static UpscaleService Create() {
        if (ImageUpscaler.IsGpuAvailable) {
            string? modelPath = PrismConfigLocator.FindModelAsset(
                "Images/Upscale/ONNX/Real-ESRGAN_x2plus.onnx");

            if (modelPath is null)
                throw new PrismConfigurationException(
                    "Real-ESRGAN ONNX model not found. Deploy Real-ESRGAN_x2plus.onnx to " +
                    "Images/Upscale/ONNX/ next to Prism_Config.json, or set PRISM_ONNX_MODEL_DIR.");

            Upscaler_g_p_u.Initialize(modelPath);
        }

        return new UpscaleService();
    }

    /// <inheritdoc/>
    public Task<byte[]> UpscaleAsync( byte[] imageBytes, double scaleFactor, CancellationToken _ ) =>
        Task.FromResult(ImageUpscaler.Upscale(imageBytes, scaleFactor));
}
