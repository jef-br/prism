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

- `P1-feature-worker` / `P4-critical-architecture` tickets: spawn the reviewer agent on the completed diff and record its verdict on the ticket block as `**Review:** Approve|Request Changes (YYYY-MM-DD)`. Only `Approve` makes the ticket eligible for `Done` — /ticket-finish enforces this and will refuse without it.
- `P0`/`P2`/`P3` tickets: orchestrator judgment suffices → mark `Done`.
- Incomplete but salvageable → correction to same agent or follow-up ticket.
- Missing product intent → ask user, then unblock agent.
- Milestone gates are authoritative: later tickets stay blocked until the gate passes.

## Ticket Format

Status: Ready, Blocked, Active, Review, Done. Agent type: `explorer`, `worker`, or orchestrator.
P1/P4 tickets carry a `**Review:** <verdict> (YYYY-MM-DD)` line once reviewed; `Approve` is required before Done.
Done tickets are moved to `AGENT-TICKETS-archive.md` (via /ticket-finish) — this file holds open tickets only.

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

Tracks the five open items in `jb/src/core/Services/Matching/Classify/jbtodo.md`:
1. Gate phenotypes (bypass flag — stays open until phenotypes validated).
2. Confirm ImageNGP taxonomy: `ImageNGP.json` ↔ `imagePhenotypes.md` ↔ `ImageRoles.json` agree on 26 phenotypes and their IF combinations.
3. Resolve `illustration-technical-drawing` scope (option (b) = null/no-phenotype recommended).
4. Replace `RecordUnknownFeatures()` stub with real CLIP measurements (after taxonomy + prompts are settled).
5. Phenotype production validation: labeled set, confusion matrix, <5% misassignment rate across 26 phenotypes.
6. Per-feature CLIP confidence calibration (carried over from the 2026-07-04 input_ids fix): influential-tag bars are per feature in `Prism_Config.json` `Classification.Confidence_Thresholds`, but orientation/head prompts rarely clear their bar on real batches — 2026-07-10 full-pipeline test saw 2 of 25 images get a phenotype because `hero-orientation` stayed UNKNOWN everywhere. Calibrate (e.g. per-feature softmax over the prompt group) before item 5's validation makes sense.

M5 gate condition: all Classify decisions answered; ONNX session migrated to singleton.

**Files:** `jb/src/core/Services/Matching/Classify/jbtodo.md`, `jb/src/core/Services/Matching/Classify/ImageFeatureAnalyzer.cs`

---


### T-2820 · Ordered stage assigns non-deterministic det-slots for tied images within a family
**Status:** Ready | **Profile:** P1-feature-worker
**Found by:** [[T-2800]] end-to-end verification (2026-07-02).

**Problem:** Running `pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` three times back-to-back against the same unchanged build produced three different det-slot assignments for images tied within a family. Family `94613033` (`Pareo Exotica.jpg`, `Pareo_exotica_F1.jpg`, `Pareo_exotica_F2.jpg`) assigned `Pareo_exotica_F1.jpg` to det10, then det8, then det9 across three consecutive runs with no code change in between; family `90861083` (`23211008_02_A.jpg`/`_B.jpg`) flip-flopped between det8/det9. This makes any `expected-manifest.json` golden-file test unsafe for a family with more than one image sharing the same ImageRole/precedence tier, since there is no single correct "expected" det-slot to pin.

**Evidence:** 3 consecutive runs (2026-07-02), same build, same input, same API process — det-slot assignment for tied images changed every run. The `Ordered` stage runs before `Transformed` in the immutable pipeline order (Imported → Classified → Matched → **Ordered** → Renamed → Generated → **Transformed** → Exported), which rules out [[T-2800]]'s Transform/Upscale fix as the cause — `Ordered` output cannot depend on a later stage's behavior or timing.

**Root cause (untriaged):** `MatchingService.BuildLambda`'s `Parallel.For` results are explicitly re-aggregated in original input order ("Aggregate into ordered collections (single-threaded; preserves input order for deterministic matching)"), so `LambdaRecords` itself is deterministic. The non-determinism most likely enters via `ImageOrderer.Run` (`jb/src/core/Services/Matching/ImageOrderer.cs`) or upstream CLIP classification confidence — if GPU/DirectML inference has run-to-run floating-point variance for near-identical images, and the det-slot ranking for same-role candidates doesn't fall back to a fully deterministic secondary key (e.g. filename or original list order) when scores are equal/near-equal, ties resolve arbitrarily.

**What to do:**
1. Read `ImageOrderer.Run` and `jb/src/core/Services/Matching/Order/*.cs` (`DetSlotRule.cs`, `CandidateDetOrder.cs`, `DetOrderConfig.cs`) to find where same-role candidates are ranked/tie-broken.
2. Confirm or rule out CLIP/GPU floating-point non-determinism as the trigger.
3. Add a fully deterministic secondary/tertiary tie-break so equal/near-equal candidates always resolve the same way, every run.
4. Re-run `Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` at least 5x consecutively and confirm identical det-slot assignment every time before recapturing `expected-manifest.json`.

**Acceptance:**
- 5 consecutive `-Mode Full -Dataset CiMini` runs (no code change between them) produce byte-identical `FinalFileName`/`DetOrder` for every image.
- `expected-manifest.json` can be captured once and trusted as a stable golden file.

