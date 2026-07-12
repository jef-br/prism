namespace Prism.Api;

/// <summary>
/// API runtime configuration. Every value is sourced from Prism_Config.json via the
/// core <see cref="PrismConfiguration"/> loader. There is no graceful degradation:
/// any missing config file or invalid value fails loud at startup.
/// </summary>
internal sealed record PrismApiConfiguration {
    public bool ConfigReady { get; init; }
    public bool RequiredModelAssetsReady { get; init; }
    public bool TempStorageReady { get; init; }
    public string ConfigReadinessMessage { get; init; } = string.Empty;
    public int MaxQueuedJobs { get; init; }
    public int MaxConcurrentJobs { get; init; }
    public int JobRetentionPeriodInHours { get; init; }
    public long MaximumRequestBytes { get; init; }
    public int MinimumImageCount { get; init; }
    public int MaximumImageCount { get; init; }
    public long MinimumImageBytes { get; init; }
    public long MaximumImageBytes { get; init; }
    public int MinimumExcelCount { get; init; }
    public int MaximumExcelCount { get; init; }
    public long MinimumExcelBytes { get; init; }
    public long MaximumExcelBytes { get; init; }
    public int MaximumZipCount { get; init; }
    public long MaximumZipBytes { get; init; }
    public PrismSafeLimitResponse Limits { get; init; } = new();
    public IReadOnlyList<string> ImageMediaTypes { get; init; } = [];
    public IReadOnlyList<string> ExcelMediaTypes { get; init; } = [];
    public IReadOnlyList<string> ZipMediaTypes { get; init; } = [];
    public IReadOnlyList<string> AcceptedMediaTypes => [.. ImageMediaTypes, .. ExcelMediaTypes, .. ZipMediaTypes];
    public FetchDispatcher FetchDispatcher { get; init; } = null!;

    /// <summary>
    /// Loads API configuration from Prism_Config.json via the core loader.
    /// Fails loud with <see cref="PrismConfigurationException"/> if the file is missing,
    /// invalid, or temp storage is unavailable.
    /// </summary>
    public static PrismApiConfiguration Load() {
        string configPath = PrismConfigLocator.FindPrismConfigPath()
            ?? throw new PrismConfigurationException(
                "Prism_Config.json was not found in any expected location. " +
                "Ensure the file is deployed next to the running assembly.");

        PrismConfiguration core = PrismConfiguration.LoadPrismConfig(configPath);

        // transform_Config.json sits beside Prism_Config.json; TransformConfig.Load fails loud
        // (missing file, invalid JSON, or an out-of-range value) — same fail-fast contract as
        // PrismConfiguration.LoadPrismConfig above.
        string transformConfigPath = Path.Combine(Path.GetDirectoryName(configPath) ?? string.Empty, "transform_Config.json");
        TransformConfig.Load(transformConfigPath);

        if (!Directory.Exists(Path.GetTempPath())) {
            throw new PrismConfigurationException(
                $"Temp storage is not available at: {Path.GetTempPath()}");
        }

        return new PrismApiConfiguration {
            ConfigReady = true,
            RequiredModelAssetsReady = true,
            TempStorageReady = true,
            ConfigReadinessMessage = "Prism_Config.json loaded and safe API configuration is available.",
            JobRetentionPeriodInHours = core.JobRetentionPeriodInHours,
            MaxQueuedJobs = core.MaxQueuedJobs,
            MaxConcurrentJobs = core.MaxConcurrentJobs,
            MaximumRequestBytes = core.MaximumRequestBytes,
            MinimumImageCount = core.MinimumImageCountPerJob,
            MaximumImageCount = core.MaximumImageCountPerJob,
            MinimumImageBytes = core.MinBytesPerImg,
            MaximumImageBytes = core.MaxBytesPerImg,
            MinimumExcelCount = core.MinXLSCount,
            MaximumExcelCount = core.MaxXLSCount,
            MinimumExcelBytes = core.MinXLSBytes,
            MaximumExcelBytes = core.MaxXLSBytes,
            MaximumZipCount = core.MaxZipCount,
            MaximumZipBytes = core.MaxZipBytes,
            ImageMediaTypes = core.AcceptedImageExtensions,
            ExcelMediaTypes = core.AcceptedExcelExtensions,
            ZipMediaTypes = core.AcceptedZipExtensions,
            FetchDispatcher = FetchDispatcher.Create(),
            Limits = new PrismSafeLimitResponse {
                MaximumRequestBytes = core.MaximumRequestBytes,
                MinimumImageCount = core.MinimumImageCountPerJob,
                MaximumImageCount = core.MaximumImageCountPerJob,
                MinimumImageBytes = core.MinBytesPerImg,
                MaximumImageBytes = core.MaxBytesPerImg,
                MinimumExcelCount = core.MinXLSCount,
                MaximumExcelCount = core.MaxXLSCount,
                MinimumExcelBytes = core.MinXLSBytes,
                MaximumExcelBytes = core.MaxXLSBytes,
                MaximumZipCount = core.MaxZipCount,
                MaximumZipBytes = core.MaxZipBytes
            }
        };
    }
}

/// <summary>
/// Safe public limits derived from PRISM configuration.
/// </summary>
internal sealed record PrismSafeLimitResponse {
    public long MaximumRequestBytes { get; init; }
    public int MinimumImageCount { get; init; }
    public int MaximumImageCount { get; init; }
    public long MinimumImageBytes { get; init; }
    public long MaximumImageBytes { get; init; }
    public int MinimumExcelCount { get; init; }
    public int MaximumExcelCount { get; init; }
    public long MinimumExcelBytes { get; init; }
    public long MaximumExcelBytes { get; init; }
    public int MaximumZipCount { get; init; }
    public long MaximumZipBytes { get; init; }
}
