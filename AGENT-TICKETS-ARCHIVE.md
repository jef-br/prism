# PRISM Agent Tickets — Archive

Done tickets, moved here by /ticket-finish to keep AGENT-TICKETS.md (read every session start) lean.
Newest at the top.

### T-4710 · Collapse DetOrderRules/ProductTypeMap to 5 product types; expose WinningPhenotype
**Status:** Done (2026-07-27) | **Profile:** P1-feature-worker
**Found by:** [[T-4700]] — direct follow-up, same "subtract, then get a reliable catch-all
working" effort.
**Review:** Approve (2026-07-27) — verified `topwear`'s synonym list is byte-identical to the
old `clothing-tops`, `bottomwear`'s list is the clean union of `clothing-bottoms`+
`clothing-dresses` with no cross-group term collisions, and all 13 retired groups' raw terms are
fully gone (not just renamed). `DetOrderRules.json` diffed against git history: `topwear`/
`bottomwear` tables are byte-for-byte the old `clothing-tops`/`clothing-bottoms` content under
new keys, confirming the user's tie-break choices landed correctly. `ImageTransformer`'s
`IsDetailCropperDetSlotExcluded` fix re-derived as correct and confirmed still dead code (gated
behind `BypassPhenotypes=true`, same limitation the existing test file already documents — no
coverage was lost). `WinningPhenotype` export gated identically to `DetOrder`, both new
`ExporterTests.cs` cases non-vacuous (positive + KO-null). Build 0 errors, full suite 417/417.
Two non-blocking doc nits (a wrong ticket-number attribution, a stale `headphone`/
`electronics-small` example) fixed same session. Commit `fd894aa`.

`DetOrderRules.json`/`ProductTypeMap.json` had 19 product types (`default` + 18 bespoke ones),
none validated in production. Per user direction: subtract down to `default` + 4 categories that
are actually in scope right now (`topwear`, `bottomwear`, `footwear`, `bags-accessories`); the
other 13 (`clothing-outerwear`, `fmcg-*`, `beauty-cosmetics`, `electronics-*`, `homeware-*`,
`toys-children`, `diy-tools`, `gardening`, `sports-equipment`, `furniture`) fall back to
`default`. `clothing-tops`→`topwear` (unchanged synonym list); `clothing-bottoms`+
`clothing-dresses`→`bottomwear` (merged per explicit user tie-breaks: allow back/side-packshot
fallback at det1/det2, and rank `front-on-model-partial` ahead of `lifestyle-hero` at det4 —
both resolved in `clothing-bottoms`' favor, so the merged table is `clothing-bottoms`' content
verbatim under the new id). Also exposes `OrderEvidence.WinningPhenotype` (computed by
`ImageOrderer` but never surfaced) on the export manifest, so a downstream consumer can see
*why* an image landed in a given det slot instead of inferring it from position alone.

**What to do:** rename/merge `ProductTypeMap.json` groups and `DetOrderRules.json` tables per
above; fix `ImageTransformer.IsDetailCropperDetSlotExcluded`'s `StartsWith("clothing-")` check
to match the renamed ids (`topwear`/`bottomwear`) — note this method is currently unreachable
dead code while `BypassPhenotypes = true` gates the whole `DetailCropper` branch off, so the fix
is a correctness-for-later change, not something testable end-to-end today; add
`ManifestImageRow.WinningPhenotype`, wire it in `Exporter.ToManifestRow`; update
`ImageOrdererTests.cs`, `ProductTypeResolverTests.cs`, and `ExporterTests.cs` for the
renamed/removed ids and the new field.

**Acceptance:** `dotnet build jb/src/PRISM.sln` and `dotnet test jb/src/PRISM.sln` green;
`DetOrderConfig.Load` reports exactly 5 product types (`default`, `topwear`, `bottomwear`,
`footwear`, `bags-accessories`); no dangling `clothing-*`/retired-category id anywhere in
production code, tests, or `ProductTypeMap.json`/`DetOrderRules.json`.

**Files:** `jb/src/core/config/ProductTypeMap.json`, `jb/src/core/config/DetOrderRules.json`,
`jb/src/core/Services/Transform/ImageTransformer.cs`, `jb/src/core/lib/Export/ManifestImageRow.cs`,
`jb/src/core/lib/Export/Exporter.cs`, `jb/src/core/Models/ImageRecord_LAMBDA.cs`,
`jb/src/core/Services/Matching/Order/DetOrderConfig.cs`,
`jb/src/core/Services/Matching/Analyzers/Analyzer_ProductType.cs`,
`jb/src/core/Services/Matching/Analyzers/ProductTypeResolver.cs`,
`jb/src/core/Services/Matching/Analyzers/Analyzer_Interior.md`,
`jb/src/tests/Prism.Services.Matching.Tests/Order/ImageOrdererTests.cs`,
`jb/src/tests/Prism.Services.Matching.Tests/Analyzers/ProductTypeResolverTests.cs`,
`jb/src/tests/Prism.Core.Tests/Export/ExporterTests.cs`,
`jb/docs/ImageNGP/PRODUCTTYPES.MD` (flagged stale, not fully rewritten — see note in file),
`jb/docs/ideas-on-NGP.md`.

---

### T-4700 · Remove unimplemented analyzers; trim ImageNGP/ImageRoles/DetOrderRules to real+reachable only
**Status:** Done (2026-07-27) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-27) — verified deletion completeness (all 10 stub `.cs`/`.md` pairs
and their `Prism.Services.Matching.Classify.csproj` `Compile Include` entries gone), zero
dangling references to any of the 23 removed features or 6 removed phenotypes anywhere
(`ImageRoles.json`, `DetOrderRules.json`, `ClipPrompts.json`, `ImageFeatureAnalyzer.cs`, tests),
`ghost-front`'s dead clause removed without reordering (confirmed against
`PhenotypeRuleSetTests.cs`'s new overlap/reachability tests), and every `DetOrderRules.json` slot
that lost its only phenotypes became `[]` rather than being deleted (preserving overflow slot
numbering). Build 0 errors, full suite 415/415 (then 417/417 after T-4710). Two non-blocking doc
nits (a feature-count off-by-one, a doc example citing a just-deleted feature) fixed same session.
Commit `fe9ac38`.

`ImageNGP.json` declares 60 features and 26 phenotypes, but only 11 of 21 analyzer classes are
actually implemented — the other 10 (`Analyzer_FacePose`, `Analyzer_TextPresent`,
`Analyzer_Mannequin`, `Analyzer_LogoPresent`, `Analyzer_CameraAngle`, `Analyzer_IndoorOutdoor`,
`Analyzer_ShadowReflection`, `Analyzer_Packaging`, `Analyzer_MaterialTexture`,
`Analyzer_LightingDetail`) are empty-body stubs. Because `PhenotypeRuleSet` treats `UNKNOWN` as
never satisfying a required condition, every phenotype gated on a stub-only feature is
mathematically unreachable — 6 of 26 phenotypes are dead on arrival, cascading into 13 of 19
`DetOrderRules.json` product-type tables having an inert det-slot. First half of a user-directed
"simplify by subtraction, then re-expand piecemeal" effort (see [[T-4000]], [[T-2600]]); a
follow-up ticket collapses `DetOrderRules.json`/`ProductTypeMap.json` from 19 product types to 5.

**What to do:** delete the 10 stub `.cs`/`.md` pairs and their call sites in
`ImageFeatureAnalyzer.cs`; remove the 23 features they would have produced plus the
structurally-dead `background-type=STUDIO` enum value from `ImageNGP.json` (60→37 features);
remove the 6 now-unreachable phenotypes from `ImageNGP.json`/`ImageRoles.json` (26→20), dropping
`ghost-front`'s dead `contains-mannequin` clause without reordering; strip the 6 dead phenotype
ids from every `DetOrderRules.json` slot; update `Analyzers/jbtodo.md`, `Classify/jbtodo.md`,
`ImageFeatures.md`, `imagePhenotypes.md`, `PRISM-index.md`, and 3 Classify test files
accordingly; write a new `jb/docs/ImageNGP/HowToAddAPhenotype.md` reference doc covering the
full analyzer→feature→phenotype→det-order wiring chain with a worked hero-image example.

**Acceptance:** `dotnet build jb/src/PRISM.sln` and `dotnet test jb/src/PRISM.sln` green; startup
`ImageNgpValidator` passes (no dangling id references across `ImageNGP.json`/`ImageRoles.json`/
`DetOrderRules.json`/`ClipPrompts.json`); no behavior change for any image that previously
exercised a real (non-stub) code path — pure removal of unreachable paths.

**Files:** `jb/src/core/Services/Matching/Analyzers/*.cs`, `jb/src/core/Services/Matching/Analyzers/*.md`,
`jb/src/core/Services/Matching/Analyzers/jbtodo.md`, `jb/src/core/Services/Matching/Classify/ImageFeatureAnalyzer.cs`,
`jb/src/core/Services/Matching/Classify/jbtodo.md`, `jb/src/core/config/ImageNGP.json`,
`jb/src/core/config/ImageRoles.json`, `jb/src/core/config/DetOrderRules.json`,
`jb/docs/ImageNGP/ImageFeatures.md`, `jb/docs/ImageNGP/imagePhenotypes.md`,
`jb/docs/ImageNGP/HowToAddAPhenotype.md` (new), `jb/docs/PRISM-index.md`,
`jb/src/tests/Prism.Services.Matching.Tests/Classify/ImageFeatureAnalyzerTests.cs`,
`jb/src/tests/Prism.Services.Matching.Tests/Classify/PhenotypeRuleSetTests.cs`,
`jb/src/tests/Prism.Services.Matching.Tests/Classify/ImageFeatureSnapshotTests.cs`,
`AGENT-TICKETS.md`.

---

