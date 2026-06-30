# PRISM Agent Tickets

Main thread is the orchestrator: owns ticket status, final integration, conflict resolution, and user-facing summaries.

## Team Rules

- Do not revert or overwrite edits made by other agents.
- Stay inside the ownership and write scope stated on your ticket.
- Read `jb/docs/PRISM-index.md` first; load only docs relevant to the ticket.
- Preserve pipeline order: Imported → Classified → Matched → Ordered → Renamed → Generated → Transformed → Exported.
- Do not advance past a milestone until its gate is documented and passed.
- Unresolved product decisions stay in folder-local `jbtodo.md` files — do not guess policy.

## Agent Reporting Protocol

- Report: ticket ID, changed files, commands run, pass/fail results, blockers, assumptions, next ticket.
- If blocked: stop, ask the orchestrator one targeted question — never ask the user directly.
- If work is found outside ticket scope: report a follow-up ticket, do not edit out of scope.
- Do not self-start the next ticket; orchestrator reviews completed work first.

## Orchestrator Handoff Protocol

- Satisfactory → mark `Done`.
- Incomplete but salvageable → correction to same agent or follow-up ticket.
- Missing product intent → ask user, then unblock agent.
- Milestone gates are authoritative: later tickets stay blocked until the gate passes.

## Ticket Format

Status: Ready, Blocked, Active, Review, Done. Agent type: `explorer`, `worker`, or orchestrator.

## Runtime Profiles

| Profile | Model | Use |
|---|---|---|
| `P0-orchestrator` | parent/default | Main thread, integration, conflict resolution, milestone decisions |
| `P1-feature-worker` | parent/default | Primary implementation tickets |
| `P2-verifier` | haiku | Smoke-test agents — run commands, inspect results, report blockers |
| `P3-scout` | haiku | Read-only exploration, architecture maps, dependency checks |
| `P4-critical-architecture` | parent/default | Cross-cutting contracts or pipeline architecture |

## Milestone Gates

| Milestone | Feature area | Gate condition |
|---|---|---|
| M5 Classification Groundwork | ImageNGP taxonomy + rule correctness | All Classify `jbtodo.md` decisions answered; ONNX session migrated to singleton ✅ (done 2026-06-29) |
| M6 Human & Model Detection | `hero-is-human`, `contains-mannequin`, `has-human`, `head-visible`, `face-visible` | On-model and ghost phenotypes (`front-on-model-*`, `ghost-front/back/side`) fire correctly on labeled images |
| M7 Orientation & Pose | `hero-orientation`, `pose-type`, `camera-angle`, `top-view` | Packshot orientation-split phenotypes (`front-packshot`, `back-packshot`, `side-packshot`) fire from real signal |
| M8 Product & Packaging | `packaging-visible`, `product-type-label`, `multiple-products` | packshot phenotypes fire from CLIP; `packaging-visible` no longer always UNKNOWN |
| M9 Composition & Spatial | `product-coverage-ratio`, `image-occupancy`, `salient-bbox`, `vertical-centering`, `horizontal-centering` | Composition phenotypes measured; overflow slot assignment accuracy confirmed |
| M10 Semantic & Content | `text-present`, `logo-present`, `dominant-colors`, `lighting` | Content features populated; transform routing that depends on them verified |
| M11 Production Validation | All 26 phenotypes | < 5% misassignment on labeled validation set; no systematic error on any single phenotype |

## Tickets


### T-2000 · Implement Tx_CenterAndStretch pixel flow
**Status:** Blocked | **Profile:** P1-feature-worker | **Agent:** worker  
**Blocked-by:** T-2300 (saliency/headcut/greedy user decisions) — T-1900 Done

Three-step pixel flow inside `Tx_CenterAndStretch.Transform()` — currently gated behind `ImageProcessorAvailable() = true` but pixel body is a `NotSupportedException`.

**When unblocked, what to do:**
1. Pre-steps: if `low-contrast` feature true → call `Tx_LowContrastEnhancement`; if `shadow-present` → shrink `salient-bbox` bottom edge above shadow band.
2. Tight crop: shrink source canvas to adjusted `salient-bbox`.
3. Center: place cropped object on target square canvas with `Transformation.Positioning.Margin` (4.2%) on all sides.
4. Fill: call `Tx_util_BgStretch.Stretch()` on the uncovered canvas edges.
5. Populate `ImageTransformationResult` fully (crop rect, fill method, warnings).
6. `dotnet build jb/src/PRISM.sln` passes.

