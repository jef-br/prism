# Daily Brief

##### Changed
- Self-hosted CI merged (PR #3, commit afeb3c0). `ci.yml` PR gate: Release build → xUnit unit tests (excludes `PipelineIntegrationTests`) → web typecheck+build → match-only CiMini smoke. `full-pipeline.yml`: daily 10:30 Europe/Brussels (DST-aware) + manual full run.
- CiMini fixture dataset committed under `test/datasets/CiMini/` (the only in-repo dataset) with `expected-match.json` golden; `Invoke-CiPipeline.ps1` golden-assertion harness added; `Submit-PrismJob`/`Wait-PrismResult` exported from `PrismJobRunner.psm1`.
- 
- ONNX model paths moved out of hard-coded literals into `Prism_Config.json` (new `Models` section) via `PrismConfiguration`; `ClassificationService`/`UpscaleService` repointed. Stale `Run_TinyTest.ps1` dataset path fixed.

- Classify taxonomy todo has since been FROZEN by you ("captured in canonical files, no reconciliation action needed") — this supersedes last brief's pending name-level verification for that item.

##### Todo updates
- Services test-suites todo (`jb/src/core/Services/jbtodo.md`) — added a proposed-triage note (pending approval, existing data only): the split's design is already present, only the physical project split is missing. Grounded in `.github/workflows/ci.yml` + repo layout — the `I*Service` boundary set exists, `Prism.Core.Tests` is already partitioned by stage folders under one `.csproj`, `PipelineIntegrationTests.cs` is the top-level e2e suite, and CI already enforces the unit/integration split by name filter. Residual work is mechanical (promote folders to per-service `.csproj`). Why safe: no invention, no course change, matches the existing pending-approval pattern.
- Everything else unimproved: Transform DetailCropper/HeadCutter (T-2200) need product decisions; Classify per-feature analyzers mostly need new `ClipPrompts.json` entries or triage approval; Generate + phenotype-validation todos FROZEN. Nothing improvable without guessing.

##### Next steps
- Approve or reject the Services test-suites triage: decide whether per-service project split is worth the multi-project overhead now vs. deferring until services deploy independently.
  
- Still open from last brief: decide whether the match-only / `MatchLite` route needs a ticket so the root `jbtodo.md` "matchingservice public" line can close.
