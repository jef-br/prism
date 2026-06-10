var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
});

var app = builder.Build();

app.MapGet("/PRISM/health", () =>
    Results.Ok(new PrismHealthResponse(
        Message: "Prism API host is running.",
        CanAcceptJobs: false,
        ProcessingWired: false,
        ActiveJobCount: 0,
        QueuedJobCount: 0,
        MaxQueuedJobs: 0,
        MaxConcurrentJobs: 0,
        SupportedRuntimeProviders: [],
        ConfigReady: false,
        RequiredModelAssetsReady: false,
        TempStorageReady: false,
        Notes: "Core processing and runtime configuration are not wired into this API project yet.")));

app.MapGet("/PRISM/config", () =>
    Results.Ok(new PrismConfigReadinessResponse(
        ConfigReady: false,
        SafeConfigurationAvailable: false,
        Notes: "Runtime configuration is owned by Prism.cs and is not wired into this API project yet.")));

app.MapPost("/PRISM/process", () =>
    Results.Problem(
        title: "PRISM processing is not wired.",
        detail: "This API host is runnable, but it does not accept processing jobs until core ingestion and Prism.Process are connected.",
        statusCode: StatusCodes.Status501NotImplemented));

app.Run();

/// <summary>
/// Describes the current readiness of the API host without claiming that PRISM processing is available.
/// </summary>
/// <param name="Message">A safe status message for callers.</param>
/// <param name="CanAcceptJobs">Whether this API instance can currently accept PRISM jobs.</param>
/// <param name="ProcessingWired">Whether the API is connected to the PRISM processing pipeline.</param>
/// <param name="ActiveJobCount">The number of jobs currently being processed by this API instance.</param>
/// <param name="QueuedJobCount">The number of jobs currently queued by this API instance.</param>
/// <param name="MaxQueuedJobs">The configured maximum queued jobs value, or zero when configuration is unavailable.</param>
/// <param name="MaxConcurrentJobs">The configured maximum concurrent jobs value, or zero when configuration is unavailable.</param>
/// <param name="SupportedRuntimeProviders">The configured runtime providers that are safe to expose.</param>
/// <param name="ConfigReady">Whether runtime configuration has been loaded and validated.</param>
/// <param name="RequiredModelAssetsReady">Whether required model assets have been validated.</param>
/// <param name="TempStorageReady">Whether temporary storage has been validated.</param>
/// <param name="Notes">A safe explanation of missing readiness pieces.</param>
internal sealed record PrismHealthResponse(
    string Message,
    bool CanAcceptJobs,
    bool ProcessingWired,
    int ActiveJobCount,
    int QueuedJobCount,
    int MaxQueuedJobs,
    int MaxConcurrentJobs,
    IReadOnlyList<string> SupportedRuntimeProviders,
    bool ConfigReady,
    bool RequiredModelAssetsReady,
    bool TempStorageReady,
    string Notes);

/// <summary>
/// Describes whether a safe public PRISM configuration payload is available from the API host.
/// </summary>
/// <param name="ConfigReady">Whether runtime configuration has been loaded and validated.</param>
/// <param name="SafeConfigurationAvailable">Whether a sanitized configuration response is available to callers.</param>
/// <param name="Notes">A safe explanation of why configuration is or is not available.</param>
internal sealed record PrismConfigReadinessResponse(
    bool ConfigReady,
    bool SafeConfigurationAvailable,
    string Notes);
