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
Done tickets are moved to `AGENT-TICKETS-ARCHIVE.md` (via /ticket-finish) — this file holds open tickets only.

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


### T-4110 · Unify ONNX Runtime execution-provider policy across every model-running component in PRISM
**Status:** Review | **Profile:** P4-critical-architecture
**Found by:** [[T-4100]] — health-probe investigation surfaced two inconsistencies (version skew + YOLO CPU-only).
**Implemented (2026-07-20):** CPM via new `jb/src/Directory.Packages.props` — single pin for ORT DirectML 1.24.4 plus (user-directed scope extension) ImageSharp/OpenCvSharp4/test packages; new `OnnxSessionFactory` (file-linked like `GpuProbe`) is the sole session-construction path for CLIP/YOLO/Upscale; `RuntimeProviderProbe.SessionProviders()` no longer hardcodes YOLO=CPU; conventions-hook category `onnx-session-bypass` added and verified firing; policy doc `jb/docs/PRISM-model-runtime.md` + index row + classify-doc pointer + AGENTFEEDBACK entry. Build green; full suite failure set byte-identical to HEAD baseline on the Linux CI container (failures = missing model assets + Windows-only OpenCV natives, pre-existing).
**Scope extension (2026-07-20, user):** no algorithm switching on GPU presence — Upscale now loads Real-ESRGAN on every host (CPU EP when no adapter, like CLIP/YOLO). Follow-up user decisions the same day: `Upscaler_c_p_u` (Lanczos fallback) and the `ImageUpscaler` router are **deleted** — single `Upscaler` class; missing/unloadable Real-ESRGAN now fails startup loud (`ValidateModelAssets` + `UpscaleService.Create`), same as YOLO, no silent degradation. Decisions recorded in `PRISM-model-runtime.md`.
**Deferred to dev box (needs model assets + Windows):** CiMini golden 5× re-verify after the 1.20.1→1.24.4 CLIP runtime bump, and live `GET /PRISM/health` `SessionRuntimeProviders` check (expect all three identical: DirectML(GPU) on the GPU box / CPU when no adapter). Do these before /ticket-finish.

**Problem:** PRISM's ONNX/model-running components are inconsistent along three axes that should be uniform:
1. **Package version skew.** Classify (CLIP) pins `Microsoft.ML.OnnxRuntime.DirectML 1.20.1`; Upscale pins `1.24.4`. In the monolith API host both run in-process, so two versions of the same native runtime load into one address space — a latent binding/load-order risk (works today, but fragile).
2. **Provider policy skew.** CLIP (`ImageClassifier.cs:108-111`) and Upscale (`Upscaler_g_p_u.cs:60-62`) append the DirectML EP gated on `GpuProbe.HasHardwareDirectMLAdapter()`; **YOLO (`YoloDetector.cs:65`) appends no EP at all → CPU-only always**, even on a GPU box. No shared session-options factory exists, so each site decides independently.
3. **No mandate for future model code.** Analyzers (e.g. `Analyzer_FacePose`, `Analyzer_TextPresent`, YOLO-based ones) and future transformers (segmentation for coverage-ratio masks, etc.) will also run models, with no single policy to follow.

**Mandate (2026-07-15, user):** every part of PRISM image processing that runs a model MUST use the **same ONNX Runtime DirectML package, the same version, and the same execution-provider policy** — **CPU-only always works (mandatory baseline); GPU (DirectML) used automatically when a hardware adapter is present.** Applies to CLIP, YOLO, Upscale today, and to all future analyzers and transformers. This is a sibling of [[T-3300]] (each separable service/deployable must honor the same policy independently), not of T-3500/T-3600.

**What to do:**
1. **Single version.** Centralize the ONNX Runtime DirectML package + version to one pin (central package management via `Directory.Packages.props`, or the existing `jb/src/Directory.Build.props`). Align the two engine projects to one version. **Re-verify CiMini golden 5× after the bump** — changing CLIP's runtime can shift FP results (guards [[T-2820]]'s determinism).
2. **Single provider policy.** Introduce one shared session-options factory in core (e.g. `OnnxSessionFactory`, reusing `GpuProbe`) that appends the DirectML EP when a hardware adapter is present and falls back to CPU otherwise. Route CLIP, YOLO, and Upscale through it — YOLO gains GPU-when-present; all three become identical. No direct `AppendExecutionProvider_DML` or bare `new InferenceSession` outside the factory.
3. **Make it mandatory + enforced.** Document the policy in `jb/docs/PRISM-classify.md` (or a dedicated model-runtime note) + `AGENTFEEDBACK.md`, and add a conventions-hook category so any new `InferenceSession` not created via the factory fails review. Covers future analyzers/transformers.

