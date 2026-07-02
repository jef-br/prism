# PRISM Agent Tickets

Main thread is the orchestrator: owns ticket status, final integration, conflict resolution, and user-facing summaries.

## Team Rules

- Do not revert or overwrite edits made by other agents.
- Stay inside the ownership and write scope stated on your ticket.
- Read `jb/docs/PRISM-index.md` first; load only docs relevant to the ticket.
- Preserve pipeline order: Imported → Classified → Matched → Ordered → Renamed → Generated → Transformed → Exported.
- Do not advance past a milestone until its gate is documented and passed.
- Unresolved product decisions stay in folder-local `jbtodo.md` files — do not guess policy.

## Agent Reporting Protocol

- Report: ticket ID, changed files, commands run, pass/fail results, blockers, assumptions, next ticket.
- If blocked: stop, ask the orchestrator one targeted question — never ask the user directly.
- If work is found outside ticket scope: report a follow-up ticket, do not edit out of scope.
- Do not self-start the next ticket; orchestrator reviews completed work first.

## Orchestrator Handoff Protocol

- Satisfactory → mark `Done`.
- Incomplete but salvageable → correction to same agent or follow-up ticket.
- Missing product intent → ask user, then unblock agent.
- Milestone gates are authoritative: later tickets stay blocked until the gate passes.

## Ticket Format

Status: Ready, Blocked, Active, Review, Done. Agent type: `explorer`, `worker`, or orchestrator.

## Runtime Profiles

| Profile | Model | Use |
|---|---|---|
| `P0-orchestrator` | parent/default | Main thread, integration, conflict resolution, milestone decisions |
| `P1-feature-worker` | parent/default | Primary implementation tickets |
| `P2-verifier` | haiku | Smoke-test agents — run commands, inspect results, report blockers |
| `P3-scout` | haiku | Read-only exploration, architecture maps, dependency checks |
| `P4-critical-architecture` | parent/default | Cross-cutting contracts or pipeline architecture |

## Milestone Gates

| Milestone | Feature area | Gate condition |
|---|---|---|
| M5 Classification Groundwork | ImageNGP taxonomy + rule correctness | All Classify `jbtodo.md` decisions answered; ONNX session migrated to singleton ✅ (done 2026-06-29) |
| M6 Human & Model Detection | `hero-is-human`, `contains-mannequin`, `has-human`, `head-visible`, `face-visible` | On-model and ghost phenotypes (`front-on-model-*`, `ghost-front/back/side`) fire correctly on labeled images |
| M7 Orientation & Pose | `hero-orientation`, `pose-type`, `camera-angle`, `top-view` | Packshot orientation-split phenotypes (`front-packshot`, `back-packshot`, `side-packshot`) fire from real signal |
| M8 Product & Packaging | `packaging-visible`, `product-type-label`, `multiple-products` | packshot phenotypes fire from CLIP; `packaging-visible` no longer always UNKNOWN |
| M9 Composition & Spatial | `product-coverage-ratio`, `image-occupancy`, `salient-bbox`, `vertical-centering`, `horizontal-centering` | Composition phenotypes measured; overflow slot assignment accuracy confirmed |
| M10 Semantic & Content | `text-present`, `logo-present`, `dominant-colors`, `lighting` | Content features populated; transform routing that depends on them verified |
| M11 Production Validation | All 26 phenotypes | < 5% misassignment on labeled validation set; no systematic error on any single phenotype |

## Tickets


### T-2600 · M5 Classify groundwork
**Status:** Blocked | **Profile:** P0-orchestrator  
**Blocked-by:** M5 milestone gate — all Classify `jbtodo.md` decisions must be answered first.

Tracks the five open items in `jb/src/core/Images/Classify/jbtodo.md`:
1. Gate phenotypes (bypass flag — stays open until phenotypes validated).
2. Confirm ImageNGP taxonomy: `ImageNGP.json` ↔ `imagePhenotypes.md` ↔ `ImageRoles.json` agree on 26 phenotypes and their IF combinations.
3. Resolve `illustration-technical-drawing` scope (option (b) = null/no-phenotype recommended).
4. Replace `RecordUnknownFeatures()` stub with real CLIP measurements (after taxonomy + prompts are settled).
5. Phenotype production validation: labeled set, confusion matrix, <5% misassignment rate across 26 phenotypes.

M5 gate condition: all Classify decisions answered; ONNX session migrated to singleton.

**Files:** `jb/src/core/Images/Classify/jbtodo.md`, `jb/src/core/Images/Classify/ImageFeatureAnalyzer.cs`

---


### T-2800 · API/in-process pipeline never initializes the GPU Real-ESRGAN upscaler
**Status:** Ready | **Profile:** P1-feature-worker
**Found by:** self-hosted CI `full-pipeline` smoke (2026-07-02).

