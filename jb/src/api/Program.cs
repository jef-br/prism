using System.Text.Json;
using Prism.Api;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader());
});

PrismApiConfiguration apiConfiguration = PrismApiConfiguration.Load();

// Apply the configured maximum request size (Prism_Config.json → Input.MAXIMUM_REQUEST_SIZE) to the
// transport and multipart form limits. Without this, Kestrel enforces its 30 MB default and rejects
// legitimate batches with 413 even though the configured ceiling is far higher.
if (apiConfiguration.MaximumRequestBytes > 0)
{
    builder.WebHost.ConfigureKestrel(options =>
        options.Limits.MaxRequestBodySize = apiConfiguration.MaximumRequestBytes);

    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
        options.MultipartBodyLengthLimit = apiConfiguration.MaximumRequestBytes);
}

builder.Services.AddSingleton(apiConfiguration);
builder.Services.AddSingleton<PrismService>();
builder.Services.AddSingleton<PrismJobCoordinator>();

WebApplication app = builder.Build();
app.UseCors();
PrismJobCoordinator jobCoordinator = app.Services.GetRequiredService<PrismJobCoordinator>();

app.MapGet("/PRISM/health", (PrismApiConfiguration configuration, PrismJobCoordinator coordinator) =>
{
    PrismHealthResponse response = new()
    {
        Message = "Prism Health OK",
        CanAcceptJobs = configuration.ConfigReady && coordinator.CanAcceptJobs,
        ProcessingWired = true,
        ActiveJobCount = coordinator.ActiveJobCount,
        QueuedJobCount = coordinator.QueuedJobCount,
        MaxQueuedJobs = coordinator.MaxQueuedJobs,
        MaxConcurrentJobs = coordinator.MaxConcurrentJobs,
        SupportedRuntimeProviders = ["CPU"],
        ConfigReady = configuration.ConfigReady,
        RequiredModelAssetsReady = configuration.RequiredModelAssetsReady,
        TempStorageReady = configuration.TempStorageReady,
        Notes = configuration.ConfigReady
            ? "T-200 API routes are wired to the minimal PRISM core adapter."
            : configuration.ConfigReadinessMessage
    };

    return Results.Ok(response);
});

app.MapGet("/PRISM/config", (PrismApiConfiguration configuration, PrismJobCoordinator coordinator) =>
{
    PrismSafeConfigResponse response = new()
    {
        ConfigReady = configuration.ConfigReady,
        SafeConfigurationAvailable = configuration.ConfigReady,
        AcceptedMediaTypes = configuration.AcceptedMediaTypes,
        OutputFormats = ["zip", "json"],
        VisibleFeatureFlags = new PrismVisibleFeatureFlags(
            Rename: true,
            Transform: true,
            Generation: true,
            ProgressSse: true,
            MinimalCoreAdapter: true),
        Limits = configuration.Limits,
        Queue = new PrismQueueConfigResponse(coordinator.MaxQueuedJobs, coordinator.MaxConcurrentJobs),
        Notes = configuration.ConfigReadinessMessage
    };

    return Results.Ok(response);
});