### T-4400 · Adopt Roslyn analyzers: SA1402/SA1649/SA1101/SA1633/S109 (S109 priority), suppress SA1500/SA1025/SA1503
**Status:** Done (2026-07-24) | **Profile:** P1-feature-worker
**Review (phase 1, 2026-07-20):** Approve. StyleCop.Analyzers/SonarAnalyzer.CSharp wired into every production project, curated root `.editorconfig`, `SonarLint.xml`, SA1402/SA1649 fixed to zero and CI-gated — verified internally consistent (no type compiled twice or dropped across the Prism.Core/Prism.Core.Contracts Include/Remove split), package versions confirmed real/current on nuget.org, SonarLint.xml schema confirmed correct for sonar-dotnet, CI `-warnaserror:SA1402,SA1649` confirmed to actually fail the build on regression. Two non-blocking follow-ups for Planner: (1) `Prism.Tests.Shared` is excluded from analyzer coverage by the `*Tests*` name match even though CLAUDE.md documents it as a non-test fixture classlib — debatable but defensible; (2) the ticket's own "verify the global `none` floor doesn't mute IDE0xxx hints" caveat was never checked, and user has since said to drop it — not pursued. S109/SA1633/SA1101 correctly left warn-only (not silently suppressed, not prematurely gated) pending phases 2-4.
**Phase 2 done (2026-07-23):** S109 triaged to zero across the solution (real baseline was ~163 unique warnings across ~30 files, not the stale 98 estimate — a clean analyzer rebuild had never actually been re-measured since the phase-1 baseline). Nearly everything was structural (file-format magic bytes, RGB/luma/CHW-tensor math, alpha thresholds, pixel-sample strides, switch-pattern case values, config-validation bounds) and got named `private const`s at point of use — zero behavior change. One genuine infra-tuning file (`WetransferClient.cs`) got promoted to `HostRules.json`'s new `weTransferPolling` section instead, per the shadow-defaults rule. Per-feature confidence weights (CLIP/heuristic calibration in `ImageFeatureAnalyzer.cs`, `NumericMatcher.cs`, `SiblingPropagator.cs`, `StringMatcher.cs`) were deliberately named-const'd, **not** moved to config — calibration is an open product question tracked by [[T-2600]]; see `AGENTFEEDBACK.md`'s S109 entry for the standing rule on any newly-discovered confidence literal. `-warnaserror:SA1402,SA1649,S109` gates CI.
**Phase 4 done (2026-07-24):** SA1101 (472+ `this.`-prefix warnings, later re-measured at 878 on a true clean build) fixed solution-wide via `dotnet format jb/src/PRISM.sln analyzers --diagnostics SA1101` — purely mechanical, 94 files, zero behavior change (verified: 799 insertions/799 deletions, identical line counts). `-warnaserror:SA1402,SA1649,S109,SA1101` now gates CI. **SA1633 (phase 3) resolved by permanent suppression, not fix**: per user decision, `dotnet_diagnostic.SA1633.severity = none` in `.editorconfig`, same treatment as SA1500 — this repo's doc-comment convention (class-level `/// <summary>` only, CLAUDE.md) makes a per-file header pure noise, not a real gap. Final verification: clean non-incremental Release build with all 4 gated rules as errors → 0 errors, 11 residual warnings (pre-existing SA0001/CS0414/CS8602/CS8600, outside this ticket's scope); full test suite 408/408 passing.
**Review (2026-07-24):** Approve. Independent reviewer pass against the full phase 2-4 diff (`main..HEAD`, 147 files): reproduced the clean `-warnaserror:SA1402,SA1649,S109,SA1101` build (0 errors) and the full suite (408/408, then 416/416 after closeout fixes) itself rather than trusting reported numbers; spot-checked config-extraction commits for shadow-default violations (none found — every section class `required`-props + `IValidatableConfig`) and the one-type-per-file fold-in exception; confirmed the SA1101 commit is purely mechanical. Two closeout findings raised and both resolved before this Approve: an open `jbtodo.md` block from this same branch closed (decision moved to `PRISM-pipeline-core.md`'s Configuration Lifecycle section per the todo-lifecycle rule), and missing fail-loud test coverage added for the two new config classes this ticket shipped (`OutputConfig`, `ClassifyParameters`), mirroring the existing `AnalyzerConfigTests.cs`/`TransformConfigTests.cs` pattern — verified independently, not just re-run.
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

### T-4110 · Unify ONNX Runtime execution-provider policy across every model-running component in PRISM
**Status:** Done (2026-07-20) | **Profile:** P4-critical-architecture
**Review:** Approve (2026-07-20) — all acceptance criteria met; two non-blocking warnings: (1) `UpscaleService.Create`'s throw branches have no automated regression test (model assets absent in CI; fast follow-up suggested), (2) session-load failure (vs file existence) still degrades silently for YOLO/CLIP — doc caveat added to `PRISM-model-runtime.md`, code fix out of this ticket's scope.
**Found by:** [[T-4100]] — health-probe investigation surfaced two inconsistencies (version skew + YOLO CPU-only).
**Implemented (2026-07-20):** CPM via new `jb/src/Directory.Packages.props` — single pin for ORT DirectML 1.24.4 plus (user-directed scope extension) ImageSharp/OpenCvSharp4/test packages; new `OnnxSessionFactory` (file-linked like `GpuProbe`) is the sole session-construction path for CLIP/YOLO/Upscale; `RuntimeProviderProbe.SessionProviders()` no longer hardcodes YOLO=CPU; conventions-hook category `onnx-session-bypass` added and verified firing; policy doc `jb/docs/PRISM-model-runtime.md` + index row + classify-doc pointer + AGENTFEEDBACK entry. Build green; full suite failure set byte-identical to HEAD baseline on the Linux CI container (failures = missing model assets + Windows-only OpenCV natives, pre-existing).
**Scope extension (2026-07-20, user):** no algorithm switching on GPU presence — Upscale now loads Real-ESRGAN on every host (CPU EP when no adapter, like CLIP/YOLO). Follow-up user decisions the same day: `Upscaler_c_p_u` (Lanczos fallback) and the `ImageUpscaler` router are **deleted** — single `Upscaler` class; missing/unloadable Real-ESRGAN now fails startup loud (`ValidateModelAssets` + `UpscaleService.Create`), same as YOLO, no silent degradation. Decisions recorded in `PRISM-model-runtime.md`.
**Deferred to dev box (needs model assets + Windows):** CiMini golden 5× re-verify after the 1.20.1→1.24.4 CLIP runtime bump, and live `GET /PRISM/health` `SessionRuntimeProviders` check (expect all three identical: DirectML(GPU) on the GPU box / CPU when no adapter). Do these before /ticket-finish.
**Dev-box verification (2026-07-20):**
1. **Build + tests: PASS.** `dotnet build jb/src/PRISM.sln` — 0 errors. `dotnet test jb/src/PRISM.sln` — fully green: 399/399 (Upscale 15, Generate 10, Transform 51, Matching 193, Core 130).
2. **Health: PASS.** `GET /PRISM/health` → `SessionRuntimeProviders: ["CLIP=DirectML(GPU)","YOLO=DirectML(GPU)","Upscale=DirectML(GPU)"]` — all three identical, all GPU, YOLO no longer CPU-only. Caveat found while investigating step 4: `RuntimeProviderProbe.SessionProviders()` (`jb/src/api/RuntimeProviderProbe.cs:27-30`) derives all three labels from one `Upscaler.IsGpuAvailable` hardware check, not from querying each session's actual bound EP — so this endpoint cannot by itself catch a real per-model provider mismatch (e.g. one session silently falling back to CPU while `IsGpuAvailable` stays true). Not a regression from this ticket and out of the verification scope given here, but worth a follow-up ticket if that guarantee matters.
3. **CiMini golden 5×: PASS, 5/5 byte-identical.** `Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` run 5 consecutive times, no code change between runs — every run exited 0 and reported `Full PASSED: 14 sources match golden, 14 Ok.` against the same committed `expected-manifest.json` (asserts Status/FamilyId/FinalFileName/DetOrder per source), which transitively proves all 5 runs identical to each other post the 1.20.1→1.24.4 bump.
4. **Fail-fast: PASS, with a dev-box gotcha.** First attempt (renaming only `jb/src/core/Services/Upscale/Engine/ONNX/Real-ESRGAN_x2plus.onnx`) did **not** fail — this box has a machine-level `PRISM_ONNX_MODEL_DIR=C:\Users\JefB\prism-ci-assets\models` env var (`ModelAssetLocator`'s documented second-priority override, ahead of the source-tree walk) holding its own independent model copy, so the API started clean and healthy off that copy instead. Renamed the override-dir copy too, retried: startup now threw `Prism.Core.PrismConfigurationException: Real-ESRGAN ONNX model not found at 'Services/Upscale/Engine/ONNX/Real-ESRGAN_x2plus.onnx'...` from `PrismConfiguration.ValidateModelAssets` → `PrismApiConfiguration.Load()`, process exited, no port listener — correct fail-loud behavior, no silent fallback. Both copies restored afterward; re-verified clean healthy startup. Anyone re-running this check on a box with `PRISM_ONNX_MODEL_DIR` set must block that path too, or the test passes vacuously.

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


### T-4600 · SSE progress events carry no per-item counts or blocked state
**Status:** Done (2026-07-20) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-20)
**Found by:** [[T-3400]] review (2026-07-14) — the web StatusPanel requirement that could not be met from the web side.

**Problem:** `PipelineProgressEvent` (`jb/src/core/Pipeline/PipelineProgressEvent.cs`) declares `CompletedCount`/`TotalCount`/`Severity` fields, but the only place any `PipelineProgressEvent` is ever constructed is `StageProgress.EmitStarted` (`jb/src/core/Services/StageProgress.cs:24-31`). It emits exactly one `"Stage {name} started."` event per stage, with `CompletedCount`/`TotalCount` left `null` and `Severity` hardcoded to `"Information"`. No accepted/rejected count, no blocked-vs-running state, and no per-item progress is emitted anywhere in the pipeline.

Consequence: the workbench can only ever display a stage *name*. `PRISM-workbench.md`'s Required Display section mandates "image collection/import state", "output preview", and "KO records" — none of which the SSE stream can currently source. T-3400 was closed on the narrower claim (real stage name replaces placeholder text) precisely because its web-only file scope made this unfixable there.

**What to do:**
1. Decide the progress contract: which stages emit per-item progress, and what an item is (per image? per family?). Import and Export are the two the workbench most needs (accepted/rejected counts).
2. Extend `StageProgress` beyond `EmitStarted` — at minimum an `EmitProgress`/`EmitCompleted` that populates `CompletedCount`/`TotalCount`, and a real `Severity` for blocked/warning states (KO records are the obvious source).
3. Emit from `Importer.cs` and `Exporter.cs` first (accepted/rejected are already computed there — KO records exist), then the remaining stages as warranted.
4. Update `StatusPanel.tsx` to read `severity` (it currently ignores the field entirely — only `StageRouteList.tsx:41` reads it) and render the real counts + blocked-vs-running distinction.

**Acceptance:** a running job's SSE stream carries non-null `CompletedCount`/`TotalCount` for Import and Export, and a non-`Information` `Severity` when items KO; the workbench StatusPanel shows real accepted/rejected counts and a blocked-vs-running distinction sourced from those events (no synthetic labels, per the No-Hidden-Behavior Rule).

**Resolution (2026-07-20):** `StageProgress.EmitCompleted` populates `CompletedCount`/`TotalCount`/`Severity` (Warning when koCount>0). Wired from `IngestService` (Import stage, using `NormalizedImages.Count`/`ImageKoRecords+ZipKoRecords`) and `Pipeline.ExportAsync` (Export stage, using `LambdaRecords` `IsKo` split — the same records `Exporter.BuildZip` packages into OK/KO folders, after a review round caught the first pass using pipeline-wide cumulative KO counts instead of stage-scoped ones). `StatusPanel.tsx` reads `severity` and renders a blocked-state chip. Remaining stages (Classified/Matched/Ordered/Renamed/Generated/Transformed) intentionally left on `EmitStarted`-only per the ticket's own scoping — a candidate follow-up if the workbench needs mid-pipeline KO visibility before the final Export tally.

**Files:** `jb/src/core/Pipeline/PipelineProgressEvent.cs`, `jb/src/core/Services/StageProgress.cs`, `jb/src/core/lib/Ingress/Importer.cs`, `jb/src/core/lib/Export/Exporter.cs`, `jb/src/workbench/web/components/StatusPanel.tsx`, `jb/docs/PRISM-workbench.md`.

---

### T-3300 · Validate and complete the Phase 2 distributed-services seam
**Status:** Done (2026-07-17) | **Profile:** P4-critical-architecture
**Review:** Approve (2026-07-17)
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

**Closeout (2026-07-17):** implemented across commits `2f337b6`..`53502f9` (T-3300 branch, merged to main at `1ebd00e`). Reviewer verified distributed correctness via actual CI run `29451640778` showing both in-process and 4-service-host distributed goldens matching on CiMini; all four `Http*Service` clients have real-HTTP roundtrip tests (`jb/src/tests/Prism.Core.Tests/ServiceHost/`); test projects split into `Prism.Services.{Matching,Generate,Transform,Upscale}.Tests` + `Prism.Core.Tests` + `Prism.Tests.Shared`. Two non-blocking follow-ups noted by review, not ticketed separately: (1) `ServiceHostTestHelpers.cs`/`ServiceHostFixture.cs` carry method-level XML doc comments against CLAUDE.md's class-summary-only rule; (2) root `jbtodo.md`'s T-3300 independent-review block (R1-R8) should be closed per the todo lifecycle, with R7 (sync-over-async remote upscale call) ticketed separately if still wanted.

---

### T-3500 · Fuse Import→Match in-process handoff to remove redundant image decode
**Status:** Done (2026-07-15) | **Profile:** P1-feature-worker
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

**Closed: measured, not worth it (2026-07-15).** Gate decision per root `jbtodo.md`'s "measure before deciding" (user-approved, dataset SPACINI29 per user — CiMini too small). Temporary Stopwatch probe in `PrepareLambda` split the normalized-JPEG load into file-read vs decode; full pipeline on SPACINI29 (86 source JPEGs ~486 MB total, 86/86 OK, job wall **156.5 s**): file read **1.8 s summed** (~1.2% counted serially, <0.5% wall at the 8-wide `Parallel.For` fan-out — all a bytes-carry saves, since it still decodes from memory), decode **21.3 s summed CPU** (~2–3 s wall — all a decoded-`Image<Rgba32>` carry could save, at ~16 MB/image unbounded RAM spike + pixel drift vs. the JPEG on disk). Neither saving justifies the memory risk the jbtodo flagged. No production code changed; instrumentation reverted. Decision recorded in `PRISM-io-import.md` ("Import→Match Handoff: Disk Is the Contract"); root jbtodo block closed (commit cd4bc59).
**Review:** Approve (2026-07-15)

---

### T-3600 · Matching's HTTP contract silently assumes a shared filesystem with Import
**Status:** Done (2026-07-15) | **Profile:** P4-critical-architecture
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

**Direction + partial progress (2026-07-15, user):** the central design question is **decided**: Ingress + Matching + Export are **always co-deployed on one physical system**; only Transform/Generate/Upscale vary per public route. This selects **option (b)** — no ship-bytes-over-the-wire work; the core needs no cross-host shared filesystem because ingress and matching run in one process sharing one job temp folder. Confirmed in code that URL ingress is fully implemented (`FetchDispatcher` + Dropbox/WeTransfer/HTTPS fetchers → `SourceKind = RemoteUrl`), and lives in the **API host** (`PrismProcessIngressReader`), not the standalone `Prism.ServiceHost` matching route. First slice of the "documented deliberate answer" acceptance landed: a **Core vs. Features** section added to `jb/docs/PRISM-overview.md` (core = aggregation+normalize+match+order+export fed by URL/upload; features = Transform/Generate/Upscale; ServiceHost split is feature-only). **Remaining for this ticket:** fold the same statement into `PRISM-io-import.md` + `AGENTFEEDBACK.md`, and add the startup check / clear failure mode when a Matching host can't read `NormalizedJpgPath` (covered by [[T-3300]]'s planned real-HTTP tests).