**Problem:** The in-process pipeline built by `PipelineServiceFactory.CreateInProcess` / `CreateFromEnvironment` (used by the API and WPF) never calls `UpscaleService.Create()`, which is the **only** code path that invokes `Upscaler_g_p_u.Initialize()`. That init happens solely in the ServiceHost ([jb/src/services/Prism.ServiceHost/Program.cs:30](jb/src/services/Prism.ServiceHost/Program.cs#L30)). Transform runs in-process through `ImagePreProcessor` ([jb/src/core/Images/ImagePreProcessor.cs:232](jb/src/core/Images/ImagePreProcessor.cs#L232) → `ImageUpscaler.Upscale`). On a machine where `ImageUpscaler.IsGpuAvailable` is true, `ImageUpscaler.Upscale` routes to `Upscaler_g_p_u.Upscale → RunRealEsrgan`, which throws `"Upscaler_g_p_u.Initialize() must be called before RunRealEsrgan."` ([Upscaler_g_p_u.cs:50-53](jb/src/core/Images/Upscale/Upscaler_g_p_u.cs#L50-L53)). Any full job that needs to **upscale** a below-minimum image aborts the whole pipeline → `PrismService.BuildFailedResult` returns `Status="Failed"` with an empty manifest.

**Evidence:** CI Full run on the committed CiMini fixture (small images, e.g. `CARDIGAN_MAGENTA76_DETAIL.jpg` 230 KB, needing upscale to the 800 px output minimum) fails with `RouteSummaries = ["Pipeline failed: Upscaler_g_p_u.Initialize() must be called before RunRealEsrgan."]`. Match-only (transform off) passes. Only triggers when a GPU/DirectML adapter is present.

**What to do:**
1. Initialize the GPU upscaler once in the in-process/API path — e.g. call `UpscaleService.Create(configuration)` (or `Upscaler_g_p_u.Initialize(modelPath)` directly) at API/PrismService startup so `ImageUpscaler.Upscale` has a live session when a GPU is available. Choose a clean seam (PipelineServiceFactory, PrismService init, or `Prism.Api` startup).
2. Resolve the model path from config (`configuration.UpscaleModelPath`, now config-driven) via `FindModelAsset` / `PRISM_ONNX_MODEL_DIR`.
3. Verify the CPU fallback (`Upscaler_c_p_u`) still used when no GPU is present (that path works today).
4. Consider degrading gracefully (fall back to CPU or skip upscale) if the GPU model can't initialize, rather than aborting the whole job.

**Acceptance:**
- Full pipeline on CiMini completes on a GPU machine (no "Initialize() must be called" abort); `expected-manifest.json` can be captured and CI `full-pipeline.yml` goes green.
- `dotnet build jb/src/PRISM.sln` clean; existing tests green; CPU-only machines unaffected.

**Files:** `jb/src/core/Services/PipelineServiceFactory.cs`, `jb/src/core/PrismService.cs`, `jb/src/core/Services/UpscaleService.cs`, `jb/src/core/Images/ImageUpscaler.cs`, `jb/src/core/Images/Upscale/Upscaler_g_p_u.cs`, `jb/src/api/` startup.

---


### T-2810 · PipelineIntegrationTests hard-depend on an uncommitted dataset
**Status:** Active — paths repointed to CiMini; green blocked by [[T-2800]] | **Profile:** P1-feature-worker
**Found by:** self-hosted CI setup (2026-07-02).

**Problem:** `PipelineIntegrationTests` ([jb/src/tests/Prism.Core.Tests/PipelineIntegrationTests.cs](jb/src/tests/Prism.Core.Tests/PipelineIntegrationTests.cs)) resolved fixtures via a broken `ResolveTestFixturePath()` (looked for a non-existent `jb/Testing`, then a hardcoded `c:\Users\JefB\...` path) and asserted on `SPACINI29/TINY` + `SmallTest/*`, none of which exist on a fresh checkout, so the tests **failed** (not skipped) with `DirectoryNotFoundException` — including CI. As a workaround, `ci.yml` currently excludes the whole class via `--filter "FullyQualifiedName!~PipelineIntegrationTests"`, so this real end-to-end coverage does not run in CI.

**Done (option a, 2026-07-02):** Rewrote `ResolveTestFixturePath()` to walk up to `test/datasets` keyed by the committed `CiMini` folder (no hardcoded path — resolves on the CI runner). Repointed all fixture references (`SPACINI29/TINY`, `SPACINI29-INPUTS.xlsx`, `SmallTest/*`) to CiMini (`test/datasets/CiMini` + `ci-mini.xlsx`). Verified: the tests now load CiMini and run the full pipeline (no more `DirectoryNotFound`; failures shifted to the [[T-2800]] upscaler crash).

**Blocked on [[T-2800]]:** these E2E tests run with `Transform=true`, so on a GPU machine they hit the T-2800 upscaler crash → `Status="Failed"` (9/12 fail on `Assert.Equal("Completed", …)`). The transform issue is deliberately left untouched, so the tests are **not yet green** and the CI `--filter` exclusion **stays in place**.

**Remaining (after T-2800):** confirm the repointed tests pass end-to-end on CiMini, then remove the `--filter "FullyQualifiedName!~PipelineIntegrationTests"` exclusion from `.github/workflows/ci.yml`.

**Files:** `jb/src/tests/Prism.Core.Tests/PipelineIntegrationTests.cs`, `.github/workflows/ci.yml`.

---


## Verification Rules

- After project/solution setup: `dotnet build jb/src/PRISM.sln`, API/WPF run smoke, web `npm run typecheck` + `npm run build`.
- After each milestone: run relevant smoke and record result in milestone table.
- After todo/doc sync: `rg --files -g 'jbtodo.md' jb/src` and `rg -n "^- \[ \]" jb/src`.
- Before advancing milestones: `git status --short` to confirm edits stayed inside assigned ownership.
