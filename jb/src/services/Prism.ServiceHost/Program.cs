using Prism.Core;

// PRISM service host — exposes the public services (Matching, Generate, Transform, Upscale) over HTTP so
// they can be deployed and scaled independently. Ingest is core, not a public service: media enters PRISM
// only through in-process ingress, so this host has no ingest route. By default the host exposes all public
// services; set PRISM_SERVICE=matching|generate|transform|upscale to run a single service as its own
// deployable host. Every host shares the local job temp folder, which is the artifact bus; there is no
// cloud storage.

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// PascalCase on the wire, matching the API and the HTTP service clients.
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = null);

string? onlyService = Environment.GetEnvironmentVariable("PRISM_SERVICE");
bool Hosts(string serviceName) =>
    string.IsNullOrWhiteSpace(onlyService) || string.Equals(onlyService, serviceName, StringComparison.OrdinalIgnoreCase);

// Load the same configuration the in-process core validates at startup.
PrismConfiguration configuration = PrismConfiguration.LoadPrismConfig(
    ConfigLoader.RequireFile(PrismConfiguration.FileName));

WebApplication app = builder.Build();

// Root health: reports which services this process hosts.
app.MapGet(PrismServiceRoutes.Health, () =>
    Results.Json(new { status = "ok", host = "prism-service-host", services = onlyService ?? "all" }));

// Each hosted service is constructed inside its own branch so a single-service host loads only the
// resources it serves (a transform host never loads CLIP; a matching host never fail-fasts on the
// Real-ESRGAN asset). Fail-fast is preserved per hosted service: no model → no host.

if (Hosts("matching"))
{
    IArtifactStore store      = new LocalArtifactStore();
    IMatchingService matching = new MatchingService(configuration);
    app.MapPost(PrismServiceRoutes.Match, async (IngestResult ingestResult, CancellationToken ct) =>
        Results.Json(await matching.MatchAsync(ingestResult, store, null, ct)));
    app.MapGet(PrismServiceRoutes.Match + "/health", () => Results.Json(new { status = "ok", service = "matching" }));
}

if (Hosts("generate"))
{
    IGenerateService generate = new GenerateService();
    app.MapPost(PrismServiceRoutes.Generate, async (MatchingResult matched, CancellationToken ct) =>
        Results.Json(await generate.GenerateAsync(matched, matched.Ingest.Parameters.Generation, null, ct)));
    app.MapGet(PrismServiceRoutes.Generate + "/health", () => Results.Json(new { status = "ok", service = "generate" }));
}

if (Hosts("transform"))
{
    // With PRISM_UPSCALE_URL set, this transform host delegates upscaling to the remote Upscale host and
    // needs no local Real-ESRGAN session. Otherwise it upscales below-minimum images via the static
    // Upscaler (T-2800), mirroring the in-process pipeline's semantics
    // (PipelineServiceFactory.EnsureUpscalerReady): a missing model asset fails hosting loud — there is
    // no fallback upscaler (T-4110).
    ITransformService transform;
    string? upscaleUrl = Environment.GetEnvironmentVariable(PipelineServiceFactory.UpscaleUrlVariable);
    if (string.IsNullOrWhiteSpace(upscaleUrl))
    {
        UpscaleService.Create(configuration);
        transform = new TransformService();
    }
    else
    {
        transform = new TransformService(new HttpUpscaleService(new Uri(upscaleUrl, UriKind.Absolute)));
    }
    app.MapPost(PrismServiceRoutes.Transform, async (MatchingResult matched, CancellationToken ct) =>
        Results.Json(await transform.TransformAsync(matched, matched.Ingest.Parameters.Transform, matched.Ingest.Parameters.Headcut, null, ct)));
    app.MapGet(PrismServiceRoutes.Transform + "/health", () => Results.Json(new { status = "ok", service = "transform" }));
}

if (Hosts("upscale"))
{
    // Dedicated upscale hosting fails fast: no model asset → no host (any machine; T-4110).
    IUpscaleService upscale = UpscaleService.Create(configuration);
    app.MapPost(PrismServiceRoutes.Upscale, async (UpscaleRequest request, CancellationToken ct) =>
        Results.Json(await upscale.UpscaleAsync(request.ImageBytes, request.ScaleFactor, ct)));
    app.MapGet(PrismServiceRoutes.Upscale + "/health", () => Results.Json(new { status = "ok", service = "upscale" }));
}

app.Run();

// Required by WebApplicationFactory<Program> for integration testing. Do not remove.
public partial class Program { }
