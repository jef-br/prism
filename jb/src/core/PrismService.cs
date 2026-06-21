namespace Prism.Core;

/// <summary>
/// PRISM facade. Accepts core-facing job requests, validates them, and delegates
/// real pipeline work to <see cref="Pipeline"/>. Reads like a recipe:
/// Initialize sets up validated resources; Process expresses the job lifecycle;
/// helpers below each method do their named step.
/// </summary>
public sealed class PrismService
{
    private readonly PrismConfiguration configuration;
    private readonly Pipeline pipeline;

    // -------------------------------------------------------------------------
    // Lifecycle — Initialize
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates the PRISM facade, loads and validates all configuration on startup.
    /// Throws <see cref="PrismConfigurationException"/> if any required config file or model asset is missing or invalid.
    /// </summary>
    public PrismService()
    {
        (configuration, ModelBuilder modelBuilder) = Initialize();
        pipeline = new Pipeline(configuration, modelBuilder);
    }

    /// <summary>
    /// Creates the PRISM facade with already-loaded configuration and model builder.
    /// Intended for testing and injection scenarios where config is pre-validated.
    /// </summary>
    /// <param name="configuration">Pre-validated PRISM configuration.</param>
    /// <param name="modelBuilder">Pre-loaded Excel model builder.</param>
    public PrismService(PrismConfiguration configuration, ModelBuilder modelBuilder)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        pipeline = new Pipeline(this.configuration, modelBuilder ?? throw new ArgumentNullException(nameof(modelBuilder)));
    }

    // -------------------------------------------------------------------------
    // Entry point — Process
    // -------------------------------------------------------------------------

    /// <summary>
    /// Processes one PRISM job through the full pipeline.
    /// Validates the request, delegates all stage work to <see cref="Pipeline"/>,
    /// and returns a structured result to the caller.
    /// </summary>
    /// <param name="request">The normalized core-facing job request.</param>
    /// <param name="progress">Progress callback used by API SSE transport and workbench direct invocation.</param>
    /// <param name="cancellationToken">Host shutdown token — does not cancel accepted user jobs.</param>
    /// <returns>A structured PRISM job result.</returns>
    public async Task<PrismJobResult> Process(
        PrismJobRequest request,
        Func<PipelineProgressEvent, Task>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        PipelineResult pipelineResult = await pipeline.RunAsync(request, progress, cancellationToken);

        return BuildJobResult(request, pipelineResult);
    }

    // -------------------------------------------------------------------------
    // Helpers — called by Initialize
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads Prism_Config.json, validates all required folder-local config files,
    /// and pre-loads the Excel model builder so ExcelConfig.json failures surface at startup.
    /// Throws <see cref="PrismConfigurationException"/> if any required asset is missing or invalid.
    /// </summary>
    private static (PrismConfiguration Config, ModelBuilder ExcelModelBuilder) Initialize()
    {
        string configPath = LocatePrismConfig();
        PrismConfiguration config = PrismConfiguration.LoadPrismConfig(configPath);
        ValidateRequiredFolderLocalConfigs(configPath);
        string coreDir = Path.GetDirectoryName(configPath)!;
        ModelBuilder modelBuilder = ModelBuilder.FromConfigFile(Path.Combine(coreDir, "Excel", "ExcelConfig.json"));
        return (config, modelBuilder);
    }

    private static string LocatePrismConfig()
    {
        string? configPath = PrismConfigLocator.FindPrismConfigPath();

        if (configPath is null)
        {
            throw new PrismConfigurationException(
                "Prism_Config.json was not found in any expected location. " +
                "Ensure the file is deployed next to the running assembly.");
        }

        return configPath;
    }

    private static void ValidateRequiredFolderLocalConfigs(string prismConfigPath)
    {
        string[] requiredFolderLocalConfigs =
        [
            "Excel/ExcelConfig.json",
            "IO/cfg/HostRules.json",
            "Images/Match/MatchingConfig.json",
            "Images/Match/Translate/TranslationConfig.json",
            "Images/Order/DetOrderRules.json",
            "Images/Order/DetOrderKeywordStems.json"
        ];

        string coreDirectory = Path.GetDirectoryName(prismConfigPath)
            ?? throw new PrismConfigurationException("Could not determine core configuration directory.");

        foreach (string relativePath in requiredFolderLocalConfigs)
        {
            string fullPath = Path.Combine(coreDirectory, relativePath);

            if (!File.Exists(fullPath))
            {
                throw new PrismConfigurationException(
                    $"Required PRISM configuration file was not found: {fullPath}. " +
                    "Ensure all configuration assets are deployed with the assembly.");
            }
        }
    }

    // -------------------------------------------------------------------------
    // Helpers — called by Process
    // -------------------------------------------------------------------------

    /// <summary>
    /// Validates that the request satisfies all pre-pipeline requirements.
    /// Throws <see cref="ArgumentException"/> for caller-supplied structural failures.
    /// </summary>
    private static void ValidateRequest(PrismJobRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.JobID == Guid.Empty)
        {
            throw new ArgumentException("PrismJobRequest.JobID is required.", nameof(request));
        }

        if (request.PrismProcessingParameters is null)
        {
            throw new ArgumentException("PrismProcessingParameters is required.", nameof(request));
        }

        if (request.ImageRecords.Count == 0)
        {
            throw new ArgumentException("At least one accepted image record is required.", nameof(request));
        }

        if (request.ExcelRecords.Count == 0)
        {
            throw new ArgumentException("At least one accepted Excel record is required.", nameof(request));
        }
    }

    /// <summary>
    /// Projects a completed <see cref="PipelineResult"/> into the caller-facing <see cref="PrismJobResult"/>.
    /// </summary>
    private static PrismJobResult BuildJobResult(PrismJobRequest request, PipelineResult pipelineResult)
    {
        IReadOnlyList<ManifestImageRow> rows = pipelineResult.Manifest.ImageRows;

        return new PrismJobResult
        {
            JobID              = request.JobID,
            ClientRequestToken = request.ClientRequestToken,
            Status             = pipelineResult.Status,
            OutputFormat       = pipelineResult.OutputFormat,
            FailureReason      = pipelineResult.FailureReason,
            Warnings           = pipelineResult.Warnings,
            Manifest           = pipelineResult.Manifest,
            ZipBytes           = pipelineResult.ZipBytes,
            OkImages           = rows.Where(r => r.Status == "Ok").ToList(),
            KoImages           = rows.Where(r => r.Status == "Ko").ToList()
        };
    }
}