**Completed (2026-07-15):** remaining scope landed — `PRISM-io-import.md` gained a "Co-Deployment Contract" section, `AGENTFEEDBACK.md` a core co-deployment Behavioral Memory bullet, and `MatchingService.MatchAsync` now throws an explicit `InvalidOperationException` (co-deployment message, not `PrismConfigurationException` — deployment topology, not config) when OK images exist but `IngestResult.JobTempFolder` is unreadable, replacing misleading per-image `CLASSIFY_ERROR` KOs. Covered by `Match/MatchingCoDeploymentGuardTests.cs` (Match suite 56/56 green; build 0 errors, only pre-existing warnings). Real-HTTP roundtrip coverage stays with [[T-3300]] as ticketed.
**Review:** Approve (2026-07-15)

---

### T-4100 · Investigate real GPU vs CPU ONNX behavior: health reports CPU-only on a GPU dev machine
**Status:** Done (2026-07-15) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-15)
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

**Findings + fix (2026-07-15):** the trigger was a **hardcoded lie**, not a GPU regression. The health endpoint set `SupportedRuntimeProviders = ["CPU"]` as a string literal (`api/Program.cs:52`) — it never queried ONNX, so the "CPU-only" report proved nothing. Actual providers (verified by reading the session-creation code): **CLIP** appends the DirectML EP gated on a hardware DX12 adapter (`ImageClassifier.cs:108-111`) → GPU here; **Upscaler** likewise (`Upscaler_g_p_u.cs:60-62`) → GPU here; **YOLO** appends no EP (`YoloDetector.cs:65`) → always CPU. **Fix:** `SupportedRuntimeProviders` now = `OrtEnv.Instance().GetAvailableProviders()`, plus a new `SessionRuntimeProviders` field reporting per-session usage (`api/RuntimeProviderProbe.cs`, reusing public `ImageUpscaler.IsGpuAvailable`). **Verified live** on the dev box: `SupportedRuntimeProviders = [DmlExecutionProvider, CPUExecutionProvider]`, `SessionRuntimeProviders = [CLIP=DirectML(GPU), YOLO=CPU, Upscale=DirectML(GPU)]`. Memory `project_local_gpu_verification` updated. Build 0 errors, 370 tests pass.

**Surfaced, NOT changed (deliberate — need own follow-up):** (1) **ONNX version skew** — Classify pins `Microsoft.ML.OnnxRuntime.DirectML 1.20.1`, Upscale pins `1.24.4` (two ORT runtimes in one process); aligning must be paired with a full CiMini re-verify since it can perturb CLIP numerics (relevant to [[T-2820]]). (2) **YOLO CPU-only** — deliberate per baseline policy, but a possible GPU-speed opportunity. A formal CPU-vs-GPU classify timing delta was not measured (GPU use is confirmed active; forcing CPU to benchmark is follow-up work). Recommend a small follow-up ticket for the version alignment specifically.

---


### T-3700 · Align project/assembly names, solution structure, and test namespaces with the Services/ restructure
**Status:** Done (2026-07-15) | **Profile:** P4-critical-architecture
**Review:** Approve (2026-07-15)
**Found by:** namespacing audit, 2026-07-10.

**Problem:** The 2026-07-08 core restructure renamed folders and C# namespaces to `Services/`/`Prism.Services.*` + `lib/`/`Prism.Lib.*`, but several identifiers were never updated, so the same project now answers to 2-3 different names depending on where you look. Confirmed by direct inspection of every `namespace` declaration, every `.csproj`, and `PRISM.sln`:

1. **Project/assembly identity mismatch.** `Prism.Core.Images.Classify.csproj`, `Prism.Core.Images.Transform.csproj`, `Prism.Core.Images.Upscale.csproj` never had their file name/assembly name updated. Each now has three different names for the same project: assembly `Prism.Core.Images.Transform.dll`, namespace `Prism.Services.Transform`, folder `Services/Transform/Engine/` (same pattern for Classify → `Prism.Services.Matching`, Upscale → `Prism.Services.Upscale`). Neither `<AssemblyName>` nor `<RootNamespace>` is set explicitly in any of the three, so both default to the stale file name.
2. **Stale solution-folder hierarchy.** `PRISM.sln` still nests these three projects under solution folder `core > Images > Transform` / `core > Images > Classify` — a Visual Studio artifact left over from the pre-restructure `jb/src/core/Images/` layout, not the real `Services/` layout.
3. **Upscale invisible in the solution.** `Prism.Core.Images.Upscale.csproj` has no `Project(...)` entry in `PRISM.sln` at all — it only builds because `Prism.Core.csproj` references it directly via `<ProjectReference>`. Its sibling engine projects (Classify, Transform) do have solution entries; Upscale doesn't, for no documented reason.
4. **CLAUDE.md's project list is stale.** The solution-project list in CLAUDE.md's Architecture section names Contracts/Core/Images.Classify/Images.Transform/Api (Workbench.Wpf was removed 2026-07-10 along with the WPF workbench itself), but omits `Prism.Core.Images.Upscale`, `Prism.Core.Tests`, and `Prism.ServiceHost` — all three real and already part of the tree (Tests and ServiceHost even have `PRISM.sln` entries). `jb/docs/PRISM-transform-generate.md` also has one stale example path (`Images/Upscale/ONNX/...` instead of the actual `Services/Upscale/Engine/ONNX/...` used by `Prism_Config.json` and `PrismConfigLocator`).