**Files:** `jb/src/core/Images/Transform/Tx_CenterAndStretch.cs`

---

### T-2100 · Implement Tx_DetailCropper pixel flow
**Status:** Blocked | **Profile:** P1-feature-worker | **Agent:** worker  
**Blocked-by:** T-2300 (saliency/headcut/greedy user decisions), T-2200 (HeadCutter spec), T-2000 (for pattern reference)

Pixel body for `Tx_DetailCropper.Transform()` — currently gated and throws.

**When unblocked, what to do:**
1. Read `salient-bbox` from `InputImage.Features`.
2. Detect border intersection (intersects-top/bottom/left/right features).
3. Non-intersecting: apply greedy crop centered on saliency region; apply headcut when `head-visible` and `hero-is-human` meet configured thresholds.
4. Border-intersecting: anchor crop to touched edges; record no-reposition decision.
5. Apply `Tx_util_BgStretch` when crop extends beyond original bounds.
6. Populate full `ImageTransformationResult`.
7. Internal fallback to `Tx_CropSquare` when border intersection blocks pixel-level repositioning.
8. `dotnet build jb/src/PRISM.sln` passes.

**Files:** `jb/src/core/Images/Transform/Tx_DetailCropper.cs`

---

### T-2200 · Spec and implement Tx_util_HeadCutter
**Status:** Blocked | **Profile:** P1-feature-worker | **Agent:** worker  
**Blocked-by:** Product decisions (landmark model, family-aware threshold, cut line style, Y-coordinate return format) must be recorded in Transform `jbtodo.md` before any code is written.

Utility class for cutting a human head at the nose-to-lips boundary. Two modes: family-aware (shared cut line from clear-face images in the group) and per-image fallback.

**Files:** `jb/src/core/Images/Transform/processingtools/Tx_util_HeadCutter.cs`

---

### T-2300 · User decisions: detail crop saliency, headcut, greedy crop
**Status:** Blocked | **Profile:** P0-orchestrator  
**Blocked-by:** User product decision required — answers must be recorded in Transform `jbtodo.md` before T-2100 or T-2200 can proceed.

Three open questions in Transform `jbtodo.md` with blank `Answer:` fields:
1. Saliency map behavior: how the dominant saliency region influences square crop placement when no border intersection blocks repositioning.
2. Headcut thresholds: which `head-visible`/`hero-is-human` confidence levels enable headcut; how top crop placement shifts for eligible non-intersecting images.
3. Greedy crop behavior: minimum content retention and padding rules for non-headcut non-intersecting images.

Each answer unlocks T-2100 (DetailCropper) and T-2200 (HeadCutter).

**Files:** `jb/src/core/Images/Transform/jbtodo.md` (answers to be recorded there)

---


### T-2600 · M5 Classify groundwork
**Status:** Blocked | **Profile:** P0-orchestrator  
**Blocked-by:** M5 milestone gate — all Classify `jbtodo.md` decisions must be answered first.

Tracks the five open items in `jb/src/core/Images/Classify/jbtodo.md`:
1. Gate phenotypes (bypass flag — stays open until phenotypes validated).
2. Confirm ImageNGP taxonomy: `ImageNGP.json` ↔ `imagePhenotypes.md` ↔ `ImageRoles.json` agree on 26 phenotypes and their IF combinations.
3. Resolve `illustration-technical-drawing` scope (option (b) = null/no-phenotype recommended).
4. Replace `RecordUnknownFeatures()` stub with real CLIP measurements (after taxonomy + prompts are settled).
5. Phenotype production validation: labeled set, confusion matrix, <5% misassignment rate across 26 phenotypes.

M5 gate condition: all Classify decisions answered; ONNX session migrated to singleton.

**Files:** `jb/src/core/Images/Classify/jbtodo.md`, `jb/src/core/Images/Classify/ImageFeatureAnalyzer.cs`

---


## Verification Rules

- After project/solution setup: `dotnet build jb/src/PRISM.sln`, API/WPF run smoke, web `npm run typecheck` + `npm run build`.
- After each milestone: run relevant smoke and record result in milestone table.
- After todo/doc sync: `rg --files -g 'jbtodo.md' jb/src` and `rg -n "^- \[ \]" jb/src`.
- Before advancing milestones: `git status --short` to confirm edits stayed inside assigned ownership.
