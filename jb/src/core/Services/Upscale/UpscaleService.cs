namespace Prism.Services.Upscale;

/// <summary>
/// In-process <see cref="IUpscaleService"/> backed by <see cref="Upscaler"/>.
/// Call <see cref="Create"/> at startup to initialize the Real-ESRGAN session — DirectML when a
/// hardware adapter is present, CPU otherwise (OnnxSessionFactory decides; T-4110 mandate).
/// </summary>
public sealed class UpscaleService : IUpscaleService {
    private UpscaleService() { }

    private const string TilingConfigRelativePath = "Services/Upscale/Engine/cfg_Upscale.json";

    /// <summary>
    /// Resolves the Real-ESRGAN model asset and tiling config and initializes the model session on
    /// every host — the execution provider (DirectML vs CPU) is the factory's decision, not a load
    /// gate. Throws <see cref="PrismConfigurationException"/> when the model asset or tiling config
    /// cannot be located, or (from <see cref="Upscaler.Initialize"/>) when the model file is present
    /// but corrupt — there is no fallback upscaler (T-4110).
    /// </summary>
    public static UpscaleService Create(PrismConfiguration configuration) {
        string? modelPath = ModelAssetLocator.Find(configuration.UpscaleModelPath);

        if (modelPath is null)
            throw new PrismConfigurationException(
                $"Real-ESRGAN ONNX model not found at '{configuration.UpscaleModelPath}'. Deploy it next " +
                "to Prism_Config.json, or set PRISM_ONNX_MODEL_DIR.");

        string? configPath = ModelAssetLocator.Find(TilingConfigRelativePath);

        if (configPath is null)
            throw new PrismConfigurationException(
                $"cfg_Upscale.json not found at '{TilingConfigRelativePath}'. Deploy it next " +
                "to Prism_Config.json, or set PRISM_ONNX_MODEL_DIR.");

        Upscaler.Initialize(modelPath, configPath);

        return new UpscaleService();
    }

    /// <inheritdoc/>
    public Task<byte[]> UpscaleAsync(byte[] imageBytes, double scaleFactor, CancellationToken _) =>
        Task.FromResult(Upscaler.Upscale(imageBytes, scaleFactor));
}