**Correction (2026-07-14):** this ticket previously carried a fifth item — the Analyzers test-namespace break (`namespace Prism.Core.Tests.Analyzers;` instead of `PrismCoreTests.Analyzers`), which made `--filter "FullyQualifiedName~PrismCoreTests.Analyzers"` match zero tests. **That item is already fixed** and is no longer part of this ticket: all four files in `jb/src/tests/Prism.Core.Tests/Analyzers/` (`YoloDetectorTests.cs`, `VisualAnalyzerTests.cs`, `ProductTypeResolverTests.cs`, plus `AnalyzerConfigTests.cs` added by T-4300) now declare `namespace PrismCoreTests.Analyzers;`. Fixed incidentally by commit `c16ec50` ("align tests with namespace refactoring"), not by this ticket. The bug no longer reproduces — do not "re-fix" it. What remains here is the pure project/solution rename.

**Confirmed blast radius (re-measured 2026-07-14):** 5 files repo-wide contain the literal string `Prism.Core.Images` (excluding the ticket board itself) — `Prism.Core.csproj` (3 `<ProjectReference>` paths), `PRISM.sln`, `CLAUDE.md`, `jb/docs/PRISM-transform-generate.md`, plus 1 doc-comment in `Tx_DetailCropper.cs` that references the project name descriptively. `CropTransformSettings.cs` no longer mentions it — T-4530 rewrote that file during the ConfigLoader migration, so it has dropped out of scope. No CI workflow, PowerShell script, or test infra hardcodes these names (checked `.github/workflows/*.yml`, `test/ci/`). Model-asset resolution (`PrismConfigLocator.FindModelAsset`, `Prism_Config.json`'s `Models` section, `PrismConfiguration.cs`, `FeatureAnalysisService.cs`) already uses the correct `Services/...` paths — not part of this bug, already fixed in the original restructure. This is a build-graph/text rename with no runtime behavior change.

**What to do:**
1. Rename the three engine `.csproj` files to match their real namespace: `Prism.Core.Images.Classify.csproj` → `Prism.Services.Matching.Classify.csproj`, `Prism.Core.Images.Transform.csproj` → `Prism.Services.Transform.csproj`, `Prism.Core.Images.Upscale.csproj` → `Prism.Services.Upscale.csproj`. Update the 3 `<ProjectReference>` paths in `Prism.Core.csproj` accordingly.
2. Update `PRISM.sln`: rename the 3 project entries to their new names/paths, add the missing Upscale project entry, and replace the stale `Images` solution folder with one that mirrors the real `Services/` layout.
3. Update the doc-comment mention in `Tx_DetailCropper.cs` to the new project name.
4. Update CLAUDE.md's Architecture/Solution project list to name every project actually in the tree (add Upscale, Tests, ServiceHost), and fix the one stale path example in `PRISM-transform-generate.md`.
5. Do **not** touch `Prism.Contracts`-namespaced files that live outside `Models/` (e.g. `OrderEvidence.cs`, `MatchEvidence.cs`, `ImageFeatureSnapshot.cs`) — that cross-folder namespace is deliberate (`Prism.Core.Contracts.csproj` cherry-picks files by relative path regardless of physical location). Don't "fix" these into folder-matching namespaces.

**Verification:**
- `dotnet build jb/src/PRISM.sln` → 0 errors / 0 warnings, same as before the rename.
- `dotnet sln jb/src/PRISM.sln list` shows all real projects, including the 3 renamed ones and the previously-missing Upscale entry.
- Full existing suite (`dotnet test jb/src/tests/Prism.Core.Tests/Prism.Core.Tests.csproj`) has the same pass count before and after — pure identity rename, nothing should newly pass or fail.
- `git grep -n "Prism.Core.Images"` returns zero hits repo-wide.
- Open `PRISM.sln` (Visual Studio or `dotnet sln list`) and confirm the solution-folder hierarchy matches the physical `Services/`/`lib/` layout — no leftover `Images` grouping.
- `pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` still produces the existing `expected-manifest.json` unchanged (proves the rename didn't alter runtime behavior).

**Files:** `jb/src/core/Services/Matching/Classify/Prism.Core.Images.Classify.csproj`, `jb/src/core/Services/Transform/Engine/Prism.Core.Images.Transform.csproj`, `jb/src/core/Services/Upscale/Engine/Prism.Core.Images.Upscale.csproj`, `jb/src/core/Prism.Core.csproj`, `jb/src/PRISM.sln`, `jb/src/core/Services/Transform/Engine/Tx_DetailCropper.cs`, `CLAUDE.md`, `jb/docs/PRISM-transform-generate.md`.

**Done (2026-07-15):** all four items implemented. (1) Three engine `.csproj` renamed via `git mv` → `Prism.Services.Matching.Classify.csproj` / `Prism.Services.Transform.csproj` / `Prism.Services.Upscale.csproj`; the 3 `<ProjectReference>` paths in `Prism.Core.csproj` updated. (2) `PRISM.sln`: 3 project entries renamed, missing **Upscale project entry added** (with config-platforms + nesting), stale `Images` solution folder replaced by `Services` mirroring the real layout (`Services > Matching/Transform/Upscale`). (3) `Tx_DetailCropper.cs` doc-comment updated. (4) `CLAUDE.md` project list now names all 8 projects (added Upscale/Tests/ServiceHost, dropped the stale "not in .sln" caveat); `PRISM-transform-generate.md` `Prism.Core.Images.Upscale` mention fixed. **Verification:** `dotnet build jb/src/PRISM.sln` 0 errors / 2 pre-existing warnings; `dotnet sln list` shows all 8 incl. renamed 3 + Upscale; `git grep "Prism.Core.Images"` = 0 in code/config/docs (ticket board excepted); **370 tests pass** (same as before); CiMini Full still produces the golden manifest unchanged. Pure identity rename, no runtime change. **Ready for review.**

---


### T-2830 · `_det#` numbering starts at det8 instead of the documented zero-based det0
**Status:** Done (2026-07-15) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-15)
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

**Note (2026-07-14, [[T-4560]] verification):** appears **already fixed**. The CiMini evidence run numbered from zero — `90861025_det0.jpg`, and family `94613033` → det0/det1/det2 — which is exactly the documented target. Confirm on a fresh run before closing; nothing in this repo's history explicitly claims the fix.

**Direction (2026-07-14, user):** ordering also depends on **phenotypes**, which are still only half implemented — so the det index that comes out of the spec'd ordering pass can legitimately leave gaps while that work is incomplete. Consider a **final collapse pass**: after ordering runs per spec, renumber each family's assigned slots down to a contiguous `det0..detN` with no gaps. Make it **toggle-able** via config — the pre-collapse numbering is the one that carries ImageRole/slot meaning, and we may want to see it raw.

**Resolution (2026-07-15):** the requested toggle-able collapse pass **already exists and is verified** — no new code needed. `ImageOrderer.CompactDetOrder` (`ImageOrderer.cs:44`) renumbers each family to contiguous `det0..detN` (renumber only, never reorder), called from `Exporter.Run` + the MatchLite/MatchOnly `PrismService` paths, gated by the toggle `Output.DET-ORDER-GAPS-ALLOWED` (`Prism_Config.json`, currently `false` = collapse on). Fresh CiMini run confirms det0-based per-family numbering (golden already encodes it, e.g. family `94613033` → det0/det1/det2). Docs reconciled: `jb/docs/PRISM-order-rename.md` now names the method and adds the phenotype caveat (contiguous numbering reflects overflow order until phenotypes fire — not an ordering bug); `CLAUDE.md` already agreed. `Order/jbtodo.md` removed (decision moved to the doc, file empty). **Ready to close.**

---


### T-2820 · Ordered stage assigns non-deterministic det-slots for tied images within a family
**Status:** Done (2026-07-15) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-15)
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

**Note (2026-07-14, [[T-4560]] verification):** did **not** reproduce. Three consecutive `-Mode Full -Dataset CiMini` runs on an unchanged tree produced byte-identical `DetOrder`/`FinalFileName`, all matching golden. Three runs cannot prove an intermittent race is gone — a coin can land the same way three times — so the ticket stays open, but re-verify (5+ runs, per Acceptance) before spending effort on a fix.

**Direction (2026-07-14, user):** the fix likely lives in **CLIP refinement**, not in a tie-break hack. If two images in a family score near-identically, that is the classifier failing to distinguish them; a deterministic secondary key would only freeze an arbitrary answer in place. Look at (a) the model side, (b) the CLIP prompts (`ClipPrompts.json`), and (c) the PRISM config values — thresholds in particular — before adding tie-break machinery.

**Verification (2026-07-15):** ran **5 consecutive `-Mode Full -Dataset CiMini` runs on an unchanged build** — all 5 byte-identical to golden (14/14 Ok every run, incl. tied families `94613033` and `90861083`). The ticket's Acceptance bar (5 consecutive identical runs) is **met**; the bug does **not reproduce** on the current build. Confirmed why: `ImageOrderer.CompareCandidates` (`ImageOrderer.cs:253`) already tie-breaks on `string.CompareOrdinal(Filename)` before `SourceIndex`, so exact ties are deterministic and input-order-independent. The residual theoretical risk (CLIP/NGP confidences differing by GPU float noise → *near*-ties that flip ordering before the filename key engages) did not manifest across 5 runs. Note this is now consistent with the T-4100 finding that CLIP genuinely runs on DirectML/GPU here. **Recommended disposition:** close as "acceptance met, not reproducing" OR keep as a low-priority watch; pursue the CLIP-refinement direction only if/when it recurs. Orchestrator/user decision.

**Closed (2026-07-15, user):** closing as acceptance-met / not-reproducing. User will signal to refine (CLIP-refinement direction) in the future if it recurs — no watch kept for now.

---



### T-4500 · Master: generic ConfigLoader + Transform cleanup (waves T-4510…T-4560)
**Status:** Done (2026-07-14) | **Profile:** P4-critical-architecture
**Review:** Approve (2026-07-14) — index ticket, no diff of its own. Every child was individually reviewed: T-4510/T-4520/T-4530/T-4540 Approve (2026-07-12), T-4550 Approve (2026-07-14), T-4560 Approve (2026-07-14).

Master/index ticket for the approved 2026-07-12 plan: replace the per-config `Load()` pattern AND `PrismConfigLocator` with one generic section-aware **ConfigLoader**, clean up the Transform folder layout, delete `BackgroundType`, and fold `ImageTransformationResult` into the record lifecycle (`Base → INPUT → LAMBDA → OUTPUT`).

