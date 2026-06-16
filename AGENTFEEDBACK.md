# AGENTFEEDBACK.md

Agent-owned reload memory. Not authoritative project documentation. Agents may edit this file when useful unless the prompt explicitly says `AGENTFEEDBACK.md` is unavailable. Accepted PRISM knowledge lives in `jb\docs`; folder-local `jbtodo.md` files hold unresolved or not-yet-integrated decisions, including draft recommendations, frozen todos, and user-owned answers before sync.

Project terminology, accepted media, pipeline order, completed decisions, and operating assumptions also exist in [PRISM-information.md](PRISM-information.md), but `jb\docs` is now the durable documentation home.

## Current State

- Local todo answers have been progressively moved into `jb\docs`; closed blocks are removed from folder-local `jbtodo.md` files after sync.
- Synced areas include API request/progress/result/health/error/URL/request-size/ignored-zip behavior; IO JSON export and EXIF orientation; match normalization, thresholds, weighting, waterfall, tie-breaking, descriptive/mixed matching, `_det`, numeric false positives, language, and stop words; web upload/layout/style decisions; V1 in-process queue; Images filename/collision/sanitization; Zip corrupt/password-protected KO behavior.
- Removed empty todo files after sync for `jb/src/core/Excel/`, `jb/src/core/Models/`, `jb/src/api/`, and `jb/src/core/IO/`. The former `jb/src/core/` todo file is deleted; do not link new work to that location unless restored.
- Current local todo set: 4 non-empty `jbtodo.md` files (Classify, Transform, IO, Pipeline), 23 open todos. `jb/src/jbtodo.md` fixture todo closed 2026-06-16.
- Before the latest classification/ONNX/transform sync, the live local todo set was 5 non-empty `jbtodo.md` files and 32 open todos.
- The fixture folder structure todo in `jb/src/` was thawed and closed on 2026-06-16. No frozen todos remain.
- Four ImageFeature architecture decisions now resolved and recorded in `jb/docs/ImageNGP/ImageFeatures.md` and `jb/docs/PRISM-models.md`: `salient-bbox` uses `BoundingBox` with flat-float serialization; `pose-type`/`body-visible` share one gated detector pass; `product-type-label` extreme mismatch causes KO; `dominant-colors` uses spatially-weighted palette-cluster with salient-mask background subtraction.
- Current temporary CLIP model source in `jb\docs`: `sentence-transformers/clip-ViT-B-32`, retrievable from Hugging Face or Microsoft Foundry. The local ONNX artifact is ignored and must not be stored in git.
- `jb/src/core/Images/ImageClassifier.cs` owns the ONNX model boundary: model loading, asset validation/readiness, session lifetime, diagnostics, and communication with the rest of PRISM. Any ONNX provider, worker, session, tokenizer, or buffer helper is hidden behind `ImageClassifier.cs`.
- Current model placeholders live in `jb/src/core/Models`: `ImageNGP.cs`, `ImageRecord_INPUT.cs`, `ImageRecord_LAMBDA.cs`, `ImageRecord_OUTPUT.cs`, `ImageRecord_GENERATED.cs`.
- Matching uses `jb/src/core/Images/Match/MatchEvidence.cs`; transformation uses `jb/src/core/Images/Transform/ImageTransformationResult.cs`; per-image route visualization uses `ImageRecord_LAMBDA` in route order imported, classified, matched, ordered, renamed, generated, transformed, exported.
- IO failure placeholders exist as import/export/general exception files; `KoReason.cs` is not present.
- V1 queue decision: single-server in-process bounded queue with fixed workers; RabbitMQ deferred until durable recovery, multiple processing servers, or broker-backed distribution is needed. Future areas: API/server job coordinator, in-memory job store, SSE adapter, result retention cleanup, queue pressure in health/config.
- **SixLabors.ImageSharp**: upgraded from 3.1.5 → 3.1.12 (2026-06-15). Both CVEs (GHSA-2cmq-823j-5qj8 high, GHSA-rxmq-m78w-7wmc moderate) resolved. 4.0.0 was the absolute latest but requires a paid commercial license and was rejected. 3.1.12 is the latest Apache 2.0 patch and clears all known vulnerabilities.
- **API ingress TempFilePath** (T-400 follow-up): complete. `PrismProcessIngressReader.cs` spills each uploaded file to `%TEMP%/prism/{jobID}/{index:D4}_{filename}` and sets `TempFilePath` on every `ImageRecord_INPUT`, `InputExcelFileRecord`, and `InputZipFileRecord` before enqueuing. Temp dir is cleaned up on pre-core validation failures.
- **Web workbench** (2026-06-15): fully implemented. `ResultSection.tsx` shows parsed `BatchManifest` (counts, stage summaries, warnings) and ZIP download button. `RouteSection.tsx` shows live job-status badge. `StageRouteList.tsx` uses stage name as heading and renders fields conditionally.
- **ImageNGP taxonomy finalized (2026-06-15)**: `imagePhenotypes.md` reduced from 30 → 26 phenotypes. `detail-material`, `detail-stitching`, `detail-label`, `detail-hardware` merged into `closeup-image` (required: `hero-is-human = FALSE`, `intersection-count ≥ 1`, `occlusion-level = closeup`). `model-detail-closeup` retained separately — requires `has-human` or skin-tone evidence. `exploded-view` and `multi-angle-composite` merged into `illustration-technical-drawing` (always assigned last det slot). `lifestyle-context` is the catch-all for non-packshot images (generic marketing photos qualify; key distinction is packshot-family vs. non-packshot). Phenotype assignment: always hard; no soft probability vectors.
- **ProductType authority model (2026-06-15)**: Excel is authoritative. `product-type-label` is error-checking and supporting evidence; becomes authority when Excel has no ProductType. Multiple ProductTypes map to one ImageFeature (example: sweater, hoodie, pullover, jacket, short coat, vest, cardigan → `topwear-short`).
- **det0 orientation (2026-06-15)**: Frontal orientation required for det0. Fallback: FRONT → SIDE → DIAGONAL. Back, top, bottom do not qualify for det0.
- **DetOrderRules.json (2026-06-15)**: Current file content is indicative only (not authoritative). Per-product-type det slot specs in `jb/docs/ImageNGP/PRODUCTTYPES.MD` are the authoritative source.
- **T-500 unblocked**: Both blockers cleared — T-400 done, taxonomy finalized. T-500 status: Ready.
- **T-800 done (2026-06-16)**: Renamed stage implemented as `ImageRenamer.cs` under `jb/src/core/Images/`. Stage is validation-only: FamilyID sanitization happens upstream; `NewName` computed property (`{Family}_det{DetOrder}.jpg`) is the definitive output filename consumed by the Exported stage. Work done: det-slot collision detection (`RENAME_COLLISION` KOs whole family), `OkRenamedCount` tracking, stage-complete signal. `RenameStageShell` now delegates to `ImageRenamer.Run(context)`. 82/82 tests green. T-900 unblocked.
- **T-900 done (2026-06-16)**: Generated stage decision shell implemented as `ImageGenerator.cs` under `jb/src/core/Images/Generate/`. Stage evaluates each FamilyID group: families above `MinImagesPerFamily` (config = 1) → `Skipped`; hero below 1600×1600 → `SkippedLowQuality`; qualified hero → `Gated` (no backend deployed). `GenerationRouteState` enum and `GeneratedChildren` list added to `ImageRecord_LAMBDA`. `GeneratedRecords` list and `GeneratedCount` counter added to `PipelineContext`. `GenerateStageShell` now delegates to `ImageGenerator.Run(context)`. 93/93 tests green.
- **T-1000 done (2026-06-16)**: Transformed stage decision shell implemented. `ImageTransformer.TransformImage(lambda)` routes by phenotype: `closeup-image`/`model-detail-closeup` → `Tx_DetailCropper`; null phenotype → `Tx_ProblemImageProcessor`; all others → `Tx_CenterAndStretch`. All Tx classes gate pixel processing behind `ImageProcessorAvailable() = false` → all images receive `TransformationStatus.Gated`. `TransformationStatus` enum (5 states), `ImageTransformationResult` (13 fields), `TransformationResult?` on `ImageRecord_LAMBDA`, `OkTransformedCount` on `PipelineContext`, `TransformStageShell` wired. 107/107 tests green. T-1100 is next.