**Files:** `jb/src/core/Services/Matching/ImageOrderer.cs`, `jb/src/core/Services/Matching/Order/*.cs`, `jb/src/core/Services/Matching/MatchingService.cs` (if root-caused to classification confidence).

---


### T-2830 · `_det#` numbering starts at det8 instead of the documented zero-based det0
**Status:** Ready | **Profile:** P1-feature-worker
**Found by:** [[T-2800]] end-to-end verification (2026-07-02).

**Problem:** CLAUDE.md's domain vocabulary states: "`_det#` — Zero-based image ordering suffix within a FamilyID (e.g. `_det0`, `_det1`)." The captured Full-mode CiMini manifest (2026-07-02) instead showed every family's first image landing on `_det8` — e.g. family `90861025` → `90861025_det8.jpg`, family `90861026` → `90861026_det8.jpg`, family `94613033`'s three images → det8/det9/det10. No family in the fixture ever produced `_det0` through `_det7`. This strongly suggests `DetOrderRules.json`'s per-product-type slot list is indexed against some fixed ordered list of ImageRoles, and slot 8 happens to be the first role CiMini's images actually match, rather than det numbering restarting at 0 per family as documented.

**Current vs. target behavior:**
- Current: the first assigned image in a family gets `_det8` (or higher); no image is ever `_det0`–`_det7`.
- Target (per CLAUDE.md domain vocabulary): det-slot numbering is zero-based *per family* — the first image in any family's det order should be `_det0`.

**What to do:**
1. Read `DetOrderRules.json` (`jb/src/core/config/`) and `ImageOrderer.Run`/`DetSlotRule.cs`/`CandidateDetOrder.cs` (`jb/src/core/Services/Matching/Order/`) to find where the numeric det index is derived from ImageRole precedence.
2. Determine whether this is a genuine off-by-N indexing bug (e.g. enumerating a role list that includes roles never present in CiMini, with matched roles landing at index 8+) or a deliberate-but-undocumented convention — resolve via `jb/docs/PRISM-order-rename.md` (documented owner of `_det#`/ordering rules) and the `jbtodo.md` process if intent isn't already decided.
3. Fix the indexing (or correct the documentation, whichever is actually wrong) so det numbering matches the agreed convention.
4. Sequence after [[T-2820]] — recapturing `expected-manifest.json` to verify this fix needs deterministic det-slot assignment first.

**Acceptance:**
- First image in every family's det order is `_det0` (or CLAUDE.md's vocabulary is corrected to match the actual intended behavior, if that's the real resolution) — confirmed on CiMini.
- `jb/docs/PRISM-order-rename.md` and CLAUDE.md agree with implemented behavior.

**Files:** `jb/src/core/config/DetOrderRules.json`, `jb/src/core/Services/Matching/ImageOrderer.cs`, `jb/src/core/Services/Matching/Order/*.cs`, `jb/docs/PRISM-order-rename.md`, `CLAUDE.md`.

---


### T-3300 · Validate and complete the Phase 2 distributed-services seam
**Status:** Ready | **Profile:** P4-critical-architecture
**Tracks:** `jb/src/core/Services/jbtodo.md` (per-service test suite todo, triaged 2026-07-07).

**Problem:** The physical separation of deployables described as "Phase 2" in `PipelineServices.cs` is largely already built, not merely planned:
- `PipelineServiceFactory.CreateFromEnvironment` already swaps any of Ingest/Matching/Generate/Transform for its HTTP client (`Http*Service` in `jb/src/core/Services/Http/`) when `PRISM_INGEST_URL` / `PRISM_MATCHING_URL` / `PRISM_GENERATE_URL` / `PRISM_TRANSFORM_URL` is set.
- `jb/src/services/Prism.ServiceHost/Program.cs` already exposes each service over HTTP independently via `PRISM_SERVICE=ingest|matching|generate|transform|upscale`.

None of this is validated end-to-end:
1. No test exercises the actual HTTP round trip for any `Http*Service` client against `Prism.ServiceHost` — only in-process paths are tested today.
2. No CI job runs PRISM as actually-separate processes (multiple `Prism.ServiceHost` instances + URL env vars wired per service) — `ci.yml`/`full-pipeline.yml` only run the monolith API.

**Correction (2026-07-11):** this ticket previously claimed the API's in-process pipeline never initializes the GPU upscaler, sourced from `test/ci/README.md`. That's stale — `test/ci/README.md` describes a pre-T-2800 state. `PipelineServiceFactory.CreateFromEnvironment` already calls `EnsureUpscalerReady` before constructing `TransformService` on the same path `Pipeline`'s constructor uses (`jb/src/core/Pipeline.cs:26`), and T-2800 (archived Done) confirms this was fixed and verified via a live CiMini Full run. No upscaler-init fix is needed here; `test/ci/README.md`'s "Full run is currently red" section should be corrected separately (out of scope for this ticket).