All six children Done:
- Wave 1: [[T-4510]] ConfigLoader core ∥ [[T-4520]] Transform layout + dead code
- Wave 2: [[T-4530]] Transform adoption ∥ [[T-4540]] Analyzers adoption
- Wave 3: [[T-4550]] OUTPUT record merge (commit `d5c2727`)
- Wave 4: [[T-4560]] rest-of-PRISM migration + retire PrismConfigLocator/ConfigCache (commit `5e98be0`)

**Master-level gate — all passed (2026-07-14, final state = `5e98be0`):**
- `dotnet build jb/src/PRISM.sln` → 0 errors. (2 warnings, `CS0414 MatchingService._disposed` + `CS8602`, are pre-existing at HEAD in untouched code — not introduced by this work. Worth a follow-up.)
- Full suite: **370 passed / 0 failed.**
- API startup fail-loud check: misspelling `FeatherPx` in `transform_Config.json` stops startup with `Prism.Core.PrismConfigurationException: Cannot load section 'BgStretch' … missing required properties including: 'FeatherPx'`.
- `pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` → PASSED, 14 sources match golden, 14 Ok.
- Evidence run (non-vacuous transformed output): **14/14 manifest rows** carry `TransformerType` + `TransformationStatus` sourced from `ImageRecord_OUTPUT` — 9× `Tx_CropSquare`, 5× `Tx_CenterAndStretch`, all `Ok`.

**Incidental finding worth keeping:** [[T-2820]] (non-deterministic det-slots for tied images) **did not reproduce**. Three consecutive `-Mode Full -Dataset CiMini` runs on an unchanged build produced byte-identical `DetOrder`/`FinalFileName`, matching the golden every time. That made the T-4560 identity check a strict golden match rather than a fuzzy diff. Three runs cannot prove an intermittent race is gone — but T-2820's stated repro no longer reproduces, so re-verify before spending effort on it. Related: `_det` numbering now starts at `det0` (see the evidence table above), which is also what [[T-2830]] asks for — re-check that ticket too before working it.

**Files:** index only — see child tickets.

---


### T-4560 · Migrate remaining PRISM to ConfigLoader; retire PrismConfigLocator + ConfigCache
**Status:** Done (2026-07-14) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-14)

Migrated all 23 `PrismConfigLocator` and all 9 `ConfigCache` call sites to `ConfigLoader.RequireFile`/`Section<T>`/`Root<T>` and `ModelAssetLocator.Find`; deleted `PrismConfigLocator.cs` and `ConfigCache.cs`. `git grep` returns zero source hits. Commit `5e98be0`.

**Decision — ConfigCache deleted with NO replacement (do not re-add a config cache).** It memoized the hand-written `Load(path)` parsers, but the memoization was measured and found worthless: all config JSON in the project totals **62 KB**, and every one of those sites fires **once per job, never per image** (`ImageMatcher.Run` is a static per-job method; `MatchingService` constructs its sub-services once per job; `TransformService` bundles once per stage run). Config parsing is on the order of **0.01%** of a job that runs CLIP + YOLO per image plus Real-ESRGAN. Those sites now call their parser directly. Recorded in `jb/docs/PRISM-pipeline-core.md`.
`ConfigLoader`'s **own** internal cache stays — a different thing. The two fixed-signature engine webservice entry points (`Tx_util_BgStretch.Process`, `Tx_LowContrastEnhancement`) self-load per call, and that one *is* the per-image path.

**Scope widened mid-ticket (user-approved): one exception type for config.** `PrismConfigurationException` is now the single fail-loud type for every config failure — `ConfigLoader`'s own throws plus ~45 across every section class's `Validate()` and every hand-written parser (Excel, Analyzers, Classify, Match, Order, Transform/Admin, Upscale). It derives from `InvalidOperationException`, so `catch` sites are unaffected — **but xUnit's `Assert.Throws<T>` is exact-type**, so config tests now assert the precise type (this is what caught the change; 8 tests failed until updated). Non-config runtime failures deliberately keep `InvalidOperationException`: image-too-small, HTTP/WeTransfer fetch, `ServiceHttp`, the `Upscaler_g_p_u.Initialize()` lifecycle guard, and `ExcelFileHandler`'s user-workbook parsing (a bad user workbook is not a deployment fault).

**Also:** `Prism.Core.Images.Upscale.csproj` had **no** `ProjectReference` to `Prism.Core.Contracts` and only compiled transitively — it now references it directly (no cycle; Contracts has no outbound references). `Prism.Config` added to the 4 GlobalUsings shims. `PrismConfiguration.FileName` const replaces the repeated `"Prism_Config.json"` literal.

**Acceptance — all met:** zero `git grep` source hits; build 0 errors; suite 370 passed / 0 failed (same count — identity migration); CiMini Full byte-identical to the pre-change 3-run baseline including `DetOrder`.

**Files:** `jb/src/core/config/*`, all call sites, ~20 config classes, 3 engine csprojs, 4 GlobalUsings, `CLAUDE.md`, `jb/docs/PRISM-pipeline-core.md`, `jb/docs/PRISM-transform-generate.md`, `test/ci/README.md`.

---


### T-4550 · Fold ImageTransformationResult into ImageRecord_OUTPUT (Base→INPUT→LAMBDA→OUTPUT)
**Status:** Done (2026-07-14) | **Profile:** P4-critical-architecture
**Review:** Approve (2026-07-14)

Folded `ImageTransformationResult` into `ImageRecord_OUTPUT`, completing the record lifecycle. Commit `d5c2727`.

`ImageRecord_OUTPUT` now carries a **transform block** and an **export block**. Transform creates the record and fills the transform block; Export enriches that same instance with the export block and re-copies the identity fields (`CompactDetOrder` may have renumbered `_det` since Transform ran). Deleted `Engine/ImageTransformationResult.cs`, its Contracts csproj link, and `ImageRecord_LAMBDA.TransformationResult`.

**Design decisions (all reviewer-confirmed):**
- Property is `TransformStatus`, not `Status` — the record already inherits `ImportStatus` and carries `ExportStatus`, so a bare `Status` is ambiguous. It is **nullable**, so "transform never evaluated this image" stays distinguishable from the enum's `NotEvaluated` default; this preserves `Exporter.BuildTransformStep`'s `?? (IsKo ? "Skipped" : "Ok")` fallback exactly.
- Props are `get; set;` not `init` — two stages write the record now.
- `Tx_*` classes do **not** set the identity fields; Export owns them. Verified by tracing every reader: nothing reads identity off a KO-at-transform record (`ManifestImageRow` takes identity from `lambda.*`, `ImageJourneyItem.Output` is null for KO).
- Field initializers kept (`string.Empty`, `1.0`, `[]`) — carried over verbatim. The no-shadow-defaults rule scopes to **config classes**, not contract/model records; changing these would change manifest output.

**Acceptance — met:** build 0/0; suite 370 passed / 0 failed (incl. 2 new Export tests covering the two-writer contract — the regression this fold could silently introduce); CiMini evidence run confirms 14/14 manifest rows carry `TransformerType` + `TransformationStatus` from `OutputRecord` (9× `Tx_CropSquare`, 5× `Tx_CenterAndStretch`, all Ok).

**Files:** `jb/src/core/Models/ImageRecord_OUTPUT.cs`, `ImageRecord_LAMBDA.cs`, `Prism.Core.Contracts.csproj`, `Services/Transform/**`, `lib/Export/Exporter.cs`, Transform/Export tests, `jb/docs/{GLOSSARY,PRISM-models,PRISM-index,PRISM-knowledge-base,PRISM-transform-generate,PRISM-workbench}.md`.

---


### T-3400 · Web workbench: dark mode, layout compaction, import/export feedback
**Status:** Done (2026-07-14) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-14)
**Tracks:** root `jbtodo.md` — web-workbench refinement, triaged 2026-07-10.

**Outcome (2026-07-14):** implemented in `403ed16`; review found the dark theme shipped three theme-bypassing hardcoded colors, fixed in `f9df410`:
- `.workbench-shell-dragging .drop-zone` hardcoded `#fff5e7`, so the drag-over title (inheriting near-white `--prism-color-ink`) rendered at ~1:1 contrast in dark mode — effectively invisible. Now `var(--prism-color-surface-strong)`: 13.5:1 light / 8.9:1 dark.
- `.drop-zone` grid pattern hardcoded the *light* accent teal at 8% opacity, rendering the grid invisible on the dark surface. Now `var(--prism-color-line)`.
- `.error-detail` hardcoded `rgba(255,255,255,0.64)`. Now `var(--prism-color-surface)`.

The dark *palette* itself was complete throughout — all 15 semantic tokens are mirrored across the light `:root`, `@media (prefers-color-scheme: dark)`, and both `[data-theme]` blocks. The bugs were purely values that escaped the variable system.

**Accepted as-is (user decision, 2026-07-14):** `.primary-button`/`.action-button` use `color: white` on `--prism-color-accent` (pink-500 `#d43d78`) = 4.43:1 in dark mode, marginally under the 4.5:1 AA bar for normal text. Judged close enough; the accent is a brand color.

**Scope narrowed (user decision, 2026-07-14):** item 4's "accepted/rejected counts, blocked-vs-running" requirement is **not** met and was **not** achievable in this ticket. `StageProgress.EmitStarted` is the only place a `PipelineProgressEvent` is ever constructed, and it leaves `CompletedCount`/`TotalCount` null — so the SSE stream carries no such data, and T-3400's file list is web-only. T-3400 closes on the achievable bar (real stage name replaces the placeholder chips); the backend gap is now [[T-4600]].

**Delivered:** dark palette + `@media (prefers-color-scheme: dark)` + `[data-theme]` override pair in `PRISM-theme.css`; tri-state (auto/light/dark) header toggle in `WorkbenchShell.tsx` persisting to `localStorage` (auto correctly *removes* the attribute); `ResultSection` reordered above `RouteSection` and `RouteSection` bounded to one row per stage via `StageRouteList.tsx`; `StatusPanel.tsx` placeholder chips replaced with the real SSE stage name. No Upscale toggle added (negative constraint honored). `npm run typecheck` + `npm run build` green.

**Files:** `jb/src/workbench/web/styles/PRISM-theme.css`, `.../styles/workbench.css`, `.../sections/WorkbenchShell.tsx`, `.../components/StatusPanel.tsx`, `.../sections/StageRouteList.tsx`, `.../sections/UploadSection.tsx`.

---


