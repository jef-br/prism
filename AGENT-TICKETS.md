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
**Status:** Done | **Profile:** P1-feature-worker
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

**Done (2026-07-02):** Fixed at the named seam — `PipelineServiceFactory.CreateInProcess`/`CreateFromEnvironment` now call `UpscaleService.Create(configuration)` once (mirrors the `MatchingService`/CLIP eager-init already in the same two methods), catching `PrismConfigurationException` so a missing model asset degrades to CPU instead of blocking pipeline construction. `Upscaler_g_p_u.Initialize` is now idempotent, thread-safe (`_sessionLock`, also serializes `session.Run()` across concurrent jobs — same DML constraint `MatchingService._clipLock` exists for), and non-throwing (new `IsReady` flag, mirrors `ImageClassifier`'s graceful-degradation contract exactly). `ImageUpscaler.Upscale` now routes to GPU only when both hardware is present *and* the session loaded.

Fixing the crash exposed a second, previously-unreachable bug: the committed `Real-ESRGAN_x2plus.onnx` has a fixed `[1,3,64,64]` input, but the code fed it whole images untiled, so real inference still failed with an ONNX shape-mismatch error. Added overlapping-tile inference (`RunTiled`/`RunSingleTile` in `Upscaler_g_p_u.cs`): images are split into tiles sized to the model's fixed input shape (queried from `session.InputMetadata` — falls back to one tile covering the whole image if a future model export has a dynamic shape), each tile is run through the session, and only each tile's trusted center region (discarding an 8px border affected by convolution edge effects) is stitched into the final result.

Verified: `dotnet build jb/src/PRISM.sln -c Release` clean. Full test suite 224/224 passing (was 9 failing pre-fix — all `PipelineIntegrationTests`, see [[T-2810]]); added `Upscaler_g_p_uTests.cs` (4 tests) for the new idempotency/graceful-degradation contract. Live end-to-end: started `Prism.Api`, ran `pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` against the real API — pipeline completes with real GPU-tiled Real-ESRGAN output, 12/14 images `Ok`, 2 `Ko` matching the pre-existing, already-trusted `expected-match.json` baseline (unrelated pre-existing unmatched-image cases, not caused by this fix).

`expected-manifest.json` was captured but **not committed** — 3 consecutive runs of the same unchanged build produced 3 different det-slot assignments for images tied within a family, so it isn't safe as a stable CI golden file yet. Filed as [[T-2820]] (non-determinism) and [[T-2830]] (det-slot numbering starts at det8, not the documented det0) — both must be resolved before recapturing.

**Files:** `jb/src/core/Services/PipelineServiceFactory.cs`, `jb/src/core/Images/ImageUpscaler.cs`, `jb/src/core/Images/Upscale/Upscaler_g_p_u.cs`, `jb/src/tests/Prism.Core.Tests/Upscaler_g_p_uTests.cs`.

---


### T-2810 · PipelineIntegrationTests hard-depend on an uncommitted dataset
**Status:** Done | **Profile:** P1-feature-worker
**Found by:** self-hosted CI setup (2026-07-02).

**Problem:** `PipelineIntegrationTests` ([jb/src/tests/Prism.Core.Tests/PipelineIntegrationTests.cs](jb/src/tests/Prism.Core.Tests/PipelineIntegrationTests.cs)) resolved fixtures via a broken `ResolveTestFixturePath()` (looked for a non-existent `jb/Testing`, then a hardcoded `c:\Users\JefB\...` path) and asserted on `SPACINI29/TINY` + `SmallTest/*`, none of which exist on a fresh checkout, so the tests **failed** (not skipped) with `DirectoryNotFoundException` — including CI. As a workaround, `ci.yml` currently excludes the whole class via `--filter "FullyQualifiedName!~PipelineIntegrationTests"`, so this real end-to-end coverage does not run in CI.

**Done (option a, 2026-07-02):** Rewrote `ResolveTestFixturePath()` to walk up to `test/datasets` keyed by the committed `CiMini` folder (no hardcoded path — resolves on the CI runner). Repointed all fixture references (`SPACINI29/TINY`, `SPACINI29-INPUTS.xlsx`, `SmallTest/*`) to CiMini (`test/datasets/CiMini` + `ci-mini.xlsx`). Verified: the tests now load CiMini and run the full pipeline (no more `DirectoryNotFound`; failures shifted to the [[T-2800]] upscaler crash).

**Blocked on [[T-2800]]:** these E2E tests run with `Transform=true`, so on a GPU machine they hit the T-2800 upscaler crash → `Status="Failed"` (9/12 fail on `Assert.Equal("Completed", …)`). The transform issue is deliberately left untouched. The CI `--filter` exclusion has been **removed** — CI now runs these tests and reports **red** until T-2800 is fixed. A failing test honestly surfaces the bug; hiding it behind a filter was the wrong call and was reverted.

**Done (2026-07-02):** Confirmed post-[[T-2800]] — full test suite is 224/224 green, including all 12 `PipelineIntegrationTests` methods with `Transform=true` against the real CiMini fixture. CI `--filter` exclusion stays removed.

**Files:** `jb/src/tests/Prism.Core.Tests/PipelineIntegrationTests.cs`, `.github/workflows/ci.yml`.

---


### T-2820 · Ordered stage assigns non-deterministic det-slots for tied images within a family
**Status:** Ready | **Profile:** P1-feature-worker
**Found by:** [[T-2800]] end-to-end verification (2026-07-02).

**Problem:** Running `pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` three times back-to-back against the same unchanged build produced three different det-slot assignments for images tied within a family. Family `94613033` (`Pareo Exotica.jpg`, `Pareo_exotica_F1.jpg`, `Pareo_exotica_F2.jpg`) assigned `Pareo_exotica_F1.jpg` to det10, then det8, then det9 across three consecutive runs with no code change in between; family `90861083` (`23211008_02_A.jpg`/`_B.jpg`) flip-flopped between det8/det9. This makes any `expected-manifest.json` golden-file test unsafe for a family with more than one image sharing the same ImageRole/precedence tier, since there is no single correct "expected" det-slot to pin.

**Evidence:** 3 consecutive runs (2026-07-02), same build, same input, same API process — det-slot assignment for tied images changed every run. The `Ordered` stage runs before `Transformed` in the immutable pipeline order (Imported → Classified → Matched → **Ordered** → Renamed → Generated → **Transformed** → Exported), which rules out [[T-2800]]'s Transform/Upscale fix as the cause — `Ordered` output cannot depend on a later stage's behavior or timing.

**Root cause (untriaged):** `MatchingService.BuildLambda`'s `Parallel.For` results are explicitly re-aggregated in original input order ("Aggregate into ordered collections (single-threaded; preserves input order for deterministic matching)"), so `LambdaRecords` itself is deterministic. The non-determinism most likely enters via `ImageOrderer.Run` (`jb/src/core/Images/ImageOrderer.cs`) or upstream CLIP classification confidence — if GPU/DirectML inference has run-to-run floating-point variance for near-identical images, and the det-slot ranking for same-role candidates doesn't fall back to a fully deterministic secondary key (e.g. filename or original list order) when scores are equal/near-equal, ties resolve arbitrarily.

**What to do:**
1. Read `ImageOrderer.Run` and `jb/src/core/Images/Order/*.cs` (`DetSlotRule.cs`, `CandidateDetOrder.cs`, `DetOrderConfig.cs`) to find where same-role candidates are ranked/tie-broken.
2. Confirm or rule out CLIP/GPU floating-point non-determinism as the trigger.
3. Add a fully deterministic secondary/tertiary tie-break so equal/near-equal candidates always resolve the same way, every run.
4. Re-run `Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` at least 5x consecutively and confirm identical det-slot assignment every time before recapturing `expected-manifest.json`.

**Acceptance:**
- 5 consecutive `-Mode Full -Dataset CiMini` runs (no code change between them) produce byte-identical `FinalFileName`/`DetOrder` for every image.
- `expected-manifest.json` can be captured once and trusted as a stable golden file.

**Files:** `jb/src/core/Images/ImageOrderer.cs`, `jb/src/core/Images/Order/*.cs`, `jb/src/core/Services/MatchingService.cs` (if root-caused to classification confidence).

---


### T-2830 · `_det#` numbering starts at det8 instead of the documented zero-based det0
**Status:** Ready | **Profile:** P1-feature-worker
**Found by:** [[T-2800]] end-to-end verification (2026-07-02).

**Problem:** CLAUDE.md's domain vocabulary states: "`_det#` — Zero-based image ordering suffix within a FamilyID (e.g. `_det0`, `_det1`)." The captured Full-mode CiMini manifest (2026-07-02) instead showed every family's first image landing on `_det8` — e.g. family `90861025` → `90861025_det8.jpg`, family `90861026` → `90861026_det8.jpg`, family `94613033`'s three images → det8/det9/det10. No family in the fixture ever produced `_det0` through `_det7`. This strongly suggests `DetOrderRules.json`'s per-product-type slot list is indexed against some fixed ordered list of ImageRoles, and slot 8 happens to be the first role CiMini's images actually match, rather than det numbering restarting at 0 per family as documented.

**Current vs. target behavior:**
- Current: the first assigned image in a family gets `_det8` (or higher); no image is ever `_det0`–`_det7`.
- Target (per CLAUDE.md domain vocabulary): det-slot numbering is zero-based *per family* — the first image in any family's det order should be `_det0`.

**What to do:**
1. Read `DetOrderRules.json` (`jb/src/core/Images/Order/`) and `ImageOrderer.Run`/`DetSlotRule.cs`/`CandidateDetOrder.cs` to find where the numeric det index is derived from ImageRole precedence.
2. Determine whether this is a genuine off-by-N indexing bug (e.g. enumerating a role list that includes roles never present in CiMini, with matched roles landing at index 8+) or a deliberate-but-undocumented convention — resolve via `jb/docs/PRISM-order-rename.md` (documented owner of `_det#`/ordering rules) and the `jbtodo.md` process if intent isn't already decided.
3. Fix the indexing (or correct the documentation, whichever is actually wrong) so det numbering matches the agreed convention.
4. Sequence after [[T-2820]] — recapturing `expected-manifest.json` to verify this fix needs deterministic det-slot assignment first.

**Acceptance:**
- First image in every family's det order is `_det0` (or CLAUDE.md's vocabulary is corrected to match the actual intended behavior, if that's the real resolution) — confirmed on CiMini.
- `jb/docs/PRISM-order-rename.md` and CLAUDE.md agree with implemented behavior.

**Files:** `jb/src/core/Images/Order/DetOrderRules.json`, `jb/src/core/Images/ImageOrderer.cs`, `jb/src/core/Images/Order/*.cs`, `jb/docs/PRISM-order-rename.md`, `CLAUDE.md`.

---


## Verification Rules

- After project/solution setup: `dotnet build jb/src/PRISM.sln`, API/WPF run smoke, web `npm run typecheck` + `npm run build`.
- After each milestone: run relevant smoke and record result in milestone table.
- After todo/doc sync: `rg --files -g 'jbtodo.md' jb/src` and `rg -n "^- \[ \]" jb/src`.
- Before advancing milestones: `git status --short` to confirm edits stayed inside assigned ownership.
