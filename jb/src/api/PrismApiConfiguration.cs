using System.Text.Json;

/// <summary>
/// Safe API runtime configuration loaded from PRISM configuration files.
/// </summary>
internal sealed record PrismApiConfiguration
{
    public bool ConfigReady { get; init; }
    public bool RequiredModelAssetsReady { get; init; }
    public bool TempStorageReady { get; init; }
    public string ConfigReadinessMessage { get; init; } = string.Empty;
    public int MaxQueuedJobs { get; init; } = 100;
    public int MaxConcurrentJobs { get; init; } = 1;
    public int JobRetentionPeriodInHours { get; init; } = 24;
    public long MaximumRequestBytes { get; init; }
    public int MinimumImageCount { get; init; } = 1;
    public int MaximumImageCount { get; init; } = 2500;
    public long MinimumImageBytes { get; init; }
    public long MaximumImageBytes { get; init; }
    public int MinimumExcelCount { get; init; } = 1;
    public int MaximumExcelCount { get; init; } = 10;
    public long MinimumExcelBytes { get; init; }
    public long MaximumExcelBytes { get; init; }
    public int MaximumZipCount { get; init; } = 50;
    public long MaximumZipBytes { get; init; }
    public PrismSafeLimitResponse Limits { get; init; } = new();
    public IReadOnlyList<string> AcceptedMediaTypes { get; init; } =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".tif",
        ".tiff",
        ".pdf",
        ".webp",
        ".bmp",
        ".gif",
        ".xlsx",
        ".zip"
    ];

    /// <summary>
    /// Loads safe API configuration from Prism_Config.json.
    /// </summary>
    public static PrismApiConfiguration Load()
    {
        string? configPath = FindConfigPath();
        if (configPath is null)
        {
            return new PrismApiConfiguration
            {
                ConfigReady = false,
                RequiredModelAssetsReady = false,
                TempStorageReady = Directory.Exists(Path.GetTempPath()),
                ConfigReadinessMessage = "Prism_Config.json was not found."
            };
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(configPath));
            JsonElement root = document.RootElement;

            long maximumRequestBytes = GetInt64(root, "Input", "MAXIMUM_REQUEST_SIZE") ?? 0;
            int minimumImageCount = GetInt32(root, "Input", "Images", "amount", "min") ?? 1;
            int maximumImageCount = GetInt32(root, "Input", "Images", "amount", "max") ?? 2500;
            long minimumImageBytes = GetInt64(root, "Input", "Images", "filesize", "min") ?? 0;
            long maximumImageBytes = GetInt64(root, "Input", "Images", "filesize", "max") ?? 0;
            int minimumExcelCount = GetInt32(root, "Input", "EXCEL", "amount", "min") ?? 1;
            int maximumExcelCount = GetInt32(root, "Input", "EXCEL", "amount", "max") ?? 10;
            long minimumExcelBytes = GetInt64(root, "Input", "EXCEL", "filesize", "min") ?? 0;
            long maximumExcelBytes = GetInt64(root, "Input", "EXCEL", "filesize", "max") ?? 0;
            int maximumZipCount = GetInt32(root, "Input", "ZIP", "amount", "max") ?? 50;
            long maximumZipBytes = GetInt64(root, "Input", "ZIP", "filesize", "max") ?? 0;
            int retentionHours = GetInt32(root, "Jobs", "JobRetentionPeriodInHours") ?? 24;

            return new PrismApiConfiguration
            {
                ConfigReady = true,
                RequiredModelAssetsReady = true,
                TempStorageReady = Directory.Exists(Path.GetTempPath()),
                ConfigReadinessMessage = "Prism_Config.json loaded and safe API configuration is available.",
                JobRetentionPeriodInHours = retentionHours,
                MaximumRequestBytes = maximumRequestBytes,
                MinimumImageCount = minimumImageCount,
                MaximumImageCount = maximumImageCount,
                MinimumImageBytes = minimumImageBytes,
                MaximumImageBytes = maximumImageBytes,
                MinimumExcelCount = minimumExcelCount,
                MaximumExcelCount = maximumExcelCount,
                MinimumExcelBytes = minimumExcelBytes,
                MaximumExcelBytes = maximumExcelBytes,
                MaximumZipCount = maximumZipCount,
                MaximumZipBytes = maximumZipBytes,
                Limits = new PrismSafeLimitResponse
                {
                    MaximumRequestBytes = maximumRequestBytes,
                    MinimumImageCount = minimumImageCount,
                    MaximumImageCount = maximumImageCount,
                    MinimumImageBytes = minimumImageBytes,
                    MaximumImageBytes = maximumImageBytes,
                    MinimumExcelCount = minimumExcelCount,
                    MaximumExcelCount = maximumExcelCount,
                    MinimumExcelBytes = minimumExcelBytes,
                    MaximumExcelBytes = maximumExcelBytes,
                    MaximumZipCount = maximumZipCount,
                    MaximumZipBytes = maximumZipBytes
                }
            };
        }
        catch (JsonException exception)
        {
            return new PrismApiConfiguration
            {
                ConfigReady = false,
                RequiredModelAssetsReady = false,
                TempStorageReady = Directory.Exists(Path.GetTempPath()),
                ConfigReadinessMessage = $"Prism_Config.json is invalid: {exception.Message}"
            };
        }
    }

    private static string? FindConfigPath()
    {
        string[] candidatePaths =
        [
            Path.Combine(AppContext.BaseDirectory, "Prism_Config.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Prism_Config.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "core", "Prism_Config.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "jb", "src", "core", "Prism_Config.json")
        ];

        return candidatePaths.FirstOrDefault(File.Exists);
    }

    private static int? GetInt32(JsonElement root, params string[] path)
    {
        JsonElement? element = GetElement(root, path);
        return element.HasValue && element.Value.TryGetInt32(out int value) ? value : null;
    }

    private static long? GetInt64(JsonElement root, params string[] path)
    {
        JsonElement? element = GetElement(root, path);
        return element.HasValue && element.Value.TryGetInt64(out long value) ? value : null;
    }

    private static JsonElement? GetElement(JsonElement root, params string[] path)
    {
        JsonElement current = root;
        foreach (string segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current;
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
