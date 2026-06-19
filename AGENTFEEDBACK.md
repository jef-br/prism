# AGENTFEEDBACK.md

Agent-owned reload memory. Not authoritative project docs. Accepted knowledge in `jb/docs/`. Abbreviations: `jb/docs/GLOSSARY.md`.

## Current State

- Todo files: 6 non-empty `jbtodo.md` (Classify ×2, Match, Order, Transform, Generate, IO).
- Fixture folder closed 2026-06-16: `jb/testing` one subfolder/job; each gets `foldername + " - expected result"` sibling with real expected files.
- **V1 queue**: single-server in-process bounded queue, fixed workers. RabbitMQ deferred until durable recovery/distributed workers needed.
- **SixLabors.ImageSharp 3.1.12** (Apache 2.0; CVEs GHSA-2cmq-823j-5qj8 high + GHSA-rxmq-m78w-7wmc moderate cleared). 4.0.0 rejected (commercial license).
- **ImageClassifier.cs** owns ONNX boundary: loading, asset validation, session lifetime, diagnostics. Sessions application-scoped.
- **INGP taxonomy finalized 2026-06-15**: 26 phenotypes. `imagePhenotypes.md` reduced from 30. `detail-material/stitching/label/hardware` → `closeup-image`. `exploded-view` + `multi-angle-composite` → `illustration-technical-drawing` (always last det slot). `lifestyle-context` = catch-all non-packshot. Phenotype assignment: always hard; no soft vectors.
- **PT authority**: Excel authoritative. `product-type-label` is supporting evidence/fallback when Excel has no PT. Multiple PTs can share one IF grouping.
- **det0 orientation**: Frontal required. Fallback: FRONT → SIDE → DIAGONAL. BACK/TOP/BOTTOM disqualified for det0.
- **DOR**: Current content indicative only. `jb/docs/ImageNGP/PRODUCTTYPES.MD` authoritative for per-PT det-slot specs.
- **CLIP source**: `sentence-transformers/clip-ViT-B-32` (Hugging Face or Microsoft Foundry). Not in git. SHA-256: see `jb/docs/PRISM-classify.md`.
- Model placeholders in `jb/src/core/Models`: `ImageNGP.cs`, IRI/IRL/IRO/IRG `.cs`.
- Matching uses `jb/src/core/Images/Match/MatchEvidence.cs`. Transformation uses `jb/src/core/Images/Transform/ImageTransformationResult.cs`.
- IO failure placeholders: import/export/general exception files. `KoReason.cs` not present.
- **Four IF decisions**: `salient-bbox` → `BoundingBox` (flat `float[4]` serialized); `pose-type`/`body-visible` share one PAF pass gated by `skin-tone-area`; `product-type-label` extreme mismatch → KO; `dominant-colors` → spatially-weighted LAB palette-cluster with salient-mask background subtraction.
- **M4 complete 2026-06-17**: All 8 stages implemented/tested. 130/130 green. See AGENT-TICKETS.md archive.

## INGP Structure

Decision lens for future agents.

### Core Meaning

- INGP = canonical measured semantic image state.
- ImageRole = configured label for required IF-state permutation.
- DOR = PT + det-slot → ordered ImageRole preference list.
- Transformation consumes INGP IFs as modifiers.

### Planned Config Ownership

- `jb/src/core/ImageNGP/ImageFeatures.json`: IF IDs and allowed states.
- `jb/src/core/ImageNGP/ImageRoles.json`: role labels and required IF states.
- `jb/src/core/Images/Order/DetOrderRules.json`: PT det slots and ordered allowed ImageRoles.

### Qualification Rules

- Analyzers emit measured IF observations into per-image INGP snapshot.
- Image qualifies for ImageRole only when **all** required IF states match.
- Missing or UNKNOWN required IF state = does not qualify.
- `INGP.TypeOfShot` is one IF inside INGP — not the full taxonomy.
- CLIP prompts/thresholds feed IF analyzers; they do not assign DO directly.

