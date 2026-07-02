# Services Todo

-------
- [ ] Establish automated test suites for PRISM, organized as independently runnable suites per service.
  - Impact:
    - High - Without independently runnable per-service suites, verifying a change to one stage (Import/Match/Transform/...) requires running the whole batch pipeline by hand against real image sets, which is slow, non-repeatable, and doesn't scale as services move toward independent deployability under the approved microservices split.
    - Effect on other TODOs: tickets like T-3000/T-3100 currently fall back to "validation is by running the pipeline" for acceptance — this todo, once resolved, lets future tickets cite real automated regression coverage instead.
  - Industry standard:
    Each independently deployable service gets its own test project that builds and runs without pulling in the other services' dependencies, so a change to one service can be verified in isolation and suites can run in parallel in CI. Shared end-to-end tests that exercise the whole pipeline stay in one separate top-level suite.
  - Recommended solution:
    Reorganize `jb/src/tests/Prism.Core.Tests` (already covers most stages at unit level) into one test project per service boundary already defined by the `I*Service` interfaces in `jb/src/core/Services/`, each independently runnable via `dotnet test` against just that project. Keep one separate top-level integration suite (building on the existing `PipelineIntegrationTests.cs`) for full end-to-end runs. Reuse the existing xUnit setup already in `Prism.Core.Tests.csproj` — no new test framework or CI infrastructure beyond what's needed to make suites independently runnable.
  - Answer:
    Proposed triage (pending approval, from existing data — repo layout + `.github/workflows/ci.yml`): most of the reorg's design is already present; only the physical project split is missing.
      - The service-boundary interface set the split would follow already exists: `IIngestService`, `IClassificationService`, `IFeatureAnalysisService`, `IImageNgpService`, `IMatchingService`, `ITransformService`, `IUpscaleService`, `IGenerateService` (+ `IArtifactStore`) in `jb/src/core/Services/`.
      - `Prism.Core.Tests` is already partitioned by stage as *folders* (`Classify/ Excel/ Export/ Generate/ ImageNGP/ Match/ Order/`) — same boundaries, but one `.csproj`, so they can't yet be run/deployed in isolation.
      - The top-level end-to-end suite the recommendation wants to keep separate already exists as `PipelineIntegrationTests.cs`, and CI already enforces the unit/integration split by name filter (`--filter "FullyQualifiedName!~PipelineIntegrationTests"` in the unit gate; end-to-end is the separate CiMini smoke step).
      - So the residual work is mechanical: promote each stage folder to its own `.csproj` referencing the same xUnit setup, and lift `PipelineIntegrationTests.cs` into a standalone integration project. Boundary definition, framework choice, and the CI split are already settled by existing artifacts — no new decision needed. Left open for user: whether per-service isolation is worth the multi-project overhead now vs. deferring until services actually deploy independently.
