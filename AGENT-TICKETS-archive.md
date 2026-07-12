# PRISM Agent Tickets — Archive

Done tickets, moved here by /ticket-finish to keep AGENT-TICKETS.md (read every session start) lean.
Newest at the top.


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