**What to do:**
1. Add integration tests that stand up a `Prism.ServiceHost` instance (or in-memory `WebApplicationFactory`) per service and exercise each `Http*Service` client against it — real HTTP, not mocked.
2. Add a CI (or scheduled) job that runs the full pipeline with all four service URLs pointed at separate `Prism.ServiceHost` processes, and asserts it produces the same manifest as the in-process run on CiMini.
3. Only once distributed correctness is proven: split `Prism.Core.Tests` into per-service `.csproj` files along the existing namespace boundaries (`Transform/`, `Match/`, `Classify/`, `ImageNGP/`, `Order/`, `Rename/`, `Generate/`, `Export/`, plus [[T-3200]]'s `Ingest/`). This step only pays off once steps 1-2 make Phase 2 real — do not do it speculatively first.

**Acceptance:**
- `-Mode Full -Dataset CiMini` passes both in-process and fully distributed (4 separate `Prism.ServiceHost` processes), producing identical `expected-manifest.json`.
- Each `Http*Service` has at least one real-HTTP-roundtrip test.
- Test projects physically split, mirroring the proven service boundaries.

**Files:** `jb/src/core/Services/PipelineServiceFactory.cs`, `jb/src/core/Services/Http/*.cs`, `jb/src/services/Prism.ServiceHost/Program.cs`, `jb/src/tests/Prism.Core.Tests/*`, `.github/workflows/ci.yml`.

---


### T-3400 · Web workbench: dark mode, layout compaction, import/export feedback
**Status:** Ready | **Profile:** P1-feature-worker
**Tracks:** root `jbtodo.md` — web-workbench refinement, triaged 2026-07-10.

**Problem:** Three-part complaint, confirmed against current code:
1. No dark theme — `PRISM-theme.css` (`jb/src/workbench/web/styles/PRISM-theme.css`) defines only one warm/beige palette (`--prism-color-page: #f3f1eb`, `--prism-color-surface-strong: #f7eadc`), no dark variant.
2. Page-level scrolling drowns the output — `WorkbenchShell.tsx` stacks `UploadSection` → `RouteSection` → `ResultSection` in one vertical column (`workbench-main-column`); the ZIP download button in `ResultSection` sits below the full progress/route detail, so users must scroll past everything to reach it.
3. Import/export stage feedback is generic — `StatusPanel.tsx` shows fixed dev-facing chips ("Empty input", "Loading", "Progress placeholder", "Result placeholder") rather than the real per-stage Import/Export state (accepted/rejected counts, blocked-vs-running) that `PRISM-workbench.md`'s Required Display section already mandates ("image collection/import state", "output preview", "KO records").

Also confirmed: no Upscale control exists in `JobParameterPanel.tsx` today (only rename/transform/generation/ReturnOriginalImages) — the todo's "Upscaling currently not explicitly mentioned is a good thing" is **confirming that omission is intentional**, not requesting it be added. Do not add an Upscale toggle as part of this ticket.

**Scope decision (2026-07-10):** tighten the existing single-column layout — do not restructure into tabs/stepper (bigger rework, more regression surface with no automated test suite). Dark mode: auto (`prefers-color-scheme`) + a manual header toggle that overrides and persists.

**What to do:**
1. Add a dark variable set to `PRISM-theme.css` gated by `@media (prefers-color-scheme: dark)`, plus a `[data-theme="dark"]`/`[data-theme="light"]` override pair driven by a manual toggle. Reuse the existing CSS variable names (`--prism-color-page`, `-surface`, `-surface-strong`, `-ink`, `-muted`, `-line`, etc.) so component CSS doesn't need touching.
2. Add a small theme toggle in `WorkbenchShell.tsx`'s header, persisting the user's explicit choice to `localStorage` and defaulting to `prefers-color-scheme` when no explicit choice is stored.
3. Reorder/compact `WorkbenchShell.tsx`'s main column so Upload + Result (with the download link) are reachable without scrolling past full route detail — e.g. move `ResultSection` above `RouteSection`, or collapse `RouteSection`'s live event list into a fixed-height internally-scrolling panel (workbench.css already uses this pattern — see the `max-height: 240px; overflow: auto;` rule around line 348) rather than letting it grow the page.
4. Replace `StatusPanel.tsx`'s generic "Progress placeholder"/"Result placeholder" chips with real Import/Export-stage-labeled state sourced from the SSE progress events, per `PRISM-workbench.md`'s No-Hidden-Behavior Rule — show whether the job is actively importing/exporting vs. blocked, using the actual stage name from the progress event, not a synthetic label.

**Acceptance:**
- `npm run typecheck` and `npm run build` green in `jb/src/workbench/web`.
- Manual verification: toggle dark mode, confirm all sections legible in both themes; run a job and confirm the download link is reachable without scrolling past the full route/progress detail; confirm `StatusPanel` reflects real stage state during Import/Export, not placeholder text.

**Files:** `jb/src/workbench/web/styles/PRISM-theme.css`, `jb/src/workbench/web/styles/workbench.css`, `jb/src/workbench/web/sections/WorkbenchShell.tsx`, `jb/src/workbench/web/components/StatusPanel.tsx`, `jb/src/workbench/web/sections/ResultSection.tsx`, `jb/src/workbench/web/sections/RouteSection.tsx`.

---


### T-3500 · Fuse Import→Match in-process handoff to remove redundant image decode
**Status:** Ready | **Profile:** P1-feature-worker
**Tracks:** root `jbtodo.md` — Import/Match fusion, triaged 2026-07-10.

**Problem:** `Importer.cs` normalizes each source image and writes it to disk once (`NormalizedJpgPath`, job temp folder). When Matching runs in the same process (today's default in-process mode), `MatchingService.PrepareLambda` (`jb/src/core/Services/Matching/MatchingService.cs:247`) re-reads that same file with `Image.Load<Rgba32>(source.NormalizedJpgPath)` — a second full decode of bytes Import already held in memory moments earlier, for every OK image in the batch.

**Scope decision (2026-07-10):** in-process decode reuse only. `NormalizedJpgPath` stays on disk unchanged — Exporter, KO handling, and the cross-process HTTP contract all still depend on it (see [[T-3600]] for that separate gap). This ticket only removes the redundant decode when Import and Match run in the same process/call.

**What to do:**
1. Extend the Import→Match handoff so the decoded normalized image (or raw normalized bytes) survives past `Importer.cs` into `IngestResult`/`ImageRecord_INPUT` for the in-process path, instead of being decoded, used, and discarded.
2. Update `MatchingService.PrepareLambda` to use the carried-forward image/bytes when present, falling back to `Image.Load(NormalizedJpgPath)` only when absent (i.e., when Matching is invoked without a preceding in-process Import — `HttpMatchingService`/`Prism.ServiceHost`, or any future direct-to-Matching entry point).
3. Confirm the fast-path already-conforming-JPEG case in `Importer.cs` (metadata-only `Image.Identify` + file copy, no full decode) still behaves correctly — that path has no decoded in-memory image to hand forward, so Match still decodes once there, same as today (no regression, just no double-decode to remove).
4. Verify no change to `NormalizedJpgPath`, `NormalizedWidth`/`NormalizedHeight`, or any disk artifact Exporter/KO handling reads — this is an in-memory-only optimization.

**Acceptance:**
- `dotnet build jb/src/PRISM.sln` 0/0.
- `pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` produces an identical `expected-manifest.json` to a pre-change run (no behavioral change, only I/O reduction).
- Spot-check (debug counter or log) confirms decode calls against `NormalizedJpgPath` drop from 2 to 1 per image on the in-process path.

**Files:** `jb/src/core/lib/Ingress/Importer.cs`, `jb/src/core/Models/ImageRecord_INPUT.cs`, `jb/src/core/Services/Matching/MatchingService.cs`, `jb/src/core/Services/IngestResult.cs`.

---


### T-3600 · Matching's HTTP contract silently assumes a shared filesystem with Import
**Status:** Ready | **Profile:** P4-critical-architecture
**Found by:** Import↔Match fusion scoping ([[T-3500]]), 2026-07-10.

**Problem:** `HttpMatchingService.MatchAsync` (`jb/src/core/Services/Http/HttpMatchingService.cs`) POSTs the full `IngestResult` as JSON to a remote Matching host. `IngestResult`'s per-image records carry `NormalizedJpgPath` as an absolute file path string (`jb/src/core/Models/ImageRecord_INPUT.cs:35`) — not bytes. A genuinely separate/public Matching deployment (per the root `jbtodo.md`'s "keep the matching service open to the public" goal) has no way to read that path unless it happens to share a mounted filesystem with whatever Import instance produced it. This is undocumented today — `PRISM-io-import.md` describes the local-temp-folder lifecycle but doesn't flag that the Matching HTTP client/host pair depends on it being shared with Ingest.

**What to do:**
1. Confirm the gap: check whether `Prism.ServiceHost` (`PRISM_SERVICE=matching`) is ever run against a different machine/container than Ingest in any existing deployment path, or whether it's simply untested today (per [[T-3300]], which already flags no CI job runs the services as truly separate processes).
2. Decide and document the fix: either (a) ship normalized image bytes over the wire in the Match request (bigger payload, but makes Matching truly standalone/public), or (b) formally document and enforce a shared-volume requirement between Ingest and Matching deployables (smaller, but contradicts "open to the public" unless the public entry point is different from the internal Ingest→Match handoff).
3. If (a): update `IngestResult`/`ImageRecord_INPUT` serialization, `HttpIngestService`/`HttpMatchingService`, and `PRISM-io-import.md`'s Zip/temp-folder section to describe the new contract.
4. If (b): document the shared-volume requirement explicitly in `PRISM-io-import.md` and `AGENTFEEDBACK.md`, and add a startup check or clear failure mode when `NormalizedJpgPath` isn't readable from the Matching host.

**Acceptance:**
- A documented, deliberate answer to "can Matching run as a truly independent/public service without sharing a filesystem with Ingest" exists in `jb/docs/`.
- Whichever fix is chosen is implemented and covered by [[T-3300]]'s planned real-HTTP-roundtrip tests.

**Files:** `jb/src/core/Services/Http/HttpIngestService.cs`, `jb/src/core/Services/Http/HttpMatchingService.cs`, `jb/src/core/Models/ImageRecord_INPUT.cs`, `jb/docs/PRISM-io-import.md`, `AGENTFEEDBACK.md`.

---


### T-3700 · Align project/assembly names, solution structure, and test namespaces with the Services/ restructure
**Status:** Ready | **Profile:** P4-critical-architecture
**Found by:** namespacing audit, 2026-07-10.

**Problem:** The 2026-07-08 core restructure renamed folders and C# namespaces to `Services/`/`Prism.Services.*` + `lib/`/`Prism.Lib.*`, but several identifiers were never updated, so the same project now answers to 2-3 different names depending on where you look. Confirmed by direct inspection of every `namespace` declaration, every `.csproj`, and `PRISM.sln`:

1. **Test namespace break — a real bug, not cosmetic.** Every other test subfolder follows `PrismCoreTests.<Folder>` (`PrismCoreTests.Export`, `.Order`, `.Match`, `.Transform`, `.Excel`, `.Classify`, `.Generate`, `.ImageNGP`, `.Rename`, `.Services`) — but `jb/src/tests/Prism.Core.Tests/Analyzers/*.cs` (`YoloDetectorTests.cs`, `VisualAnalyzerTests.cs`, `ProductTypeResolverTests.cs`) declare `namespace Prism.Core.Tests.Analyzers;` instead. T-3200's documented per-stage isolation command — `dotnet test --filter "FullyQualifiedName~PrismCoreTests.<Stage>"` — silently matches zero tests for Analyzers today.
2. **Project/assembly identity mismatch.** `Prism.Core.Images.Classify.csproj`, `Prism.Core.Images.Transform.csproj`, `Prism.Core.Images.Upscale.csproj` never had their file name/assembly name updated. Each now has three different names for the same project: assembly `Prism.Core.Images.Transform.dll`, namespace `Prism.Services.Transform`, folder `Services/Transform/Engine/` (same pattern for Classify → `Prism.Services.Matching`, Upscale → `Prism.Services.Upscale`). Neither `<AssemblyName>` nor `<RootNamespace>` is set explicitly in any of the three, so both default to the stale file name.
3. **Stale solution-folder hierarchy.** `PRISM.sln` still nests these three projects under solution folder `core > Images > Transform` / `core > Images > Classify` — a Visual Studio artifact left over from the pre-restructure `jb/src/core/Images/` layout, not the real `Services/` layout.
4. **Upscale invisible in the solution.** `Prism.Core.Images.Upscale.csproj` has no `Project(...)` entry in `PRISM.sln` at all — it only builds because `Prism.Core.csproj` references it directly via `<ProjectReference>`. Its sibling engine projects (Classify, Transform) do have solution entries; Upscale doesn't, for no documented reason.
5. **CLAUDE.md's project list is stale.** The solution-project list in CLAUDE.md's Architecture section names Contracts/Core/Images.Classify/Images.Transform/Api (Workbench.Wpf was removed 2026-07-10 along with the WPF workbench itself), but omits `Prism.Core.Images.Upscale`, `Prism.Core.Tests`, and `Prism.ServiceHost` — all three real and already part of the tree (Tests and ServiceHost even have `PRISM.sln` entries). `jb/docs/PRISM-transform-generate.md` also has one stale example path (`Images/Upscale/ONNX/...` instead of the actual `Services/Upscale/Engine/ONNX/...` used by `Prism_Config.json` and `PrismConfigLocator`).

**Confirmed blast radius:** exactly 6 files repo-wide contain the literal string `Prism.Core.Images` — `Prism.Core.csproj` (3 `<ProjectReference>` paths), `PRISM.sln`, `CLAUDE.md`, `jb/docs/PRISM-transform-generate.md`, plus 2 doc-comments in `Tx_DetailCropper.cs`/`CropTransformSettings.cs` that reference the project name descriptively. No CI workflow, PowerShell script, or test infra hardcodes these names (checked `.github/workflows/*.yml`, `test/ci/`). Model-asset resolution (`PrismConfigLocator.FindModelAsset`, `Prism_Config.json`'s `Models` section, `PrismConfiguration.cs`, `FeatureAnalysisService.cs`) already uses the correct `Services/...` paths — not part of this bug, already fixed in the original restructure. This is a build-graph/text rename with no runtime behavior change.

**What to do:**
1. Fix the test-namespace bug first (smallest, highest-value fix): change `namespace Prism.Core.Tests.Analyzers;` → `namespace PrismCoreTests.Analyzers;` in the 3 files listed above.
2. Rename the three engine `.csproj` files to match their real namespace: `Prism.Core.Images.Classify.csproj` → `Prism.Services.Matching.Classify.csproj`, `Prism.Core.Images.Transform.csproj` → `Prism.Services.Transform.csproj`, `Prism.Core.Images.Upscale.csproj` → `Prism.Services.Upscale.csproj`. Update the 3 `<ProjectReference>` paths in `Prism.Core.csproj` accordingly.
3. Update `PRISM.sln`: rename the 3 project entries to their new names/paths, add the missing Upscale project entry, and replace the stale `Images` solution folder with one that mirrors the real `Services/` layout.
4. Update the 2 doc-comment mentions in `Tx_DetailCropper.cs`/`CropTransformSettings.cs` to the new project name.
5. Update CLAUDE.md's Architecture/Solution project list to name every project actually in the tree (add Upscale, Tests, ServiceHost), and fix the one stale path example in `PRISM-transform-generate.md`.
6. Do **not** touch `Prism.Contracts`-namespaced files that live outside `Models/` (e.g. `OrderEvidence.cs`, `MatchEvidence.cs`, `ImageFeatureSnapshot.cs`) — that cross-folder namespace is deliberate (`Prism.Core.Contracts.csproj` cherry-picks files by relative path regardless of physical location). Don't "fix" these into folder-matching namespaces.

**Verification:**
- `dotnet build jb/src/PRISM.sln` → 0 errors / 0 warnings, same as before the rename.
- `dotnet sln jb/src/PRISM.sln list` shows all real projects, including the 3 renamed ones and the previously-missing Upscale entry.
- Reproduce the bug before fixing it, then confirm the fix: `dotnet test jb/src/tests/Prism.Core.Tests/Prism.Core.Tests.csproj --filter "FullyQualifiedName~PrismCoreTests.Analyzers"` matches 0 tests beforehand and the full Analyzer suite afterward.
- Full existing suite (`dotnet test jb/src/tests/Prism.Core.Tests/Prism.Core.Tests.csproj`) has the same pass count before and after — pure identity rename, nothing should newly pass or fail.
- `git grep -n "Prism.Core.Images"` returns zero hits repo-wide.
- Open `PRISM.sln` (Visual Studio or `dotnet sln list`) and confirm the solution-folder hierarchy matches the physical `Services/`/`lib/` layout — no leftover `Images` grouping.
- `pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` still produces the existing `expected-manifest.json` unchanged (proves the rename didn't alter runtime behavior).

**Files:** `jb/src/core/Services/Matching/Classify/Prism.Core.Images.Classify.csproj`, `jb/src/core/Services/Transform/Engine/Prism.Core.Images.Transform.csproj`, `jb/src/core/Services/Upscale/Engine/Prism.Core.Images.Upscale.csproj`, `jb/src/core/Prism.Core.csproj`, `jb/src/PRISM.sln`, `jb/src/tests/Prism.Core.Tests/Analyzers/YoloDetectorTests.cs`, `jb/src/tests/Prism.Core.Tests/Analyzers/VisualAnalyzerTests.cs`, `jb/src/tests/Prism.Core.Tests/Analyzers/ProductTypeResolverTests.cs`, `jb/src/core/Services/Transform/Engine/Tx_DetailCropper.cs`, `jb/src/core/Services/Transform/Engine/CropTransformSettings.cs`, `CLAUDE.md`, `jb/docs/PRISM-transform-generate.md`.

---


### T-3800 · Match bracket todos: edit-distance gap, substring-rescue perf, totalImageTokens precision
**Status:** Ready | **Profile:** P1-feature-worker
**Tracks:** `jb/src/core/Services/Matching/Match/jbtodo.md` (triaged 2026-07-11).

Tracks three open items, each fully detailed (impact, industry-standard framing, recommended solution) in the source `jbtodo.md`:
1. **StringMatcher edit-distance gap** — `jb/docs/PRISM-match.md` documents typo-tolerant string matching; `StringMatcher.cs` only does exact token matching via an inverted index. Decide whether the doc or the code is wrong; if the code should gain tolerance, reuse the Levenshtein helper already in `jb/src/core/lib/Excel/ModelBuilder.cs` (bounded distance ≤ 1, categorical columns only).
2. **`TryMatchBySubstringRescue` perf** — brute-force substring scan over the digit index, `O(unmatched images × rescue tokens × index size)`, unmeasured. Add a `Stopwatch` measurement against a representative/CiMini batch before deciding whether an n-gram index is warranted.
3. **`SemanticMatcher.totalImageTokens` precision** — mixes matched-token count with candidate-family count, so `stringSignal` can drift for reasons unrelated to the filename itself. Replace with a real filename-token count (reuse `StringMatcher`'s tokenizer); verify accept/reject decisions don't shift on CiMini before rollout.

**What to do:** Pick off each item independently per its `jbtodo.md` recommended solution; each needs its own decision/measurement step before code changes, not a blind implementation.

**Acceptance:**
- Each of the 3 items has either a code change + passing tests, or a documented "measured, not worth it" close with no code change.
- `jb/src/core/Services/Matching/Match/jbtodo.md` items closed and moved to `jb/docs/` per the todo lifecycle.

**Files:** `jb/src/core/Services/Matching/Match/jbtodo.md`, `jb/src/core/Services/Matching/Match/StringMatcher.cs`, `jb/src/core/Services/Matching/Match/NumericMatcher.cs`, `jb/src/core/Services/Matching/Match/SemanticMatcher.cs`, `jb/src/core/lib/Excel/ModelBuilder.cs`, `jb/docs/PRISM-match.md`.

---


### T-3900 · Order: `DetermineTieBreaker` rescan can mislabel the deciding tiebreaker
**Status:** Ready | **Profile:** P1-feature-worker
**Tracks:** `jb/src/core/Services/Matching/Order/jbtodo.md` (triaged 2026-07-11).

**Problem:** After a winning image is assigned a det slot, `ImageOrderer.DetermineTieBreaker` rescans the *entire* candidate list for the family to find competitors (same slot, same phenotype rank) and reports the first tiebreaker level where *any* competitor differs from the winner. With 3+ competitors losing for different reasons, this can name the wrong tiebreaker as the deciding one — e.g. it reports "ngp-confidence" because a clearly-losing competitor differs on confidence, when the real closest competitor actually lost on the filename-hint tiebreaker instead. Does not affect the actual `DetOrder` assigned — only the `OrderEvidence.TieBreakerWon` diagnostic text, so this is a manifest-readability/debugging issue, not an output-correctness bug.

**What to do:**
1. The candidate list is already sorted by slot then phenotype rank (`CompareCandidates`), so same-slot+phenotype-rank candidates sit in one contiguous block. Group the sorted list into these blocks once per family instead of rescanning the full family list per winner.
2. Compare the winner only against its immediate runner-up within that block (the next candidate not already claimed elsewhere), not against every competitor — fixes the mislabeling and removes the full-list rescan as a side effect.

**Acceptance:**
- `OrderEvidence.TieBreakerWon` names the tiebreaker that actually decided against the true closest competitor, verified against the counter-example in the source `jbtodo.md` (winner NgpConfidence=5/HintScore=1 vs. a tied-confidence/lower-hint true competitor plus an unrelated lower-confidence non-competitor).
- `DetOrder` output unchanged (this is a diagnostic-only fix) — confirm via existing `ImageOrdererTests.cs`.

**Files:** `jb/src/core/Services/Matching/ImageOrderer.cs`, `jb/src/core/Services/Matching/Order/jbtodo.md`, `jb/src/tests/Prism.Core.Tests/Order/ImageOrdererTests.cs`.

---


### T-4000 · Per-feature Analyzer TOC: calibration + stub implementation backlog
**Status:** Ready | **Profile:** P0-orchestrator
**Tracks:** `jb/src/core/Services/Matching/Analyzers/jbtodo.md` (triaged 2026-07-11) — a TOC of ~27 items across 3 sections, none previously represented on the ticket board.

**Problem:** `Analyzers/jbtodo.md` is a checklist pointing at per-analyzer working docs, split into:
1. **Implemented, calibration open (11)** — `Analyzer_ProductType`, `Analyzer_FilenameEvidence`, `Analyzer_HasHuman`, `Analyzer_SubjectGeometry`, `Analyzer_DominantColors`, `Analyzer_ProductColor`, `Analyzer_BackgroundColor`, `Analyzer_Exposure`, `Analyzer_MultipleProducts`, `Analyzer_Interior`, `Analyzer_IsIllustration` — each has a named open calibration/validation question in its own `.md`.
2. **Stubs, implementation open (10)** — `Analyzer_FacePose` (highest value: 6 features, unblocks most on-model phenotypes), `Analyzer_TextPresent`, `Analyzer_Mannequin`, `Analyzer_LogoPresent`, `Analyzer_CameraAngle`, `Analyzer_IndoorOutdoor`, `Analyzer_ShadowReflection`, `Analyzer_Packaging`, `Analyzer_MaterialTexture`, `Analyzer_LightingDetail`.
3. **Cross-cutting (5)** — retire `ImageOrderer.ResolveProductType`'s value-sniffing fallback once `Analyzer_ProductType` is validated; unify `ProductTypeMap.json`/`TranslationDictionary.json` vocabulary; segmentation-model milestone for true coverage-ratio masks; `Analyzer_Symmetry` stays dropped unless an orientation rule wants it; standardize CLIP-vs-analyzer write precedence.
4. **OPEN (1)** — centralize per-analyzer `*Config.cs` files into a single `AnalyzerConfig.cs` with nested objects.

**This ticket is an index, not a single unit of work.** Individual items are gated by the Milestone Gates table (M6 Human & Model Detection through M10 Semantic & Content each name the specific analyzers they depend on); pick items in milestone order, starting with `Analyzer_FacePose` (blocks the most downstream phenotypes) and the config-centralization item (independent of any milestone, can start anytime).

**What to do:** Orchestrator splits this into per-analyzer or per-milestone-batch follow-up tickets as work is picked up, rather than one agent attempting all 27 items at once.

**Acceptance:** Each analyzer's `.md` open question is answered and its `jbtodo.md` checkbox checked, in milestone order; `jb/src/core/Services/Matching/Analyzers/jbtodo.md` reflects real remaining state at all times (not batch-updated at the end).

**Files:** `jb/src/core/Services/Matching/Analyzers/jbtodo.md`, `jb/src/core/Services/Matching/Analyzers/*.md`, `jb/src/core/Services/Matching/Analyzers/*.cs`.

---


### T-4100 · Investigate real GPU vs CPU ONNX behavior: health reports CPU-only on a GPU dev machine
**Status:** Ready | **Profile:** P1-feature-worker
**Found by:** memory-vs-reality contradiction during the 2026-07-10 full-pipeline test.

**Problem:** `GET /PRISM/health` on the dev machine reported `SupportedRuntimeProviders: ["CPU"]` during the 2026-07-10 test runs, yet project memory (and the Upscale engine's purpose) says the dev machine has a real GPU and the full GPU pipeline is locally testable. Either (a) the current build genuinely runs all ONNX inference (CLIP, YOLO, Real-ESRGAN) on CPU — meaning GPU acceleration silently stopped being used, or (b) the health endpoint's provider probe is wrong/stale and misreports what ONNX Runtime actually uses. Both are worth knowing: (a) costs real wall-clock time on every classify/upscale batch; (b) makes the health endpoint lie about capacity.

**Policy context (`jb/docs/PRISM-classify.md`):** CPU is the required baseline; GPU is a bonus resource only; GPU absence must never fail a job. This ticket is about *knowing* which one is actually in use and restoring GPU use if it regressed — not about making GPU required.

**What to do:**
1. Determine what `SupportedRuntimeProviders` in `PrismApiConfiguration`/health probe actually reflects (queried ONNX Runtime providers vs hardcoded list).
2. Check which ONNX Runtime package(s) the solution references (CPU-only `Microsoft.ML.OnnxRuntime` vs `.Gpu`/DirectML) and which execution providers `InferenceSession` creation actually requests in `ImageClassifier`, YOLO analyzers, and `Upscaler_g_p_u`.
3. Measure: time a CiMini classify batch under the current build; if a GPU provider can be enabled (DirectML on this Windows box), measure again and record the delta.
4. Fix whichever side is wrong: either wire the GPU execution provider back in (keeping CPU fallback per policy) or correct the health probe so it reports the truth; update `project_local_gpu_verification` memory and any stale doc claims.

**Acceptance:**
- A documented answer to "what provider does each ONNX session actually use on this machine" (health endpoint + a log line or doc note).
- If GPU is available and enabled: CiMini classify measurably faster than CPU-only baseline, with CPU-only mode still fully green.
- Health endpoint reflects the real provider list.

**Files:** `jb/src/api/PrismApiConfiguration.cs` (or wherever the provider probe lives), `jb/src/core/Services/Matching/Classify/ImageClassifier.cs`, `jb/src/core/Services/Matching/Analyzers/*.cs` (YOLO session), `jb/src/core/Services/Upscale/Engine/Upscaler_g_p_u.cs`, `jb/src/core/config/Prism_Config.json`.

---


### T-4200 · Transform engine config retrofit: extract Tx_* empirical tunables to transform_Config.json
**Status:** Active | **Profile:** P1-feature-worker
**Found by:** 2026-07-11 config-rule audit (review-gap discussion) — Transform never got the config extraction the Analyzers got.

**Problem:** Every empirical tunable in `Services/Transform/Engine/` is a `private const` and no transform config file exists in `jb/src/core/config/`, violating the config-driven design rule. Known inventory: `Tx_ProblemImageProcessor` (MinInputPx=570, MinOutputPx=800, MaxUpscale=1.42), `Tx_util_BgStretch` (Tier1MaxRatio=1.25, Tier2MaxRatio=1.42, Tier4MinRatio=2.50, FeatherPx=16), `Tx_DetailCropper` (adjacentCropCap=0.14), `Tx_LowContrastEnhancement` (ClipLimit=2.0, TileSize=8), `Tx_util_HeadCutter` (0.75 face-height cut factor); sweep `Tx_CenterAndStretch` and `Tx_CropSquare` for the rest.

**What to do:** Follow the `AnalyzerConfig` pattern for Load/Validate but per the shadow-defaults core rule (2026-07-12): `transform_Config.json` in `jb/src/core/config/` (one section per Tx class) + typed config classes (one type per file) with **no in-code property initializers** — every property `required`, so a missing/misspelled key throws at deserialization. The old constant values live ONLY in the JSON. Fail-loud `Load`, `Validate` with range checks. Wire loading the same way `AnalyzerConfig` is loaded; add the file to API startup validation so a missing/invalid file fails fast.

**Scope boundary:** Purely mechanical extraction — zero behavior change (values move to JSON byte-for-byte). Structural math constants (byte midpoints, loop bounds) stay inline; only empirical tunables move. Do NOT resolve any HeadCutter `jbtodo.md` product decisions. Do NOT touch Analyzers config ([[T-4300]] strips its shadow defaults; T-4000 item 4 covers centralization).

**Acceptance:** `dotnet build jb/src/PRISM.sln` green; Transform suite green (`--filter "FullyQualifiedName~PrismCoreTests.Transform"`); a transform_Config load/validation test exists; API startup validation covers the new file.

**Files:** `jb/src/core/Services/Transform/Engine/Tx_*.cs`, `jb/src/core/config/transform_Config.json` (new), typed config classes (new), config-loading wiring, `jb/src/api/PrismApiConfiguration.cs`, `jb/src/tests/Prism.Core.Tests/` (Transform suite).

---


### T-4300 · Strip shadow defaults from Analyzer config classes: required keys, analyzer_Config.json is the only source
**Status:** Ready | **Profile:** P1-feature-worker
**Found by:** [[T-4200]] shadow-defaults policy decision (2026-07-12).

**Problem:** The Analyzer config classes carry in-code property initializers ("defaults mirror the previously hard-coded constants"). A missing or misspelled key in `analyzer_Config.json` silently falls back to the in-code value — two sources of truth, and the losing one wins silently. The shadow-defaults core rule (CLAUDE.md, Configuration-driven design) now forbids this for Transform and Analyzers.

**What to do:** In every `jb/src/core/Services/Matching/Analyzers/*Config.cs` (including root `AnalyzerConfig.cs` section properties): remove all property initializers and declare every property `required`, so System.Text.Json throws on a missing key. Verify `analyzer_Config.json` carries every key (it does today — confirm nothing relies on an absent key). Add a fail-loud test to the Analyzers suite: deserializing JSON missing one key throws.

**Scope boundary:** Mechanical strip only. Do NOT centralize the per-analyzer `*Config.cs` files into one file (T-4000 item 4 owns that). Do NOT change any threshold value.

**Acceptance:** `dotnet build jb/src/PRISM.sln` green; Analyzers suite green (`--filter "FullyQualifiedName~PrismCoreTests.Analyzers"`); missing-key test throws; zero property initializers remain in Analyzer config classes (the `config-shadow-default` hook category reports none).

**Files:** `jb/src/core/Services/Matching/Analyzers/*Config.cs`, `jb/src/core/Services/Matching/Analyzers/AnalyzerConfig.cs`, `jb/src/core/config/analyzer_Config.json`, `jb/src/tests/Prism.Core.Tests/` (Analyzers suite).

---


## Verification Rules

- After project/solution setup: `dotnet build jb/src/PRISM.sln`, API run smoke, web `npm run typecheck` + `npm run build`.
- After each milestone: run relevant smoke and record result in milestone table.
- After todo/doc sync: `rg --files -g 'jbtodo.md' jb/src` and `rg -n "^- \[ \]" jb/src`.
- Before advancing milestones: `git status --short` to confirm edits stayed inside assigned ownership.
