# PRISM Testing

How PRISM's automated tests are organized, how to run one service's suite in isolation, and why the layout is split along public-service boundaries.

## Layout: five projects, per-service suites by namespace

Tests are split into five xUnit projects under `jb/src/tests/`, one per public service plus a core project and a shared fixture library. Namespaces are unchanged from the single-project layout: each service boundary (the `I*Service` interfaces in `jb/src/core/Services/`) has its own suite — a folder + namespace `PrismCoreTests.<Suite>` — independently runnable via a `dotnet test` filter regardless of which project it now lives in.

| Project | Suites (namespace) |
|---|---|
| `Prism.Services.Matching.Tests` | `Match`, `Order`, `Classify`, `Analyzers` |
| `Prism.Services.Generate.Tests` | `Generate` |
| `Prism.Services.Transform.Tests` | `Transform` |
| `Prism.Services.Upscale.Tests` | `Upscale` |
| `Prism.Core.Tests` | `Ingest`, `Excel`, `Export`, `Rename`, `ImageNGP`, `Services`, `ServiceHost`, root (`PipelineIntegrationTests`) |
| `Prism.Tests.Shared` | not a test project — holds `PipelineFixture`, shared across `Prism.Core.Tests` and `Prism.Services.Matching.Tests` (MatchLite and SubjectEdgeDetector real-image tests both need the real pipeline fixture) |

```
dotnet test jb/src/PRISM.sln                                                              # everything, every project
dotnet test jb/src/tests/Prism.Services.Matching.Tests/Prism.Services.Matching.Tests.csproj   # one project
dotnet test jb/src/PRISM.sln --filter "FullyQualifiedName~PrismCoreTests.<Suite>"             # one suite, any project
dotnet test jb/src/PRISM.sln --filter "FullyQualifiedName!~PipelineIntegrationTests"           # unit tests only
```

| Suite (namespace) | Service boundary / area |
|---|---|
| `PrismCoreTests.Ingest` | `IIngestService` — `Importer` direct/multipart/stream/local inputs, ZIP expansion, Excel routing, URL fetchers (`FetchDispatcher`, `Fetch_HTTPS_DirectFile`) |
| `PrismCoreTests.Excel` | IEM building, header detection, dedup (`ModelBuilder`) |
| `PrismCoreTests.Classify` | `IClassificationService` — feature analysis, phenotype rules, edge detection |
| `PrismCoreTests.Analyzers` | `IFeatureAnalysisService` — YOLO detector, visual analyzers, product-type resolution |
| `PrismCoreTests.Match` | `IMatchingService` — matchers, MatchLite route |
| `PrismCoreTests.Order` | det-slot ordering |
| `PrismCoreTests.Rename` | output filename stems |
| `PrismCoreTests.ImageNGP` | ImageNGP taxonomy validation |
| `PrismCoreTests.Generate` | `IGenerateService` |
| `PrismCoreTests.Transform` | `ITransformService` — Tx classes, pixel-level worked examples |
| `PrismCoreTests.Upscale` | `IUpscaleService` — `Upscaler` init contract, tile-blend math |
| `PrismCoreTests.Export` | manifest/ZIP export |
| `PrismCoreTests.Services` | service composition glue — `LocalArtifactStore`, service contract serialization |
| `PrismCoreTests.ServiceHost` | `Prism.ServiceHost` HTTP round-trip — `Http*Service` clients against the standalone per-service host |
| `PrismCoreTests` (root, in `Prism.Core.Tests`) | end-to-end: `PipelineIntegrationTests` + shared `PipelineFixture` (runs the full pipeline once per request shape and caches results) |

Filters use substring matching, so `~PrismCoreTests.Match` also matches `PrismCoreTests.Matching…` if such a namespace is ever added — keep new suite names non-prefixing.

## Decision: split along public-service boundaries (2026-07-15, T-3300 step 3)

Supersedes the 2026-07-10 decision below. Once `Prism.ServiceHost`/`Http*Service` distributed correctness was proven (T-3300 steps 1-2), the deferred split landed:

- **Accepted:** one test project per public service (Matching, Generate, Transform, Upscale) plus `Prism.Core.Tests` for everything that isn't a separately-deployable service (Ingest, Excel, Export, Rename, ImageNGP, Services, ServiceHost, the end-to-end suite). Namespaces did not change — only physical project boundaries did — so existing `--filter` invocations keep working unmodified.
- `PipelineFixture` moved to a new `Prism.Tests.Shared` classlib because it is genuinely cross-boundary: `Prism.Services.Matching.Tests` needs it (MatchLite black-box tests, `SubjectEdgeDetectorRealImageTests`' fixture-path resolution) but the fixture itself must stay reusable by `Prism.Core.Tests`' `PipelineIntegrationTests`. Both projects reference `Prism.Tests.Shared`.
- Cross-assembly internal access required new `InternalsVisibleTo` grants: `Prism.Core`, the Upscale engine assembly, and `SubjectEdgeDetector`'s assembly each now also grant the specific new test project(s) that exercise their internal types (`Prism.Services.Matching.Tests`, `Prism.Services.Generate.Tests`, `Prism.Services.Upscale.Tests`), alongside the original `Prism.Core.Tests` grant.
- CI (`.github/workflows/ci.yml`) now runs `dotnet test jb/src/PRISM.sln` — `dotnet test` on a solution runs every test project in one invocation, so nothing had to change about how CI is invoked beyond the target.

### Prior decision: one `.csproj`, not one per service (2026-07-10, superseded)

Closed from `jb/src/core/Services/jbtodo.md` ("Establish automated test suites for PRISM, organized as independently runnable suites per service"):

- **Accepted then:** per-service suites as namespaces inside a single `Prism.Core.Tests` project, each independently runnable via `--filter`.
- **Rejected then:** physically splitting into one test `.csproj` per service, deferred to T-3300 step 4 (now step 3, completed above) until distributed correctness of the `Prism.ServiceHost`/`Http*Service` seam was proven.

## Conventions

- New tests for a service go in that service's folder with namespace `PrismCoreTests.<Folder>` — never `Prism.Core.Tests.*` (breaks the filter convention; this was a real bug fixed in T-3700's audit).
- Tests needing a full `PrismService` share `PipelineFixture` (now in `Prism.Tests.Shared`) via `IClassFixture<PipelineFixture>` instead of constructing their own (avoids reloading the CLIP/YOLO models).
- The Ingest suite generates image fixtures as seeded noise at runtime (`ImporterFixture`) instead of committing binaries; noise defeats compression so files clear the configured byte minimums.
- Network-free URL testing: fetcher policy failures are asserted against the shipped `HostRules.json` (validation precedes any request); download success/error paths run against a raw-socket `LoopbackHttpServer` on 127.0.0.1 with permissive test rules — never external hosts.
- A new public service's test project needs a `ProjectReference` to `..\..\core\Prism.Core.csproj` + `..\..\core\Models\Prism.Core.Contracts.csproj` (transitive references normally cover engine-project types) and, if it touches `internal` members outside its own assembly, a matching `InternalsVisibleTo` grant on the assembly that declares them.