### T-3900 · Order: `DetermineTieBreaker` rescan can mislabel the deciding tiebreaker
**Status:** Done (2026-07-13) | **Profile:** P1-feature-worker
**Tracks:** `jb/src/core/Services/Matching/Order/jbtodo.md` (triaged 2026-07-11).
**Review:** Approve (2026-07-13)

**Problem:** After a winning image is assigned a det slot, `ImageOrderer.DetermineTieBreaker` rescans the *entire* candidate list for the family to find competitors (same slot, same phenotype rank) and reports the first tiebreaker level where *any* competitor differs from the winner. With 3+ competitors losing for different reasons, this can name the wrong tiebreaker as the deciding one — e.g. it reports "ngp-confidence" because a clearly-losing competitor differs on confidence, when the real closest competitor actually lost on the filename-hint tiebreaker instead. Does not affect the actual `DetOrder` assigned — only the `OrderEvidence.TieBreakerWon` diagnostic text, so this is a manifest-readability/debugging issue, not an output-correctness bug.

**Resolution (2026-07-13):** `DetermineTieBreaker(candidates, winnerIndex, imageAssigned)` scans forward from the winner within its contiguous slot+phenotype-rank block and compares it against the first still-unassigned rival — the immediate runner-up — walking the same level chain as `CompareCandidates`. The full-list rescan is gone as a side effect. Two labels the old chain could not express were added: `filename-ordinal` (the sort compares filenames before source index since T-2820, but the labeller jumped straight to `source-index`), and `none` for a slot whose only other candidates already hold an earlier slot (they left the race when assigned; the old rescan still reported them as beaten rivals). Decision documented in `jb/docs/PRISM-order-rename.md` (Step 4 + "Which tie-breaker the evidence names"); source todo block removed.

**Acceptance:**
- `OrderEvidence.TieBreakerWon` names the tiebreaker that actually decided against the true closest competitor, verified against the counter-example in the source `jbtodo.md` (winner NgpConfidence=5/HintScore=1 vs. a tied-confidence/lower-hint true competitor plus an unrelated lower-confidence non-competitor). ✅ `Run_TieBreaker_NamesTheLevelThatBeatTheClosestRival_NotAFarBehindCompetitor` — reports `filename-hint`; the pre-fix code reports `ngp-confidence`.
- `DetOrder` output unchanged (this is a diagnostic-only fix) — confirm via existing `ImageOrdererTests.cs`. ✅ Full suite 367 passed / 0 failed; every pre-existing DetOrder assertion holds.
- Verification beyond the ticket: reverting `ImageOrderer.cs` to HEAD with the new tests in place fails exactly the 3 tests that encode the bug and passes the 3 that encode already-correct labelling; deleting the `imageAssigned` guard fails exactly `Run_TieBreaker_AlreadyAssignedRivalInsideTheBlock_IsSkippedNotReported` (added after review found that branch uncovered).

**Files:** `jb/src/core/Services/Matching/ImageOrderer.cs`, `jb/src/core/Services/Matching/Order/OrderEvidence.cs`, `jb/src/core/Services/Matching/Order/jbtodo.md`, `jb/src/tests/Prism.Core.Tests/Order/ImageOrdererTests.cs`, `jb/docs/PRISM-order-rename.md`.

---

### T-4540 · Analyzers adopt ConfigLoader; root AnalyzerConfig dissolves
**Status:** Done (2026-07-12) | **Profile:** P1-feature-worker
**Found by:** [[T-4500]] (Wave 2, parallel with [[T-4530]]) — unblocked, [[T-4510]] reviewed Approve 2026-07-12.

`FeatureAnalysisService` loads `analyzer_Config.json` sections via `ConfigLoader.Section<T>` instead of `AnalyzerConfig.Load` + `PrismConfigLocator`/`ConfigCache`. Per-section validation moves from `AnalyzerConfig.Validate` into the 9 section `*Config.cs` classes as `IValidatableConfig`; root `AnalyzerConfig.cs` dissolves; `PrismApiConfiguration.Load()` startup validation updated likewise. `analyzer_Config.json` content unchanged.

**Design decision (user, 2026-07-12) — same two-phase shape as [[T-4530]].** `AnalyzerConfig` was three things fused: a deserialization target, a validator, and the parameter bundle threaded into `ImageFeatureAnalyzer.Analyze/Refine`. The ticket dissolves the first two; the third stays, rebuilt as a *composed* type — `AnalyzerParameters` (new, `Analyzers/AnalyzerParameters.cs`), built by `AnalyzerParameters.FromConfig()` (phase 1: `ConfigLoader.Section<T>` per section, each self-validating; phase 2: compose). `FeatureAnalysisService` builds it once in its constructor (so a bad config still kills the host at startup, not mid-job) and passes it down; `ImageFeatureAnalyzer`'s signatures keep their existing arity (`Analyze` 3 params, `Refine` 7) with `AnalyzerParameters` in place of `AnalyzerConfig`. Rejected alternatives: threading the 8 sections as individual parameters (would blow `Refine` out to 12 params), and having `ImageFeatureAnalyzer` self-load each section at its call site (hides the dependency; puts two syscalls per section inside the per-image path). `AnalyzerParameters` is not an `AnalyzerConfig` rename: not JSON-bound, owns no loading and no validation, and every section stays independently loadable without it.

**Acceptance:** build + full suite green (incl. `AnalyzerConfigTests` reworked to per-section loading); startup fail-loud check on a misspelled analyzer key.

**Verified 2026-07-12:** `dotnet build jb/src/PRISM.sln` clean (2 pre-existing warnings, untouched files). Full suite **364 passed / 0 failed**. Startup fail-loud: misspelling `HeroPersonMinArea` → API refuses to boot with *"Cannot load section 'Yolo' of …/analyzer_Config.json: JSON deserialization for type 'Prism.Services.Matching.YoloAnalyzerConfig' was missing required properties including: 'HeroPersonMinArea'"*; restored, `analyzer_Config.json` byte-identical to HEAD.

**Review: Approve (2026-07-12)** — commit `cab930e`. Reviewer diffed all 23 predicates of the deleted root `AnalyzerConfig.Validate()` against the 8 new section `Validate()` methods: every bound, message, and field preserved 1:1, no checks dropped, and the previously-unvalidated leaf fields (`IsIllustration.WhiteChannelMin`, `MinClusterPopulation`, `SubjectGeometry.MinForegroundFraction`/`FallbackConfidence`, the `*.Confidence` fields) remain unvalidated — no checks invented. Fail-fast confirmed at both hosts; `analyzer_Config.json` byte-identical. One non-blocking warning, fixed in follow-up commit: `Analyzers/jbtodo.md` still carried an OPEN todo proposing to centralize the `*Config.cs` classes into a single `AnalyzerConfig.cs` — the exact architecture this ticket removed. Todo closed: decision written to `jb/docs/PRISM-pipeline-core.md` (Configuration Lifecycle → "Loading is two phases"), block removed.

**Files:** `jb/src/core/Services/Matching/FeatureAnalysisService.cs`, `jb/src/core/Services/Matching/Analyzers/*Config.cs`, `jb/src/core/Services/Matching/Analyzers/AnalyzerConfig.cs`, `jb/src/api/PrismApiConfiguration.cs`, `jb/src/tests/Prism.Core.Tests/Analyzers/*`.

---


### T-4530 · Transform adopts ConfigLoader; delete Configure() push-in; migrate CropTransformSettings
**Status:** Done (2026-07-12) | **Profile:** P1-feature-worker
**Found by:** [[T-4500]] (Wave 2, parallel with [[T-4540]]) — unblocked, [[T-4510]] reviewed Approve 2026-07-12.

- `TransformService` drops `PrismConfigLocator`/`ConfigCache`/`TransformConfig.Load`; consumers get sections via `ConfigLoader.Section<T>("transform_Config.json", "…")`.
- `Tx_util_BgStretch` / `Tx_LowContrastEnhancement` self-load their section lazily inside the engine (now reachable) → delete `Configure()`, `ResetConfigureForTests()`, `TxConfigureGateTests` (current form), `[Collection("TxStaticConfig")]` on `PipelineIntegrationTests`, and the temporal-coupling landmine note in `Engine/jbtodo.md`.
- **CropTransformSettings migration:** its 4 values move from `Prism_Config.json` (`Transformation.Positioning/Cropping`) into a new `"Crop"` section of `transform_Config.json`; `CropTransformSettings` becomes a `required`-props section class implementing `IValidatableConfig` (ranges from `PrismConfiguration.cs:265-268`); remove the 4 properties + parsing + asserts from `PrismConfiguration.cs` and the keys from `Prism_Config.json`.
- Root `TransformConfig.cs` dissolves (sections load independently; its per-section `Validate` checks move into each section class); `PrismApiConfiguration.Load()` validates each transform section explicitly (fail-fast preserved).

**Design decision (user, 2026-07-12) — load and bundle are two phases, not one.** `TransformConfig` was three things fused: a deserialization target, a validator, and the parameter bundle carried into `ImageTransformer`. The ticket dissolves the first two; the third stays, rebuilt as a *composed* type. So: **phase 1** `ConfigLoader.Section<T>` loads each section independently, each self-validating via `IValidatableConfig`; **phase 2** the loaded sections are composed into `TransformParameters` (new, `Engine/TransformParameters.cs`) via `TransformParameters.FromConfig()`. `TransformService` builds the bundle once per stage run and passes it to `ImageTransformer.TransformImage(lambda, colorMat, headcut, parameters)`; `PrismApiConfiguration.Load()` calls `FromConfig()` as its startup gate. Rejected alternative: having `ImageTransformer` self-load each section at its call site — that hides the dependency and puts two syscalls per section inside the per-image `Parallel.ForEach`. Self-load survives **only** in the two fixed-signature webservice `Process(byte[], int, float)` entry points (`Tx_util_BgStretch`, `Tx_LowContrastEnhancement`), which have no parameter to pass config through — the original reason `Configure()` existed. `TransformParameters` is not a `TransformConfig` rename: it is not JSON-bound, owns no loading and no validation, and every section remains independently loadable without it (what T-4560 and per-section service hosts need). [[T-4540]] mirrors this shape.

**Acceptance:** build + full suite green (no `[Collection]` serialization needed); startup fail-loud check — misspell a key in `transform_Config.json`, `PrismApiConfiguration.Load()` throws naming it, restore; prism-evidence-report transform run shows real transformed output, not vacuous KOs.

