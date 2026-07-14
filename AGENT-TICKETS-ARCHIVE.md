# PRISM Agent Tickets — Archive

Done tickets, moved here by /ticket-finish to keep AGENT-TICKETS.md (read every session start) lean.
Newest at the top.

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
