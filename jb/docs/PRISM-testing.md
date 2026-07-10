# PRISM Testing

How PRISM's automated tests are organized, how to run one service's suite in isolation, and why the layout is one project rather than one project per service.

## Layout: one project, per-service suites by namespace

All tests live in a single xUnit project, `jb/src/tests/Prism.Core.Tests/Prism.Core.Tests.csproj`. Each service boundary (the `I*Service` interfaces in `jb/src/core/Services/`) has its own suite: a folder + namespace `PrismCoreTests.<Suite>`, independently runnable via a `dotnet test` filter.

```
dotnet test jb/src/tests/Prism.Core.Tests/Prism.Core.Tests.csproj                        # everything
dotnet test jb/src/tests/Prism.Core.Tests/Prism.Core.Tests.csproj --filter "FullyQualifiedName~PrismCoreTests.<Suite>"
dotnet test jb/src/tests/Prism.Core.Tests/Prism.Core.Tests.csproj --filter "FullyQualifiedName!~PipelineIntegrationTests"   # unit tests only
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
| `PrismCoreTests.Upscale` | `IUpscaleService` — `Upscaler_g_p_u` init contract, tile-blend math |
| `PrismCoreTests.Export` | manifest/ZIP export |
| `PrismCoreTests.Services` | service composition glue — `LocalArtifactStore`, service contract serialization |
| `PrismCoreTests` (root) | end-to-end: `PipelineIntegrationTests` + shared `PipelineFixture` (runs the full pipeline once per request shape and caches results) |

Filters use substring matching, so `~PrismCoreTests.Match` also matches `PrismCoreTests.Matching…` if such a namespace is ever added — keep new suite names non-prefixing.

## Decision: one `.csproj`, not one per service (2026-07-10)

Closed from `jb/src/core/Services/jbtodo.md` ("Establish automated test suites for PRISM, organized as independently runnable suites per service"):

- **Accepted:** per-service suites as namespaces inside the single `Prism.Core.Tests` project, each independently runnable via `--filter`. Coverage gaps closed the same day: new `Ingest/` suite (import IO paths, previously untested) and direct `LocalArtifactStore` tests (T-3200).
- **Rejected for now:** physically splitting into one test `.csproj` per service. The multi-project overhead only pays off once services actually deploy independently; that split is deliberately deferred to T-3300 step 4 (after distributed correctness of the `Prism.ServiceHost`/`Http*Service` seam is proven). Do not split speculatively.
- CI (`.github/workflows/ci.yml`) runs the full project in one `dotnet test` invocation — integration tests included, cheap because `PipelineFixture` shares pipeline runs. End-to-end validation additionally runs via `pwsh test/ci/Invoke-CiPipeline.ps1` (CiMini smoke in CI).

## Conventions

- New tests for a service go in that service's folder with namespace `PrismCoreTests.<Folder>` — never `Prism.Core.Tests.*` (breaks the filter convention; this was a real bug fixed in T-3700's audit).
- Tests needing a full `PrismService` share `PipelineFixture` via `IClassFixture<PipelineFixture>` instead of constructing their own (avoids reloading the CLIP/YOLO models).
- The Ingest suite generates image fixtures as seeded noise at runtime (`ImporterFixture`) instead of committing binaries; noise defeats compression so files clear the configured byte minimums.
- Network-free URL testing: fetcher policy failures are asserted against the shipped `HostRules.json` (validation precedes any request); download success/error paths run against a raw-socket `LoopbackHttpServer` on 127.0.0.1 with permissive test rules — never external hosts.
