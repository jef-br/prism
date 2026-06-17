# PRISM Agent Tickets

Standing ticket board for PRISM sub-agents. Main Codex thread is the orchestrator: owns ticket status, final integration, conflict resolution, and user-facing summaries.

## Team Rules

- Agents must not revert or overwrite edits made by others.
- Agents must stay inside the ownership and write scope stated on their ticket.
- Read `jb/docs/PRISM-index.md` first, then only the docs and local todo files relevant to the ticket.
- Preserve the fixed pipeline order: Imported > Classified > Matched > Ordered > Renamed > Generated > Transformed > Exported.
- Respect the existing folder structure in assigned area.
- Do not move to a later milestone until the current milestone has a documented smoke test and passes.
- Unresolved product decisions stay in folder-local `jbtodo.md` files. Do not guess product policy.

## Agent Reporting Protocol

When done, blocked, or needing review, report: ticket ID, changed files, commands run, pass/fail results, blockers, assumptions, and recommended next ticket.

- If blocked: stop work inside scope, ask the orchestrator one targeted question. Do not ask the user directly.
- If work is found outside ticket scope: report a suggested follow-up ticket instead of editing out of scope.
- Dependency tickets require orchestrator approval before any agent starts them.
- Agents do not start their own next ticket. Orchestrator reviews completed work first.

## Orchestrator Handoff Protocol

- Satisfactory work → mark ticket `Done`.
- Incomplete but salvageable → focused correction to same agent or new follow-up ticket.
- Blocked by missing product intent → ask user, then unblock agent.
- Once `Done`: identify next eligible ticket, start correct agent/profile, update user.
- Milestone gates are authoritative: later tickets remain blocked until required smoke test passes.

## Ticket Format

Status: Ready, Blocked, Active, Review, Done. Agent type: `explorer`, `worker`, or orchestrator.
Every runtime prompt inherits Team Rules, Reporting Protocol, and Handoff Protocol above.

## Runtime Profiles

| Profile | Model | Reasoning | Use |
|---|---|---|---|
| `P0-orchestrator` | parent/default | high | Main Codex thread, integration, conflict resolution, milestone decisions |
| `P1-feature-worker` | parent/default | high | Primary implementation tickets |
| `P2-verifier` | haiku | medium | Smoke-test agents — run commands, inspect results, report blockers |
| `P3-scout` | haiku | medium | Read-only exploration, architecture maps, dependency checks |
| `P4-critical-architecture` | parent/default | xhigh | Cross-cutting contracts or pipeline architecture |

## Milestone Gates

| Milestone | Area | Gate |
|---|---|---|
| M0 Build Boundaries | `jb/src` | Passed 2026-06-10: solution and all projects build; API and WPF run; web workbench builds. |
| M1 Workbench | `jb/src/workbench` | Passed 2026-06-09: web workbench starts, HTTP 200, all states rendered. |
| M2 API | `jb/src/api` | Passed 2026-06-12: all routes live; minimal job returns BatchManifest with 8 stages in order. |
| M3 Core | `jb/src/core` | Passed 2026-06-12: Prism.cs facade + 8-stage boundary + fail-fast config exception. |
| M4 Pipeline | `jb/src/core` stage folders | **Complete 2026-06-17**: all 8 stages implemented and tested; 130/130 tests green. |

## Tickets

### T-500 Classified Stage
- `Status`: Done
- `Write scope`: classification-related files under `jb/src/core`
- `Context`: `PRISM-classify.md`, `PRISM-models.md`, `ImageNGP/imagePhenotypes.md`, `ImageNGP/ImageFeatures.md`, `ImageNGP/PRODUCTTYPES.MD`, Classify `jbtodo.md`
- `Task`: Visual dedup, CLIP boundary through `ImageClassifier.cs`, ImageFeature storage, selected/candidate ImageNGP storage. 26 phenotypes finalized. Hard assignment only — no soft probability vectors.
- `Acceptance criteria`: Classified stage tested and proven before Matched begins.

### T-600 Matched Stage
- `Status`: Done
- `Write scope`: matching-related files under `jb/src/core`
- `Context`: `PRISM-match.md`, `PRISM-models.md`
- `Task`: Matcher aggregation, evidence records, and FamilyID resolution.
- `Acceptance criteria`: Matched stage tested and proven before Ordered begins.

