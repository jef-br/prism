using Prism.Core;

// PRISM service host — exposes the pipeline-visible services (Ingest, Matching, Generate, Transform, Upscale)
// over HTTP so they can be deployed and scaled independently. By default the host exposes all services;
// set PRISM_SERVICE=ingest|matching|generate|transform|upscale to run a single service as its own deployable
// host. Every host shares the local job temp folder, which is the artifact bus; there is no cloud storage.

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// PascalCase on the wire, matching the API and the HTTP service clients.
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = null);

string? onlyService = Environment.GetEnvironmentVariable("PRISM_SERVICE");
bool Hosts(string serviceName) =>
    string.IsNullOrWhiteSpace(onlyService) || string.Equals(onlyService, serviceName, StringComparison.OrdinalIgnoreCase);

// Load the same configuration and Excel model builder the in-process core validates at startup.
string configPath = PrismConfigLocator.FindPrismConfigPath()
    ?? throw new InvalidOperationException("Prism_Config.json was not found next to the service host.");
PrismConfiguration configuration = PrismConfiguration.LoadPrismConfig(configPath);
string coreDirectory = Path.GetDirectoryName(configPath)!;
ModelBuilder modelBuilder = ModelBuilder.FromConfigFile(Path.Combine(coreDirectory, "ExcelConfig.json"));

// In-process implementations — this host IS the service. Remote clients reach these over HTTP.
IArtifactStore store          = new LocalArtifactStore();
IIngestService ingest         = new IngestService(configuration, modelBuilder);
IMatchingService matching     = new MatchingService(configuration);
IGenerateService generate     = new GenerateService();
ITransformService transform   = new TransformService();
IUpscaleService upscale       = UpscaleService.Create();

WebApplication app = builder.Build();

// Root health: reports which services this process hosts.
app.MapGet(PrismServiceRoutes.Health, () =>
    Results.Json(new { status = "ok", host = "prism-service-host", services = onlyService ?? "all" }));

if (Hosts("ingest"))
{
    app.MapPost(PrismServiceRoutes.Ingest, async (PrismJobRequest request, CancellationToken ct) =>
        Results.Json(await ingest.ImportAsync(request, store, null, ct)));
    app.MapGet(PrismServiceRoutes.Ingest + "/health", () => Results.Json(new { status = "ok", service = "ingest" }));
}

if (Hosts("matching"))
{
    app.MapPost(PrismServiceRoutes.Match, async (IngestResult ingestResult, CancellationToken ct) =>
        Results.Json(await matching.MatchAsync(ingestResult, store, null, ct)));
    app.MapGet(PrismServiceRoutes.Match + "/health", () => Results.Json(new { status = "ok", service = "matching" }));
}

if (Hosts("generate"))
{
    app.MapPost(PrismServiceRoutes.Generate, async (MatchingResult matched, CancellationToken ct) =>
        Results.Json(await generate.GenerateAsync(matched, matched.Ingest.Parameters.Generation, null, ct)));
    app.MapGet(PrismServiceRoutes.Generate + "/health", () => Results.Json(new { status = "ok", service = "generate" }));
}

if (Hosts("transform"))
{
    app.MapPost(PrismServiceRoutes.Transform, async (MatchingResult matched, CancellationToken ct) =>
        Results.Json(await transform.TransformAsync(matched, matched.Ingest.Parameters.Transform, matched.Ingest.Parameters.Headcut, null, ct)));
    app.MapGet(PrismServiceRoutes.Transform + "/health", () => Results.Json(new { status = "ok", service = "transform" }));
}

if (Hosts("upscale"))
{
    app.MapPost(PrismServiceRoutes.Upscale, async (UpscaleRequest request, CancellationToken ct) =>
        Results.Json(await upscale.UpscaleAsync(request.ImageBytes, request.ScaleFactor, ct)));
    app.MapGet(PrismServiceRoutes.Upscale + "/health", () => Results.Json(new { status = "ok", service = "upscale" }));
}

app.Run();