### DetOrder Rules

- Enum-weight ordering superseded by ImageRole qualification + ordered det preferences.
- Each PT/det-slot: ordered allowed ImageRoles. Omitted = disallowed.
- `default` mapping used when no PT-specific mapping exists.
- Ties: role confidence → compatible filename order hints → stable import/source index.
- Trusted `_det#` filename schemes may shortcut ordering only when every image has a unique det token and each image's ImageRole is allowed for that det slot.
- Images with no eligible configured role: assigned after configured det slots by deterministic fallback — not dropped.

### Todo Impact

- Directly: `jb/src/core/Images/Classify/`, `jb/src/core/Images/Transform/`.
- Indirectly: `jb/src/core/Images/Match/`, CLIP prompt/threshold todos, workbench diagnostics.

## Open Work Index

- [ ] `jb/src/core/Images/Classify/jbtodo.md`: 6 — `ghost-front` ordering bug in `ImageRoles.json` (fix ready to apply); `illustration-technical-drawing` catch-all scope (user decision needed); taxonomy + IF combos list; `RecordUnknownFeatures()` stub (blocked by first two); CLIP prompt format (key=value wrong for CLIP); `interior-shot` unreachable in CPU-only.
- [ ] `jb/src/core/Images/Match/jbtodo.md`: 5 — 4 spec deviations (numeric scoring formula, ME missing 3 fields, Bracket 3 duplicate guard, original token text not preserved) + 1 user decision (cross-bracket tie resolution).
- [ ] `jb/src/core/Images/Order/jbtodo.md`: 3 — det0 SIDE fallback (user decision), `illustration-technical-drawing` last-slot guard, OrderEvidence missing full qualifying candidate set.
- [ ] `jb/src/core/Images/Transform/jbtodo.md`: 15 — 11 design decisions (product answers required); 4 impl todos (Tx_CropSquare, Tx_CenterAndStretch, Tx_DetailCropper, Tx_ProblemImageProcessor) with prerequisites.
- [ ] `jb/src/core/Images/Generate/jbtodo.md`: 1 — wire real `GenerationBackendAvailable()`. Recommend ComfyUI + Flux.1-schnell on-prem.
- [ ] `jb/src/core/IO/jbtodo.md`: 4 — fetch stubs: T-1300 (HTTPS, Ready), T-1400 (DropBox, Blocked/deferred), WeTransfer (no ticket); media-triage extension-only; directory input missing.
- [ ] `jb/src/core/Pipeline/jbtodo.md`: StageShells split → T-1500 (Ready).
- [x] `jb/src/core/Models/jbtodo.md`: SD-8 closed — not a bug (fields inherited from `ImageRecord_Base`). T-1600 Done.
- [x] `jb/src/jbtodo.md`: fixture folder closed 2026-06-16.

## Web/API Notes

- Web workbench fully implemented 2026-06-15: upload, drag-and-drop, API client, SSE progress, job-status badge, route stage list, result manifest, ZIP download. All states rendered.
- API synced: error payload, URL validation, request size validation, ignored zip member behavior. HCFG typed policy keys: `allowedSchemes`, `blockedSchemes`, `blockedHostPatterns`, `redirects`, `networkRanges`, `timeouts`, `testing`.
- Pre-core unsupported/policy-rejected URLs dropped without manifest, KO, or PJR trace when enough valid input remains.
- WPF parity deferred. Shell exists. Deferred until API + core contracts stabilize.
- T-1200 Done 2026-06-17. 130/130 green. M4 complete. No active tickets.

## Reload Rules

1. Load `jb/docs/` first — owns accepted knowledge.
2. Use folder-local `jbtodo.md` for unresolved/pending decisions.
3. Accepted todo → move to `jb/docs/`, remove local block, delete `jbtodo.md` if empty.