### T-700 Ordered Stage
- `Status`: Done
- `Write scope`: ordering-related files under `jb/src/core`
- `Context`: `PRISM-order-rename.md`, `PRISM-match.md`, `PRISM-models.md`, `ImageNGP/PRODUCTTYPES.MD`, `ImageNGP/imagePhenotypes.md`
- `Task`: ImageNGP/DetOrder ordering and ordering evidence. Key decisions: hard phenotype assignment; `closeup-image` is single close-up phenotype; `illustration-technical-drawing` always gets last det slot; det0 fallback FRONT → SIDE → DIAGONAL; per-product-type DetOrderRules from PRODUCTTYPES.MD are authoritative.
- `Acceptance criteria`: Ordered stage tested and proven before Renamed begins.

### T-800 Renamed Stage
- `Status`: Done
- `Write scope`: rename-related files under `jb/src/core`
- `Context`: `PRISM-order-rename.md`, `PRISM-models.md`
- `Task`: Output filename stems, `_det` suffixes, unmatched naming, collision handling.
- `Acceptance criteria`: Renamed stage tested and proven before Generated begins.

### T-900 Generated Stage
- `Status`: Done
- `Write scope`: generation-related files under `jb/src/core`
- `Context`: `PRISM-transform-generate.md`, `PRISM-models.md`
- `Task`: Generation decision shell and generated-record flow. Local generation may remain gated when dependencies are not ready.
- `Acceptance criteria`: Generated stage tested and proven before Transformed begins.

### T-1000 Transformed Stage
- `Status`: Done
- `Write scope`: transform-related files under `jb/src/core`
- `Context`: `PRISM-transform-generate.md`, `PRISM-classify.md`, `PRISM-models.md`, Transform `jbtodo.md`
- `Task`: Transform policy, problem-image path, crop/fill/resize decisions, `ImageTransformationResult`.
- `Acceptance criteria`: Transformed stage tested and proven before Exported begins.

### T-1100 Exported Stage
- `Status`: Done
- `Write scope`: export-related files under `jb/src/core`
- `Context`: `PRISM-api.md`, `PRISM-pipeline-core.md`, `PRISM-models.md`
- `Task`: Zip/JSON output and `manifest.json` export.
- `Acceptance criteria`: Exported stage tested and proven; full pipeline smoke passes through all definitive stages.
- `Done note (2026-06-17)`: New: `ManifestImageRow.cs`, `BatchManifestSummary.cs`, `ExportStageResult.cs`, `ExporterTests.cs` (10 tests). Modified: `Exporter.cs` (full zip+JSON impl), `BatchManifest.cs` (ImageRows), `ImageRecord_LAMBDA.cs` (OutputRecord?), `PipelineContext.cs` (ExportResult?), `PipelineResult.cs`+`Pipeline.cs`+`Prism.cs` (ZipBytes+manifest), `PrismJobResult.cs`, `PrismApiModels.cs` (fixed PrismJsonImagesEnvelope always-empty bug). 117/117 tests green. **M4 Pipeline milestone complete.**

### T-1200 Match Stage Unit Tests
- `Status`: Done
- `Write scope`: `jb/src/tests/Prism.Core.Tests/Match/`, Match `jbtodo.md`
- `Context`: `NumericMatcher.cs`, `StringMatcher.cs`, `ImageMatcher.cs`, `MatchEvidence.cs`, `FamilyRecord.cs`
- `Task`: Unit tests for NumericMatcher (Brackets 1 and 2) and StringMatcher (Bracket 3). Close "zero unit tests" todo in Match `jbtodo.md`.
- `Acceptance criteria`: All new tests pass; no existing tests broken; "Match stage has zero unit tests" entry removed.
- `Done note (2026-06-17)`: `NumericMatcherTests.cs` (8 tests: B1/B2 happy paths, tie→null, no-match) + `StringMatcherTests.cs` (5 tests: happy path, no-match, tie, all-digit, synonym). Bracket2 TCD uses strict `>` comparison; equal-length splits produce TCD=1.0 exactly. 130/130 green.

### T-9999 Frozen Fixture Watch
- `Status`: Done
- `Done note`: User thawed 2026-06-16. Answer: one subfolder per Job under `jb/Testing`; each gets a `foldername + " - expected result"` sibling with real expected files. Test project: `jb/src/tests/Prism.Core.Tests/`.

## Archive — Completed Tickets