## ImageNGP Structure Analysis

This is a current decision lens for future agents, not a completed todo sync.

### Core Meaning

- `ImageNGP` = canonical measured semantic image state only.
- `ImageRole` = configured label for a required ImageFeature-state permutation.
- `DetOrderRules` = product-type and det-slot mapping to ordered ImageRole preference lists.
- Transformation consumes ImageNGP image features as modifiers; no broad `TransformRules` concept is introduced yet.

### Planned Config Ownership

- `jb/src/core/ImageNGP/ImageFeatures.json`: feature IDs and allowed states.
- `jb/src/core/ImageNGP/ImageRoles.json`: role labels and required feature states.
- `jb/src/core/Images/Order/DetOrderRules.json`: product-type det slots and ordered allowed ImageRoles.

### Qualification Rules

- Image analyzers emit measured feature observations into the per-image ImageNGP snapshot.
- An image qualifies for an ImageRole only when all required ImageFeature states match.
- Missing or unknown required feature states mean the image does not qualify for that ImageRole.
- `ImageNGP.TypeOfShot` is one ImageFeature inside ImageNGP, not the whole taxonomy and not the whole ImageRole system.
- CLIP prompts and thresholds should feed ImageFeature analyzers; they should not assign DetOrder directly.

### DetOrder Rules

- Current enum-weight ordering is superseded by ImageRole qualification plus ordered det preferences.
- For each product type, each det slot lists allowed ImageRoles in preference order; the first ImageRole is the preferred role for that position.
- Omitted ImageRoles are disallowed for that det slot.
- The `default` DetOrder mapping is used when no product-type-specific mapping exists.
- Ties resolve by role confidence, compatible filename order hints, then stable import/source index.
- Trusted `_det#` filename schemes may shortcut ordering only when every image has a unique det token and each image's ImageRole is allowed for that det slot.
- Images with no eligible configured role remain in the family set and are assigned after configured det slots by deterministic fallback, not dropped.

