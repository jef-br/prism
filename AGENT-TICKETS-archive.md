# PRISM Agent Tickets — Archive

Done tickets, moved here by /ticket-finish to keep AGENT-TICKETS.md (read every session start) lean.
Newest at the top.


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