**Verified 2026-07-12:** `dotnet build jb/src/PRISM.sln` clean (2 pre-existing warnings, untouched files). Full suite **361 passed / 0 failed**. Startup fail-loud: misspelling `FeatherPx` → API refuses to boot with *"Cannot load section 'BgStretch' of …/transform_Config.json: JSON deserialization for type 'Prism.Services.Transform.BgStretchConfig' was missing required properties including: 'FeatherPx'"*; restored. prism-evidence-report (CiMini, `transform`): **14/14 images Succeeded, 0 KO, 0 failed, 0 warnings** — 5× `Tx_CenterAndStretch` (background-stretch fill, scale 0.988–0.992 driven by the migrated `Crop.WhiteSpaceMargin`=0.042) and 9× `Tx_CropSquare`. Not vacuous. `Tx_DetailCropper` stays uncovered — `BypassPhenotypes` PoC gate, pre-existing.

**Review: Approve (2026-07-12)** — commit `4380cea`. Reviewer confirmed byte-exact preservation of the moved range checks (incl. `AssertInRange`'s inclusive bounds and the 0.49 margin cap), the migrated `Crop` values, no shadow defaults, and no unauthorized contract changes. Two non-blocking findings, both fixed in follow-up commit: (1) `jb/docs/PRISM-knowledge-base.md` still listed the deleted `Transformation.*` keys as live `Prism_Config.json` paths; (2) `Tx_LowContrastEnhancement.ApplyClahe` self-loaded its section and was shared by the dormant internal `Enhance()` — wiring `Enhance()` into `Tx_CenterAndStretch` (as its own doc comment invites) would have reintroduced per-image config loading inside the `Parallel.ForEach`, the exact anti-pattern this ticket's design decision rejects. The self-load is now confined to the webservice `Process()` body; `Enhance()` and `ApplyClahe` take config from the caller.

**Files:** `jb/src/core/Services/Transform/TransformService.cs`, `jb/src/core/Services/Transform/ImageTransformer.cs`, `jb/src/core/Services/Transform/Engine/*.cs`, `jb/src/core/config/transform_Config.json`, `jb/src/core/config/Prism_Config.json`, `jb/src/core/config/PrismConfiguration.cs`, `jb/src/api/PrismApiConfiguration.cs`, `jb/src/tests/Prism.Core.Tests/Transform/*`.

---


### T-4520 · Transform layout cleanup + delete dead BackgroundType
**Status:** Done (2026-07-12) | **Profile:** P1-feature-worker
**Found by:** [[T-4500]] (Wave 1, parallel with [[T-4510]]).
**Review:** Approve (2026-07-12)

- Move `Engine/TransformationStatus.cs` → `Transform/Enum/TransformationStatus.cs` (fix its `<Compile Link>` in `Prism.Core.Contracts.csproj`).
- Move `Engine/processingtools/Tx_LowContrastEnhancement.cs` → `Engine/Utils/` (fix `Prism.Core.Images.Transform.csproj`; delete empty `processingtools/`).
- `Tx_CenterAndStretch`/`Tx_CropSquare`/`Tx_DetailCropper`/`Tx_ProblemImageProcessor`/`Tx_util_BgStretch`/`Tx_util_HeadCutter` stay in `Engine/` (key human-developer files).
- Delete `Engine/BackgroundType.cs` + its Contracts csproj link. Verified dead: only references are the enum, the csproj link, and a test *method name*; runtime background typing already flows as the `"background-type"` feature-snapshot string (`ImageFeatureAnalyzer.AnalyzeBackground` → `Analyzer_Exposure`). Pure deletion, no rewiring needed.
- Delete `Services/Transform/DUMMY FOLDER/` — its goal.md content is captured in [[T-4500]].

**Acceptance:** build + full suite green; `git grep BackgroundType` returns only the unrelated test method name (rename it while there); no orphan csproj links.

**Files:** `jb/src/core/Services/Transform/Engine/TransformationStatus.cs`, `jb/src/core/Services/Transform/Engine/processingtools/Tx_LowContrastEnhancement.cs`, `jb/src/core/Services/Transform/Engine/BackgroundType.cs`, `jb/src/core/Models/Prism.Core.Contracts.csproj`, `jb/src/core/Services/Transform/Engine/Prism.Core.Images.Transform.csproj`.

---


### T-4510 · ConfigLoader core: section-aware JSON loading in the shared Contracts assembly
**Status:** Done (2026-07-12) | **Profile:** P4-critical-architecture
**Found by:** [[T-4500]] (Wave 1, parallel with [[T-4520]]).
**Review:** Approve (2026-07-12)

Create, in `jb/src/core/config/` (one type per file), namespace `Prism.Config`, compiled into `Prism.Core.Contracts.csproj` via `<Compile Link>`:
- **ConfigLoader.cs** — `T Section<T>(string configFileName, string sectionName)` (parses file once, deserializes ONLY that top-level section; missing section throws naming file + section + the sections that DO exist), `T Root<T>(string configFileName)`, `string RequireFile(string configFileName)` (discovery; throws listing every searched path). Discovery order ports `PrismConfigLocator`: `AppContext.BaseDirectory/config`, `AppContext.BaseDirectory`, cwd variants, source-tree walk-up to `jb/src/core/config/`. Serializer: `PropertyNameCaseInsensitive`, `ReadCommentHandling.Skip`, `required`-member enforcement (no-shadow-defaults core rule). Internal cache keyed `(type, path, section, LastWriteTimeUtc)` — absorbs `ConfigCache` semantics.
- **IValidatableConfig.cs** — `void Validate();` called by the loader after deserialize when implemented.
- **ModelAssetLocator.cs** — ports `FindModelAsset` (beside-config → `PRISM_ONNX_MODEL_DIR` → source-tree walk-up).

**Scope boundary:** NO adoption — no existing call site changes in this ticket. Replace the empty untracked `ConfigLoader.cs` placeholder with the real implementation.

**Acceptance:** new `ConfigLoaderTests` suite (`PrismCoreTests.Services`) covering: missing file lists searched paths; missing section names existing sections; misspelled key throws; comments + case-insensitivity accepted; unchanged file returns cached instance; touched timestamp re-parses; source-tree walk-up works; `IValidatableConfig.Validate` invoked and failures propagate. Build + full suite green.

**Files:** `jb/src/core/config/ConfigLoader.cs`, `jb/src/core/config/IValidatableConfig.cs`, `jb/src/core/config/ModelAssetLocator.cs`, `jb/src/core/Models/Prism.Core.Contracts.csproj`, `jb/src/tests/Prism.Core.Tests/Services/ConfigLoaderTests.cs`.

---


### T-4300 · Strip shadow defaults from Analyzer config classes: required keys, analyzer_Config.json is the only source
**Status:** Done (2026-07-12) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-12) — strip complete (verified by grep), analyzer_Config.json untouched, test values match JSON exactly; full suite 349/349 with both changesets
**Found by:** [[T-4200]] shadow-defaults policy decision (2026-07-12).

**Problem:** The Analyzer config classes carry in-code property initializers ("defaults mirror the previously hard-coded constants"). A missing or misspelled key in `analyzer_Config.json` silently falls back to the in-code value — two sources of truth, and the losing one wins silently. The shadow-defaults core rule (CLAUDE.md, Configuration-driven design) now forbids this for Transform and Analyzers.

**Done:** Every property in all 9 Analyzer config classes (root sections and Palette included) is `required` with zero initializers; `analyzer_Config.json` unchanged (already carried every key); Palette's OrdinalIgnoreCase comparer removal verified behavior-neutral (sole consumer enumerates only). `AnalyzerConfigTests` added: shipped-value fidelity, missing-file, missing-key, out-of-range. Implementation commit 7fbe938.

**Files:** `jb/src/core/Services/Matching/Analyzers/*Config.cs`, `jb/src/tests/Prism.Core.Tests/Analyzers/AnalyzerConfigTests.cs` (new), 3 Analyzers + 1 Classify test files (constructor call sites).

---


