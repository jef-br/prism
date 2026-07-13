# PRISM Agent Tickets — Archive

Done tickets, moved here by /ticket-finish to keep AGENT-TICKETS.md (read every session start) lean.
Newest at the top.

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

**Files:** `jb/src/tests/Prism.Core.Tests/` (new folders), `jb/src/core/IO/Import/Importer.cs`, `jb/src/core/Services/LocalArtifactStore.cs`.

---