### Todo Impact

- Directly affected: `jb/src/core/Images/Classify/`, `jb/src/core/Images/Transform/`.
- Indirectly affected: `jb/src/core/Images/Match/`, CLIP prompt/threshold todos, workbench diagnostics.
- Mostly unaffected: fixture layout and model license/provenance todos.
- `jb/docs` docs synced (2026-06-15): stale "transform-facing image permutations" wording confirmed absent from `jb/docs/` (exists only in `jbtodo.md` files where it belongs as an unresolved decision).

## Open Work Index

- [ ] `jb/src/core/Images/Classify/jbtodo.md`: 6 open todos — `ghost-front` ordering bug in `ImageRoles.json` (fix described, ready to apply); `illustration-technical-drawing` catch-all scope (needs user decision); taxonomy + feature combinations list; `RecordUnknownFeatures()` code stub (blocked by first two); CLIP prompt format (key=value schema is wrong for CLIP, no spec alternative yet); `interior-shot` silently unreachable in CPU-only mode.
- [ ] `jb/src/core/Images/Match/jbtodo.md`: 6 open todos — `MatchEvidence` missing 3 fields; `StringMatcher` Bracket 3 missing duplicate-type guard; numeric scoring formula incomplete; original token text not preserved; zero unit tests for Match stage; cross-bracket tie resolution (needs user decision).
- [ ] `jb/src/core/Images/Order/jbtodo.md`: 3 open todos — default det0 SIDE fallback requires algorithm change (needs user decision); no code guard for illustration-technical-drawing last-slot invariant; `OrderEvidence` missing full qualifying candidate set.
- [ ] `jb/src/core/Images/Transform/jbtodo.md`: 10 open todos — all design questions for post-T-1000 pixel processing (transform-facing ImageFeatures, failure/KO policy, crop decision output, fill policy, resize output, border-intersection result, saliency, headcut, greedy crop, detail fill, cleanup method). Code stubs todo closed (T-1000 done). All Answer sections still empty; these are product decisions.
- [ ] `jb/src/core/IO/jbtodo.md`: 7 open todos — `Exporter.cs` stub (blocked by T-1100); `Fetch_HTTPS_DirectFile.cs` stub (no ticket yet); `Fetch_DropBox.cs` stub (deferred); `Fetch_WeTransfer.cs` stub (deferred); ExcelConfig.json loaded at run time instead of startup; media kind triage uses extension-only detection; directory input has no production implementation.
- [ ] `jb/src/core/Pipeline/jbtodo.md`: 1 open todo — stage shell stub for Exported (T-1100). Transformed stub closed (T-1000 done).
- [x] `jb/src/jbtodo.md`: fixture folder structure closed 2026-06-16. Answer: `jb/Testing` one subfolder per Job; each job folder gets `foldername + " - expected result"` sibling with real expected files.

## Web/API Notes

- Web and WPF workbench todos were closed. Do not re-mention `jb/src/workbench/web/` or `jb/src/workbench/wpf/` as open work unless new todo files are created there.
- Web workbench implemented and complete (2026-06-15): upload, drag-and-drop, API client, SSE progress, job-status badge, route stage list, result manifest display, ZIP download. All states rendered: empty, loading, error, progress, result.
- API cleanup synced: error payload, external URL validation, request size validation, ignored zip member behavior; `HostRules.json` uses typed URL policy keys `allowedSchemes`, `blockedSchemes`, `blockedHostPatterns`, `redirects`, `networkRanges`, `timeouts`, `testing`.
- Pre-core unsupported or policy-rejected URLs are dropped without manifest, KO, or `PrismJobRequest` trace when enough valid input remains.
- WPF workbench parity is deferred. Shell exists and opens a window. Full parity deferred until API and core contracts stabilize further (no blocking ticket yet).
- **T-500 (Classified Stage) is Ready** (2026-06-15). Both blockers cleared: T-400 is done and ImageNGP taxonomy is finalized. T-500 is the next pipeline work to start.
- **T-1100 (Exported Stage) is next** (2026-06-16). T-1000 is done. T-1100 is the final pipeline stage: zip/JSON output and `manifest.json` export. `Exporter.cs` is still a comment-only stub. Context: `jb/docs/PRISM-api.md`, `jb/docs/PRISM-pipeline-core.md`, `jb/docs/PRISM-models.md`. **Full implementation plan saved at `C:\Users\JefB\.claude\plans\t1100-exported-stage.md`** — 3 new files, 10 modified files, 12-step implementation order, 10 test cases, target 107→117+ green.

## Reload Rules

- Reload `jb\docs` first; it owns accepted knowledge and completed todo decisions.
- Use folder-local `jbtodo.md` files for unresolved or not-yet-integrated decisions.
- When a todo is accepted, move the decision into `jb\docs`, remove the local todo block, and delete the local `jbtodo.md` file if no open todos remain.
- Current priority: work through remaining non-empty folder-local `jbtodo.md` files, starting with high-blocking core classification/transform decisions.