**Acceptance:**
- Exactly one ONNX Runtime DirectML package + version referenced repo-wide (grep-proven).
- One shared session-options factory; CLIP/YOLO/Upscale all use it; no bare `InferenceSession`/`AppendExecutionProvider_DML` elsewhere.
- `GET /PRISM/health` `SessionRuntimeProviders` shows all three consistent (all DirectML(GPU) on a GPU box; all CPU on a CPU-only box).
- CPU-only mode fully green (forced no-adapter path); CiMini golden identical across 5 consecutive runs after version unification.
- Documented, enforced mandatory policy for any future model-running code.

**Files:** `jb/src/Directory.Build.props` (or new `Directory.Packages.props`), the three engine `.csproj`, `jb/src/core/Services/Matching/ImageClassifier.cs`, `jb/src/core/Services/Matching/Analyzers/YoloDetector.cs`, `jb/src/core/Services/Upscale/Engine/Upscaler_g_p_u.cs`, `jb/src/core/Services/Matching/GpuProbe.cs`, new `OnnxSessionFactory`, `jb/src/api/RuntimeProviderProbe.cs`, `jb/docs/PRISM-classify.md`, `AGENTFEEDBACK.md`, conventions hook.

---


### T-4400 · Adopt Roslyn analyzers: SA1402/SA1649/SA1101/SA1633/S109 (S109 priority), suppress SA1500/SA1025/SA1503
**Status:** Ready | **Profile:** P1-feature-worker
**Found by:** 2026-07-12 analyzer baseline trial (StyleCop.Analyzers + SonarAnalyzer.CSharp on Prism.Core: 2,699 unique warnings).

**Problem:** Style/config rules are enforced only at edit time (conventions hook) and by review — nothing compiler-grade catches violations from non-Claude edits or agents that bypass process. The baseline trial measured per-rule cost in Prism.Core: SA1402 (one type per file) = 9, SA1649 (file name matches type) = 1, SA1101 (`this.` prefix) = 472, SA1633 (file header) = 113, SA1025 (whitespace) = 424, SA1503 (braces required) = 320, S109 (magic numbers) = 98.

**Pre-existing state (verified 2026-07-14):** `jb/src/Directory.Build.props` **already exists** (committed in `06e09ca` "First agentic wave") and currently sets `TargetFramework` / `ImplicitUsings` / `Nullable` / `LangVersion` / `Deterministic` for every project under `jb/src/`. This ticket **extends** that file — it does not create it. Nothing else is in place: no `StyleCop.Analyzers` or `SonarAnalyzer.CSharp` package reference exists anywhere in the repo, and there is no `SonarLint.xml`.