### T-4200 · Transform engine config retrofit: extract Tx_* empirical tunables to transform_Config.json
**Status:** Done (2026-07-12) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-12, 2nd round; 1st round Request Changes) — gate tests added, DetailCropper re-diff minimal, Configure() retained on assembly-boundary grounds (reviewer's lazy-load recommendation withdrawn after csproj verification); xUnit cross-collection race closed via shared [Collection("TxStaticConfig")]
**Found by:** 2026-07-11 config-rule audit (review-gap discussion) — Transform never got the config extraction the Analyzers got.

**Done:** All 11 empirical tunables moved to `transform_Config.json` (values byte-for-byte); 6 new `required`/no-default config classes; wired via ConfigCache like AnalyzerConfig + API startup validation; `Configure()` gate on the two fixed-signature webservice entry points is boundary-forced (Engine references only Contracts — self-load via Prism.Core types would be circular) and documented in `Engine/jbtodo.md` for the future webservice host. `TransformConfigTests` + `TxConfigureGateTests` added. Full suite 349/349. Implementation commit c0b1b42.

**Files:** `jb/src/core/Services/Transform/Engine/` (Tx_* + 6 config classes + AssemblyInfo), `jb/src/core/config/transform_Config.json`, `jb/src/core/Prism.Core.csproj`, `jb/src/api/PrismApiConfiguration.cs`, Transform test suite + `PipelineIntegrationTests.cs`.

---


### T-3200 · Close Services test coverage gaps: `IIngestService` IO/import path + `IArtifactStore`
**Status:** Done (2026-07-10) | **Profile:** P1-feature-worker
**Tracks:** `jb/src/core/Services/jbtodo.md` (per-service test suite todo, triaged 2026-07-07).

**Problem:** Existing test folders already mirror stage boundaries by namespace (`PrismCoreTests.Transform`, `.Match`, `.Classify`, etc.), so per-stage isolation already works today via `dotnet test --filter "FullyQualifiedName~PrismCoreTests.<Stage>"` — no restructuring needed for that. But two service interfaces have no real coverage:
1. `IIngestService` — `Excel/` tests only exercise Excel parsing/IEM building (`ModelBuilder*Tests.cs`). Nothing tests the IO/import side documented in `jb/docs/PRISM-io-import.md` and implemented in `jb/src/core/IO/Import/Importer.cs` — multipart, ZIP, URL, and stream ingestion paths.
2. `IArtifactStore` — `LocalArtifactStore` (`jb/src/core/Services/LocalArtifactStore.cs`) has no direct unit tests; it's only exercised indirectly through `Export/ExporterTests.cs`.

**What to do:**
1. Add a `jb/src/tests/Prism.Core.Tests/Ingest/` folder (namespace `PrismCoreTests.Ingest`) covering `Importer.cs`'s multipart, ZIP, URL, and stream code paths — success and malformed-input cases for each.
2. Add direct unit tests for `LocalArtifactStore`: put/get roundtrip, missing-key behavior, concurrent writes if applicable.
3. Keep the existing per-folder namespace convention consistent (`PrismCoreTests.<Folder>`).

**Acceptance:**
- New tests fail if the corresponding production code is reverted (real behavioral coverage, not vacuous passes).
- `dotnet test jb/src/tests/Prism.Core.Tests/Prism.Core.Tests.csproj` green.

**Done.** Added `jb/src/tests/Prism.Core.Tests/Ingest/` (`ImporterFixture.cs`, `ImporterDirectImageTests.cs`, `ImporterZipTests.cs`, `ImporterExcelRoutingTests.cs`, `LoopbackHttpServer.cs`, `FetcherTests.cs`) covering multipart/ZIP/URL/stream ingestion, and `Services/LocalArtifactStoreTests.cs` for direct `IArtifactStore` coverage. Closed the per-service test-suite `jbtodo.md`.

**Files:** `jb/src/tests/Prism.Core.Tests/Ingest/*.cs`, `jb/src/tests/Prism.Core.Tests/Services/LocalArtifactStoreTests.cs`, `jb/src/core/IO/Import/Importer.cs`, `jb/src/core/Services/LocalArtifactStore.cs`.

---


### T-3100 · Bracket 4 (SemanticMatcher) perf: skip without CLIP tags; index its string scoring
**Done.** `ImageMatcher.RunWaterfall` skips `RunBracket4` entirely when no record has an influential CLIP tag. `StringMatcher.ScoreCandidatesByStringTokens` rewritten to reuse Bracket 3's inverted token index instead of an un-indexed per-family scan. 18 tests. Verified identical `FamilyId` assignments with/without `--skip-classification` on real TinyTest data.

**Files:** `jb/src/core/Images/ImageMatcher.cs`, `jb/src/core/Images/Match/SemanticMatcher.cs`, `jb/src/core/Images/Match/StringMatcher.cs`

---

### T-3000 · Parallelize image import normalization
**Done.** Both image loops now normalize via `Parallel.ForEach` capped at `Environment.ProcessorCount`; result accumulation moved to `ConcurrentBag<T>`; filename-uniqueness index moved to a job-scoped `Interlocked` counter. Already-conforming JPEGs are copied unchanged instead of decoded/re-encoded. `jb/src/core/IO/Import/jbtodo.md` closed and removed.

**Files:** `jb/src/core/IO/Import/Importer.cs`

---

### T-2810 · PipelineIntegrationTests hard-depend on an uncommitted dataset
**Done.** `ResolveTestFixturePath()` rewritten to walk up to `test/datasets` keyed by the committed `CiMini` folder (no hardcoded path). All fixture references (`SPACINI29/TINY`, `SPACINI29-INPUTS.xlsx`, `SmallTest/*`) repointed to CiMini. CI `--filter` exclusion removed from `ci.yml`. Post-T-2800: all 12 `PipelineIntegrationTests` methods green with `Transform=true` against real CiMini fixture.

**Files:** `jb/src/tests/Prism.Core.Tests/PipelineIntegrationTests.cs`, `.github/workflows/ci.yml`

---

### T-2800 · API/in-process pipeline never initializes the GPU Real-ESRGAN upscaler
**Done.** `PipelineServiceFactory.CreateInProcess`/`CreateFromEnvironment` now call `UpscaleService.Create(configuration)` once (mirrors MatchingService/CLIP eager-init); missing model asset degrades to CPU. `Upscaler_g_p_u.Initialize` made idempotent, thread-safe (`_sessionLock`, serializes `session.Run()`) and non-throwing (`IsReady`); `ImageUpscaler.Upscale` routes to GPU only when hardware present *and* session loaded. Fix exposed second bug: committed model has fixed `[1,3,64,64]` input — added overlapping-tile inference (`RunTiled`/`RunSingleTile`, 8px border discard, shape from `session.InputMetadata`). 224/224 tests green (was 9 failing); live CiMini Full run via API completes with real GPU-tiled output. `expected-manifest.json` not committed — non-determinism filed as T-2820, det8 numbering as T-2830.

**Files:** `jb/src/core/Services/PipelineServiceFactory.cs`, `jb/src/core/Images/ImageUpscaler.cs`, `jb/src/core/Images/Upscale/Upscaler_g_p_u.cs`, `jb/src/tests/Prism.Core.Tests/Upscaler_g_p_uTests.cs`

---

### T-2700 · Wire fetcher strategies into API ingress
**Done.** `FetchDispatcher` created — ordered strategy list with `CanHandle`/`FetchAsync`. `AddRemoteInputRecords` made async; routes via dispatcher first (content-type based), falls back to URL extension. Dropbox folder ZIPs routed to `zipFiles`. `PrismApiConfiguration` carries `FetchDispatcher` instance.

---

### T-2500 · GPU upscaler (Real-ESRGAN via DirectML)
**Done.** `Upscaler_g_p_u.RunRealEsrgan` implemented: JPEG decode → BGR float32 NCHW [1,3,H,W] → `InferenceSession.Run` with DML EP → output [1,3,H×2,W×2] → clamp [0,1] → BGR uint8 → JPEG bytes. Model path from `Prism_Config.json Upscale.ModelPath`.

---

### T-2400 · Cross-bracket tie accumulator
**Done.** `RunWaterfall` maintains `crossBracketCandidates` (per-image `HashSet<string>`). Brackets 1+2 populate from `tiedCandidates`; Bracket 3 adds candidates rejected by duplicate-phenotype guard. `KoUnmatched` emits `MATCHES_MULTIPLE_FAMILYIDS` (≥2 candidates) vs `MATCH_NOT_FOUND` (0). Two `AccumulateCandidates` overloads added.

---

### T-2300 · User decisions: detail crop saliency, headcut, greedy crop
**Done.** Three product decisions recorded in jbtodo.md: BoundingBox from ImagePreProcessor is the sole saliency anchor; Headcut controlled by a bool threaded through the pipeline (from `has-human`); greedy crop aligns bbox center to canvas center with `Tx_util_BgStretch` background fill.

**Files:** `jb/src/core/Images/Transform/jbtodo.md`

---

### T-2200 · Spec and implement Tx_util_HeadCutter
**Done.** Algorithm B (full-image Haar face search, centroid Y < 50%, pick face furthest from top, cutY = face.Y + 0.75×face.Height) implemented. Algorithm A (anatomy-ratio guided search) deferred — jbtodo recorded.

**Files:** `jb/src/core/Images/Transform/Tx_util_HeadCutter.cs`

---

### T-2100 · Implement Tx_DetailCropper pixel flow
**Done.** Full 6-branch decision tree covering every bbox edge-intersection pattern. Crop-sizing driven by `Transformation.Cropping` config via new `CropTransformSettings` struct. 29 tests, including regression tests for two coordinate-shift bugs found during implementation. Verified against real TinyTest fixture image.

**Files:** `jb/src/core/Images/Transform/Tx_DetailCropper.cs`, `CropTransformSettings.cs`, `IImageTransformation.cs`, `ImageTransformer.cs`, `jb/src/core/Services/TransformService.cs`, `jb/src/core/config/PrismConfiguration.cs`

---

### T-2000 · Implement Tx_CenterAndStretch pixel flow
**Done.** Full `Transform()` + `Process()` pixel flow implemented and build clean. Headcut via `Tx_util_HeadCutter` when requested; background fill via `Tx_util_BgStretch.Stretch()`. Canvas math amended after T-2100/T-3100 verification: crop to bbox, resize to margin-adjusted target size preserving aspect ratio, center on canvas, then stretch background (guarantees non-negative placement offset).

**Files:** `jb/src/core/Images/Transform/Tx_CenterAndStretch.cs`

---

### T-1900 · Tx_LowContrastEnhancement
**Done.** CLAHE (Contrast Limited Adaptive Histogram Equalization) via OpenCVSharp4, applied to full image. Dual-interface signature `Process(byte[] arr, int stride, float upscale_factor)`.

---

### T-1800 · ProductTypeId write to ImageRecord_LAMBDA
**Done.** `lambda.ProductTypeId = productTypeId;` added in `ImageOrderer.ProcessFamily` write-back loop. `ResolveProductType()` reads from Excel IEM dynamic columns and normalizes to kebab-case against `DetOrderRules.json`.

---

### T-1700 · Tx_util_BgStretch
**Done.** Tiered background fill: ≤125% edge clamp, ≤142% content-aware extension, >142% INPAINT_TELEA, >250% solid white. Seam feathering after tiers 1 and 2. `Process(byte[] arr, int stride, float upscale_factor)` dual-interface signature.

---

### T-1600 · SD-8: ImageRecord_OUTPUT Width/Height/Checksum
**Done — not a bug.** Fields are declared on `ImageRecord_Base` and inherited by all `ImageRecord*` types. No code changes.

---

### T-1500 · Split StageShells.cs
**Done.** `StageShells.cs` deleted. Eight `ShellStage_Xyz.cs` files created in `jb/src/core/Pipeline/` (one per stage). `Prism.cs` call sites updated to new class names.

---

### T-1400 · Fetch_DropBox
**Done.** Public shared links (`dropbox.com/s/...?dl=0`) normalized to `?dl=1` and delegated to `Fetch_HTTPS_DirectFile`. `dl.dropboxusercontent.com` URLs pass through unchanged. Private OAuth deferred (out of scope V1).

---

### T-1300 · Fetch_HTTPS_DirectFile
**Done.** `Fetch_HTTPS_DirectFile.cs` streams direct HTTPS downloads to `%TEMP%/prism/{jobID}/`, validates URL against `HostRules.json` (scheme, blocked hosts, redirect limit, timeout), returns `ImageRecord_INPUT`.

---

### ONNX Singleton (M5 gate item)
**Done (2026-06-29).** `InferenceSession` hoisted from per-job to application-scoped singleton on `MatchingService`. `ClassificationService` now borrows the shared `ImageClassifier` (no longer owns/disposes it). `_clipLock` on `MatchingService` serializes all `Run()` calls (required for DML). Disposal chain: `MatchingService` → `Pipeline` → `PrismService` (all now implement `IDisposable`). PRISM-classify.md updated. Verified: two TinyTest jobs, CLIP tags in Lambda documents, probe fired once at startup.

---
