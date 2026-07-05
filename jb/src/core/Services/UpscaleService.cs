namespace Prism.Core;

/// <summary>
/// In-process <see cref="IUpscaleService"/> backed by <see cref="ImageUpscaler"/>.
/// Call <see cref="Create"/> at startup to initialize the GPU session when a DirectML adapter is present.
/// </summary>
public sealed class UpscaleService : IUpscaleService {
    private UpscaleService() { }

    private const string TilingConfigRelativePath = "Images/Upscale/cfg_Upscale.json";

    /// <summary>
    /// Resolves the Real-ESRGAN model asset and tiling config, and initializes the GPU session when a
    /// DirectML adapter is present. When no hardware DirectML adapter is detected the CPU Lanczos4
    /// fallback is used without loading any model. Throws <see cref="PrismConfigurationException"/> when
    /// DirectML is available but the model asset or tiling config cannot be located.
    /// </summary>
    public static UpscaleService Create( PrismConfiguration configuration ) {
        if (ImageUpscaler.IsGpuAvailable) {
            string? modelPath = PrismConfigLocator.FindModelAsset(configuration.UpscaleModelPath);

            if (modelPath is null)
                throw new PrismConfigurationException(
                    $"Real-ESRGAN ONNX model not found at '{configuration.UpscaleModelPath}'. Deploy it next " +
                    "to Prism_Config.json, or set PRISM_ONNX_MODEL_DIR.");

            string? configPath = PrismConfigLocator.FindModelAsset(TilingConfigRelativePath);

            if (configPath is null)
                throw new PrismConfigurationException(
                    $"cfg_Upscale.json not found at '{TilingConfigRelativePath}'. Deploy it next " +
                    "to Prism_Config.json, or set PRISM_ONNX_MODEL_DIR.");

            Upscaler_g_p_u.Initialize(modelPath, configPath);
        }

        return new UpscaleService();
    }

    /// <inheritdoc/>
    public Task<byte[]> UpscaleAsync( byte[] imageBytes, double scaleFactor, CancellationToken _ ) =>
        Task.FromResult(ImageUpscaler.Upscale(imageBytes, scaleFactor));
}