**What to do:**
1. Add `StyleCop.Analyzers` (prerelease, for modern C#) + `SonarAnalyzer.CSharp` to all production projects — via the existing `Directory.Build.props` at `jb/src/`, scoped to exclude the test project (S109 on test literals would be pure noise; decide test-project treatment explicitly).
2. Curated severities in the root `.editorconfig`: `dotnet_analyzer_diagnostic.severity = none` as the floor, then explicitly:
   - `warning`: SA1402, SA1649, SA1101, SA1633, **S109 (priority — this is the config-driven-design rule at compiler grade)**. S109 needs `dotnet_diagnostic.S109.severity = warning` (off by default) plus a `SonarLint.xml` AdditionalFile to set its allowed-values parameter (0, 1, -1 at minimum) so structural constants don't drown the empirical ones.
   - `none` **permanently**: SA1500 — it enforces Allman brace placement, the exact opposite of the house K&R rule (`csharp_new_line_before_open_brace = none` in `.editorconfig`). Comment the suppression with this reason.
   - `none` **for now (deferred, not rejected)**: SA1025, SA1503 — enable in a later phase once the 424 + 320 baseline is burned down (large mechanical cleanups; `dotnet format` handles most of SA1025).
   - Caveat to verify: the global `none` floor also mutes IDE analyzer hints (IDE0xxx) in C# Dev Kit — if that proves annoying, replace the floor with per-category StyleCop/Sonar disables.
3. Burn down in phases, gating each finished rule in CI (`-warnaserror:RULE` in the ci.yml build step): phase 1 SA1402 (9) + SA1649 (1); phase 2 S109 triage (98 in core — each is either moved to config per the shadow-defaults rule or explicitly justified as structural); phase 3 SA1633 (113 — decide the header template first; house style is token-lean, so keep it minimal); phase 4 SA1101 (472 mechanical `this.` insertions).
   - SA1101 direction check before phase 4: SA1101 *requires* the `this.` prefix; StyleCop's inverse rule is SX1101 (forbid it). Current code omits `this.` everywhere — confirm with the user that adding 472 prefixes is really the wanted direction, since it contradicts the "short, practical" style line.
4. Keep the conventions hook as-is — it stays the edit-time delta layer (catches new violations instantly, judgment-friendly); the analyzers are the build-time backstop that sees every edit from anyone.

**Acceptance:** Packages active in all production projects; curated `.editorconfig` severities in place with suppression reasons commented; SA1402/SA1649/S109 at zero warnings and CI-gated; SA1633/SA1101 either at zero or split into follow-up tickets; SA1500 suppressed with the K&R rationale; full suite green.

**Files:** `jb/src/Directory.Build.props` (exists — extend), `.editorconfig` (root, exists — extend), `SonarLint.xml` (new), `.github/workflows/ci.yml`, phased cleanup edits across `jb/src`.

---


### T-4600 · SSE progress events carry no per-item counts or blocked state
**Status:** Ready | **Profile:** P1-feature-worker
**Found by:** [[T-3400]] review (2026-07-14) — the web StatusPanel requirement that could not be met from the web side.

**Problem:** `PipelineProgressEvent` (`jb/src/core/Pipeline/PipelineProgressEvent.cs`) declares `CompletedCount`/`TotalCount`/`Severity` fields, but the only place any `PipelineProgressEvent` is ever constructed is `StageProgress.EmitStarted` (`jb/src/core/Services/StageProgress.cs:24-31`). It emits exactly one `"Stage {name} started."` event per stage, with `CompletedCount`/`TotalCount` left `null` and `Severity` hardcoded to `"Information"`. No accepted/rejected count, no blocked-vs-running state, and no per-item progress is emitted anywhere in the pipeline.

Consequence: the workbench can only ever display a stage *name*. `PRISM-workbench.md`'s Required Display section mandates "image collection/import state", "output preview", and "KO records" — none of which the SSE stream can currently source. T-3400 was closed on the narrower claim (real stage name replaces placeholder text) precisely because its web-only file scope made this unfixable there.

**What to do:**
1. Decide the progress contract: which stages emit per-item progress, and what an item is (per image? per family?). Import and Export are the two the workbench most needs (accepted/rejected counts).
2. Extend `StageProgress` beyond `EmitStarted` — at minimum an `EmitProgress`/`EmitCompleted` that populates `CompletedCount`/`TotalCount`, and a real `Severity` for blocked/warning states (KO records are the obvious source).
3. Emit from `Importer.cs` and `Exporter.cs` first (accepted/rejected are already computed there — KO records exist), then the remaining stages as warranted.
4. Update `StatusPanel.tsx` to read `severity` (it currently ignores the field entirely — only `StageRouteList.tsx:41` reads it) and render the real counts + blocked-vs-running distinction.

**Acceptance:** a running job's SSE stream carries non-null `CompletedCount`/`TotalCount` for Import and Export, and a non-`Information` `Severity` when items KO; the workbench StatusPanel shows real accepted/rejected counts and a blocked-vs-running distinction sourced from those events (no synthetic labels, per the No-Hidden-Behavior Rule).

**Files:** `jb/src/core/Pipeline/PipelineProgressEvent.cs`, `jb/src/core/Services/StageProgress.cs`, `jb/src/core/lib/Ingress/Importer.cs`, `jb/src/core/lib/Export/Exporter.cs`, `jb/src/workbench/web/components/StatusPanel.tsx`, `jb/docs/PRISM-workbench.md`.

---


## Verification Rules

- After project/solution setup: `dotnet build jb/src/PRISM.sln`, API run smoke, web `npm run typecheck` + `npm run build`.
- After each milestone: run relevant smoke and record result in milestone table.
- After todo/doc sync: `rg --files -g 'jbtodo.md' jb/src` and `rg -n "^- \[ \]" jb/src`.
- Before advancing milestones: `git status --short` to confirm edits stayed inside assigned ownership.
