# PRISM Agent Tickets — Archive

Done tickets, moved here by /ticket-finish to keep AGENT-TICKETS.md (read every session start) lean.
Newest at the top.


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