app.MapPost("/PRISM/process", async (HttpContext context, PrismApiConfiguration configuration, PrismJobCoordinator coordinator) =>
{
    PrismProcessIngressResult ingressResult = await PrismProcessIngressReader.Read(context.Request, configuration);
    if (ingressResult.Error is not null)
    {
        return Results.Json(ingressResult.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    PrismJobRequest coreRequest = ingressResult.Request!;
    if (!coordinator.TryEnqueue(coreRequest, BuildJobUrls(context.Request, coreRequest), out PrismJobStartEnvelope? envelope))
    {
        PrismPreCoreErrorResponse errorResponse = PrismPreCoreErrorResponse.Create(
            context.TraceIdentifier,
            "QUEUE_FULL",
            "PRISM cannot accept more queued jobs right now.",
            [$"MaxQueuedJobs={coordinator.MaxQueuedJobs}"],
            ["request:QUEUE_FULL"]);

        return Results.Json(errorResponse, statusCode: StatusCodes.Status429TooManyRequests);
    }

    PrismJobStartEnvelope acceptedEnvelope = envelope ?? throw new InvalidOperationException("Accepted job envelope was not created.");
    return Results.Accepted(acceptedEnvelope.ResultUrl, acceptedEnvelope);
});

app.MapPost("/PRISM/match/lite", async (HttpContext context, PrismApiConfiguration configuration, PrismService prismService) =>
{
    PrismMatchLiteIngressResult liteResult = await PrismMatchLiteIngressReader.Read(context.Request, configuration);
    if (liteResult.Error is not null)
        return Results.Json(liteResult.Error, statusCode: StatusCodes.Status400BadRequest);

    MatchOnlyResult result = prismService.MatchLite(liteResult.Images!, liteResult.ExcelFiles!);
    liteResult.CleanUp();
    return Results.Ok(result.FileNameMap);
});

app.MapPost("/PRISM/match", async (HttpContext context, PrismApiConfiguration configuration, PrismService prismService) =>
{
    PrismProcessIngressResult ingressResult = await PrismProcessIngressReader.Read(context.Request, configuration);
    if (ingressResult.Error is not null)
        return Results.Json(ingressResult.Error, statusCode: StatusCodes.Status400BadRequest);

    MatchOnlyResult result = await prismService.MatchOnlyAsync(ingressResult.Request!, context.RequestAborted);
    return Results.Ok(result.FileNameMap);
});

app.MapGet("/PRISM/jobs/{jobID:guid}/progress", async (Guid jobID, HttpContext context, PrismJobCoordinator coordinator) =>
{
    PrismProgressSubscription? subscription = coordinator.Subscribe(jobID);
    if (subscription is null)
    {
        return Results.NotFound();
    }

    if (subscription.IsTerminal)
    {
        return Results.StatusCode(StatusCodes.Status410Gone);
    }

    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";
    context.Response.ContentType = "text/event-stream";

    await foreach (PipelineProgressEvent progressEvent in subscription.Events.ReadAllAsync(context.RequestAborted))
    {
        string payload = JsonSerializer.Serialize(progressEvent);
        await context.Response.WriteAsync($"event: progress\ndata: {payload}\n\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }

    return Results.Empty;
});

app.MapGet("/PRISM/jobs/{jobID:guid}/result", (Guid jobID, PrismJobCoordinator coordinator) =>
{
    PrismStoredJobResult? storedResult = coordinator.GetResult(jobID);
    if (storedResult is null)
    {
        return Results.NotFound();
    }

    if (!storedResult.IsTerminal)
    {
        return Results.Json(new { JobID = jobID, Status = storedResult.Status }, statusCode: StatusCodes.Status202Accepted);
    }

    PrismJobResult result = storedResult.Result ?? throw new InvalidOperationException("Terminal job result was not stored.");

    if (string.Equals(result.OutputFormat, "zip", StringComparison.OrdinalIgnoreCase)
        && string.Equals(result.Status, "Completed", StringComparison.OrdinalIgnoreCase))
    {
        return Results.File(
            result.ZipBytes ?? Array.Empty<byte>(),
            "application/zip",
            $"{jobID}.zip");
    }

    return Results.Json(new PrismJsonResultEnvelope(result));
});

app.MapGet("/PRISM/jobs", (PrismJobCoordinator coordinator) =>
{
    return Results.Ok(coordinator.ListJobs());
});

app.Run();

static PrismJobUrls BuildJobUrls(HttpRequest request, PrismJobRequest coreRequest)
{
    string baseUrl = $"{request.Scheme}://{request.Host}";
    string progressUrl = $"{baseUrl}/PRISM/jobs/{coreRequest.JobID}/progress";
    string resultUrl = $"{baseUrl}/PRISM/jobs/{coreRequest.JobID}/result";
    return new PrismJobUrls(progressUrl, resultUrl);
}
