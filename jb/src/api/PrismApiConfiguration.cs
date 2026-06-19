/// <summary>
/// API runtime configuration. Every value is sourced from Prism_Config.json via the
/// core <see cref="PrismConfiguration"/> loader. There is no graceful degradation:
/// any missing config file or invalid value fails loud at startup.
/// </summary>
internal sealed record PrismApiConfiguration
{
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
    public IReadOnlyList<string> AcceptedMediaTypes { get; init; } = [];

    /// <summary>
    /// Loads API configuration from Prism_Config.json via the core loader.
    /// Fails loud with <see cref="PrismConfigurationException"/> if the file is missing,
    /// invalid, or temp storage is unavailable.
    /// </summary>
    public static PrismApiConfiguration Load()
    {
        string configPath = PrismConfigLocator.FindPrismConfigPath()
            ?? throw new PrismConfigurationException(
                "Prism_Config.json was not found in any expected location. " +
                "Ensure the file is deployed next to the running assembly.");

        PrismConfiguration core = PrismConfiguration.Load(configPath);

        if (!Directory.Exists(Path.GetTempPath()))
        {
            throw new PrismConfigurationException(
                $"Temp storage is not available at: {Path.GetTempPath()}");
        }

        return new PrismApiConfiguration
        {
            ConfigReady = true,
            RequiredModelAssetsReady = true,
            TempStorageReady = true,
            ConfigReadinessMessage = "Prism_Config.json loaded and safe API configuration is available.",
            JobRetentionPeriodInHours = core.JobRetentionPeriodInHours,
            MaxQueuedJobs = core.MaxQueuedJobs,
            MaxConcurrentJobs = core.MaxConcurrentJobs,
            MaximumRequestBytes = core.MaximumRequestBytes,
            MinimumImageCount = core.MinimumImageCount,
            MaximumImageCount = core.MaximumImageCount,
            MinimumImageBytes = core.MinimumImageBytes,
            MaximumImageBytes = core.MaximumImageBytes,
            MinimumExcelCount = core.MinimumExcelCount,
            MaximumExcelCount = core.MaximumExcelCount,
            MinimumExcelBytes = core.MinimumExcelBytes,
            MaximumExcelBytes = core.MaximumExcelBytes,
            MaximumZipCount = core.MaximumZipCount,
            MaximumZipBytes = core.MaximumZipBytes,
            AcceptedMediaTypes = core.AcceptedMediaTypes,
            Limits = new PrismSafeLimitResponse
            {
                MaximumRequestBytes = core.MaximumRequestBytes,
                MinimumImageCount = core.MinimumImageCount,
                MaximumImageCount = core.MaximumImageCount,
                MinimumImageBytes = core.MinimumImageBytes,
                MaximumImageBytes = core.MaximumImageBytes,
                MinimumExcelCount = core.MinimumExcelCount,
                MaximumExcelCount = core.MaximumExcelCount,
                MinimumExcelBytes = core.MinimumExcelBytes,
                MaximumExcelBytes = core.MaximumExcelBytes,
                MaximumZipCount = core.MaximumZipCount,
                MaximumZipBytes = core.MaximumZipBytes
            }
        };
    }
}

/// <summary>
/// Safe public limits derived from PRISM configuration.
/// </summary>
internal sealed record PrismSafeLimitResponse
{
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