| Ticket | Title | Done date | Key outcome |
|---|---|---|---|
| T-000 | Ticket Board Setup | 2026-06-09 | `AGENT-TICKETS.md` created with milestone gates, team rules, ticket format. |
| T-050 | Build Boundaries & Solution Setup | 2026-06-10 | `PRISM.sln` with 7 projects (Core.Contracts, Core, Classify, Transform, Api, Wpf, web). M0 gate passed. |
| T-100 | Workbench Bootstrap | 2026-06-09 | Web workbench scaffold: upload, route placeholders, API client. M1 gate passed. |
| T-110 | Workbench Smoke Test | 2026-06-09 | HTTP 200 at `localhost:3000`; all required states confirmed by inspection. |
| T-150 | Integration Test Fixtures | 2026-06-12 | xUnit project + 4 integration tests using SPACINI29/TINY; 8-stage route order asserted. |
| T-200 | API Online End-to-End | 2026-06-12 | Health, config, process, SSE progress, result routes wired to core. M2 gate passed. |
| T-210 | API Smoke Test | 2026-06-12 | All routes verified; BatchManifest with 8 stages in order returned for minimal job. |
| T-300 | Core Backend Shell | 2026-06-12 | `Prism.cs` facade + `Pipeline.cs` 8-stage boundary; config fail-fast; minimal job smoke passed. M3 gate passed. |
| T-310 | Core Smoke Test | 2026-06-12 | 8-stage order verified; fail-fast confirmed by inspection. |
| T-320 | Excel Module Foundation | 2026-06-12 | `ModelBuilder.cs` entry point; IEM, header detection, dedup, FamilyRecord mapping. |
| T-330 | Zip Module Foundation | 2026-06-12 | `ZipHandler.ExtractProcessableMembers`; extraction policy, encrypted/corrupt KO, fixed OK/KO layout. |
| T-400 | Imported Stage | 2026-06-12 | `Importer.cs` fully implemented; `ImageRecord_INPUT` with provenance fields; API ingress spills uploads to `%TEMP%/prism/{jobID}/` so `TempFilePath` is set before enqueue. ImageSharp upgraded to 3.1.12. |
| T-500 | Classified Stage | 2026-06-15 | Visual dedup, ImageFeatureAnalyzer, CLIP classifier, phenotype assignment; 61/61 tests green. |
| T-600 | Matched Stage | 2026-06-16 | ImageMatcher waterfall (brackets 1–3 + label evidence); NumericMatcher (TCD), StringMatcher, ImageLabelingMatcher; MatchEvidence on ImageRecord_LAMBDA; 61/61 tests green. |
| T-700 | Ordered Stage | 2026-06-16 | DetOrderConfig + ImageOrderer; 18-product-type DetOrderRules.json from PRODUCTTYPES.MD; phenotype qualification, deterministic tie-breaking; overflow assignment; OrderEvidence on ImageRecord_LAMBDA; 72/72 tests green. |
| T-800 | Renamed Stage | 2026-06-16 | ImageRenamer.cs; det-slot collision detection (RENAME_COLLISION KOs entire family); OkRenamedCount; NewName computed property; 82/82 tests green. |
| T-900 | Generated Stage | 2026-06-16 | ImageGenerator.cs decision shell; GenerationRouteState (6 states); GeneratedChildren + GenerationRouteState on ImageRecord_LAMBDA; gated (GenerationBackendAvailable=false); 93/93 tests green. |
| T-1000 | Transformed Stage | 2026-06-16 | ImageTransformer.cs routing (phenotype→Tx class); TransformationStatus (5 states); ImageTransformationResult (13 fields); all Tx classes gated; OkTransformedCount; 107/107 tests green. |
| T-1100 | Exported Stage | 2026-06-17 | ManifestImageRow, BatchManifestSummary, ExportStageResult; Exporter.cs (zip+JSON+manifest.json); PrismJsonImagesEnvelope always-empty bug fixed; 117/117 tests green. **M4 complete.** |
| T-1200 | Match Stage Unit Tests | 2026-06-17 | NumericMatcherTests.cs (8) + StringMatcherTests.cs (5); "zero unit tests" jbtodo closed; 130/130 green. |
| T-workbench-polish | Web Workbench Result & Route Display | 2026-06-15 | JSON manifest display; ZIP download; job status badge; stage name heading; conditional field rendering. |

---

## Verification Rules

- After ticket-board setup: confirm this file exists and includes all milestone tickets.
- After project/solution setup: `dotnet build jb/src/PRISM.sln`, separate project builds, API/WPF run smoke, web `npm run typecheck` + `npm run build`.
- After each milestone: run relevant local start/build/smoke and record result here.
- After todo/doc sync work: `rg --files -g 'jbtodo.md' jb/src` and `rg -n "^- \\[ \\]" jb/src`.
- Before advancing milestones: `git status --short` to confirm agent edits stayed inside assigned ownership.
