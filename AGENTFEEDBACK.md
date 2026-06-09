# AGENTFEEDBACK.md

Agent-owned reload memory. Not authoritative project documentation. Agents may edit this file when useful unless the prompt explicitly says `AGENTFEEDBACK.md` is unavailable. Accepted PRISM knowledge lives in `jb\docs`; folder-local `jbtodo.md` files hold unresolved or not-yet-integrated decisions, including draft recommendations, frozen todos, and user-owned answers before sync.

Project terminology, accepted media, pipeline order, completed decisions, and operating assumptions also exist in [PRISM-information.md](PRISM-information.md), but `jb\docs` is now the durable documentation home.

## Current State

- Local todo answers have been progressively moved into `jb\docs`; closed blocks are removed from folder-local `jbtodo.md` files after sync.
- Synced areas include API request/progress/result/health/error/URL/request-size/ignored-zip behavior; IO JSON export and EXIF orientation; match normalization, thresholds, weighting, waterfall, tie-breaking, descriptive/mixed matching, `_det`, numeric false positives, language, and stop words; web upload/layout/style decisions; V1 in-process queue; Images filename/collision/sanitization; Zip corrupt/password-protected KO behavior.
- Removed empty todo files after sync for `jb/src/core/Excel/`, `jb/src/core/Models/`, `jb/src/api/`, and `jb/src/core/IO/`. The former `jb/src/core/` todo file is deleted; do not link new work to that location unless restored.
- Current local todo set: 4 non-empty `jbtodo.md` files, 27 open todos.
- Before the latest classification/ONNX/transform sync, the live local todo set was 5 non-empty `jbtodo.md` files and 32 open todos.
- One frozen todo is known for `jb/src/`: fixture folder structure. Keep it frozen until the user explicitly thaws it.
- Local CLIP ONNX model checksum in `jb\docs`: `4AC011172C8C022937BB83DAD2E8FC207F52F19972B36E14808CC3C8042C4E60`.
- `jb/src/core/Images/ImageClassifier.cs` owns the ONNX model boundary: model loading, asset validation/readiness, session lifetime, diagnostics, and communication with the rest of PRISM. Any ONNX provider, worker, session, tokenizer, or buffer helper is hidden behind `ImageClassifier.cs`.
- Current model placeholders live in `jb/src/core/Models`: `ImageNGP.cs`, `ImageRecord_INPUT.cs`, `ImageRecord_LAMBDA.cs`, `ImageRecord_OUTPUT.cs`, `ImageRecord_GENERATED.cs`.
- Matching uses `jb/src/core/Images/Match/MatchEvidence.cs`; transformation uses `jb/src/core/Images/Transform/ImageTransformationResult.cs`; per-image route visualization uses `ImageRecord_LAMBDA` in route order imported, classified, matched, ordered, renamed, generated, transformed, exported.
- IO failure placeholders exist as import/export/general exception files; `KoReason.cs` is not present.
- V1 queue decision: single-server in-process bounded queue with fixed workers; RabbitMQ deferred until durable recovery, multiple processing servers, or broker-backed distribution is needed. Future areas: API/server job coordinator, in-memory job store, SSE adapter, result retention cleanup, queue pressure in health/config.

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
- `jb\docs` contains stale wording that says `ImageNGP.cs` owns "transform-facing image permutations"; clean this up during the next completed-decision sync, not silently inside unrelated work.

## Open Work Index

- [ ] `jb/src/core/Images/Classify/`: 1 open todo, final ImageNGP taxonomy and feature combinations.
- [ ] `jb/src/core/Images/Classify/ONNX/clip-vit-b32-uint8/`: 14 open todos, model license/provenance/version plus tensor, tokenizer, prompt, threshold, expected-output, download, rebuild details.
- [ ] `jb/src/core/Images/Transform/`: 11 open todos, transform-facing ImageFeature/ImageNGP, failures/fallback/fill, crop/resize/detail/cleanup behavior.
- [ ] `jb/src/`: 1 open frozen todo, `jb/Testing` fixture folder structure. Keep frozen.

## Web/API Notes

- Web and WPF workbench todos were recently closed. Do not re-mention `jb/src/workbench/web/` or `jb/src/workbench/wpf/` as open work unless new todo files are created there.
- Web workbench todos already synced: upload component behavior, API client behavior, drag-and-drop error states, upload validation states, Next.js project layout, CSS variable file, and CSS class file.
- API cleanup synced: error payload, external URL validation, request size validation, ignored zip member behavior; `HostRules.json` uses typed URL policy keys `allowedSchemes`, `blockedSchemes`, `blockedHostPatterns`, `redirects`, `networkRanges`, `timeouts`, `testing`.
- Pre-core unsupported or policy-rejected URLs are dropped without manifest, KO, or `PrismJobRequest` trace when enough valid input remains.

## Reload Rules

- Reload `jb\docs` first; it owns accepted knowledge and completed todo decisions.
- Use folder-local `jbtodo.md` files for unresolved or not-yet-integrated decisions.
- When a todo is accepted, move the decision into `jb\docs`, remove the local todo block, and delete the local `jbtodo.md` file if no open todos remain.
- Current priority: work through remaining non-empty folder-local `jbtodo.md` files, starting with high-blocking core classification/transform decisions.
