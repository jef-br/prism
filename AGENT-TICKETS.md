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
**Status:** Done | **Profile:** P1-feature-worker | **Agent:** worker  

Full `Transform()` + `Process()` pixel flow implemented and build clean (0 errors, 0 warnings).
Headcut via `Tx_util_HeadCutter` when requested. Background fill via `Tx_util_BgStretch.Stretch()`.

**Amended (while verifying T-2100/T-3100):** the original canvas math (`longestSide + 2*marginPx`, placing the whole uncropped source at a bbox-derived offset) crashed on real photos whenever the bbox wasn't already near the frame's own center — `Tx_util_BgStretch` can only add non-negative borders, and centering an off-center bbox this way can require a negative placement offset. Replaced with: crop to the bbox, resize that crop to fit a margin-adjusted target size (preserving aspect ratio), center the resized product on the final canvas, then stretch the background. The resized product is always strictly smaller than the canvas, so the offset is always non-negative by construction. Verified against a known-good real-world reference implementation's exact worked numbers.

**Files:** `jb/src/core/Images/Transform/Tx_CenterAndStretch.cs`

---

### T-2100 · Implement Tx_DetailCropper pixel flow
**Status:** Done | **Profile:** P1-feature-worker | **Agent:** worker  

Full 6-branch decision tree implemented, covering every bounding-box edge-intersection pattern (0/1/2-opposing/2-adjacent/3/4 touched edges). Crop-sizing driven by `Transformation.Cropping.Coverage`/`Extension.OneSided`/`Extension.BiDirectional` (`Prism_Config.json`), threaded via a new `CropTransformSettings` value struct. All "can't reposition cleanly" cases handled locally (never delegates to `Tx_CropSquare`; `TransformerType` always reports `Tx_DetailCropper`). `IImageTransformation.Process()` gained an optional `ImageRecord_LAMBDA` parameter for callers that already have one. 29 tests, including regression tests for two coordinate-shift bugs found and fixed during implementation/review. Verified against the real TinyTest fixture image `24211507_76_C.jpg` (`BypassPhenotypes` still routes it through `Tx_CropSquare` as designed — T-2600 owns flipping that gate).

**Files:** `jb/src/core/Images/Transform/Tx_DetailCropper.cs`, `CropTransformSettings.cs` (new), `IImageTransformation.cs`, `ImageTransformer.cs`, `jb/src/core/Services/TransformService.cs`, `jb/src/core/config/PrismConfiguration.cs`

---

### T-2200 · Spec and implement Tx_util_HeadCutter
**Status:** Done | **Profile:** P1-feature-worker | **Agent:** worker  

Algorithm B (full-image Haar face search, centroid Y < 50%, pick face furthest from top, cutY = face.Y + 0.75×face.Height) implemented. Algorithm A (anatomy-ratio guided search when `has-human=true`) is deferred — deepdive jbtodo recorded in `jb/src/core/Images/Transform/jbtodo.md`.

**Files:** `jb/src/core/Images/Transform/Tx_util_HeadCutter.cs`

---

### T-2300 · User decisions: detail crop saliency, headcut, greedy crop
**Status:** Done | **Profile:** P0-orchestrator  

All three product decisions answered and recorded in `jb/src/core/Images/Transform/jbtodo.md`:
1. Saliency: BoundingBox from ImagePreProcessor is the sole anchor — no further computation in Transform.
2. Headcut: controlled by a `Headcut` bool threaded through the pipeline; human presence from `has-human` feature.
3. Greedy crop: bbox center aligns to canvas center; background filled by Tx_util_BgStretch.

**Files:** `jb/src/core/Images/Transform/jbtodo.md`

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


### T-3000 · Parallelize image import normalization
**Status:** Done | **Profile:** P1-feature-worker | **Agent:** worker

**Outcome:** Both image loops (`ProcessDirectImageRecords` and the zip-member loop in `ProcessZipRecords`) now normalize via `Parallel.ForEach` capped at `Environment.ProcessorCount`; result accumulation moved to `ConcurrentBag<T>`; the filename-uniqueness index moved from the racy `normalizedImages.Count` read to a job-scoped `Interlocked` counter. The deferred fast-path-JPEG follow-up landed in the same pass (see `PRISM-io-import.md`'s "Fast-Path Already-Conforming JPEGs" section) — already-conforming JPEGs are now copied unchanged into `normalized/` instead of decoded and re-encoded, resolving the jbtodo's (a)/(b) question as (a). `jb/src/core/IO/Import/jbtodo.md` closed and removed.

**Problem:** `Importer.ProcessDirectImageRecords` and the zip-member loop in `ProcessZipRecords` normalize images in a sequential `foreach`. Each image is decoded (`Image.Load`), composited (`AutoOrient` + flatten-to-white), then re-encoded to JPEG q92 and written — all on one thread. On large batches this pins ~1 core (observed 9–17% total CPU on a multi-core box; ~20 min for MMERO26's 4048 images) while the rest of the CPU and the SSD sit idle. The per-image work is independent and embarrassingly parallel.

**What to do:**
1. Replace the sequential image loops (direct image records + zip image members) with `Parallel.ForEach`. Cap `MaxDegreeOfParallelism` to `Environment.ProcessorCount` so only N images are decoded in flight at once (bounds peak memory).
2. Make result accumulation thread-safe: `normalizedImages`, `imageKoRecords`, `zipKoRecords` are `List<T>` mutated inside the loop. Use a concurrent collection or per-partition lists merged afterward; preserve existing OK/KO semantics (batch continues on a per-image failure).
3. Fix the normalized-filename index race: `BuildNormalizedFileName` uses `normalizedImages.Count` as the uniqueness index, which races under parallelism. Pre-assign a stable index by input position (deterministic filenames) or use an `Interlocked` counter; filenames must stay unique/collision-free.
4. Leave Excel/IEM build (`BuildFamilyRecords` → `ModelBuilder`) sequential — it is not the bottleneck and `ModelBuilder` is not thread-safe.

**Acceptance:**
- `dotnet build jb/src/PRISM.sln` clean (0 warnings).
- Import wall-time on a large set (e.g. MMERO26, 4048 imgs) drops materially; CPU rises from ~1 core toward N cores during import.
- Identical OK/KO counts and the same set of normalized outputs as the sequential version (order-independent); no filename collisions.
- Existing tests green.

**Files:** `jb/src/core/IO/Import/Importer.cs`

---


### T-3100 · Bracket 4 (SemanticMatcher) perf: skip without CLIP tags; index its string scoring
**Status:** Done | **Profile:** P1-feature-worker | **Agent:** worker

**Outcome:** `ImageMatcher.RunWaterfall` now skips `RunBracket4` entirely when no record in the batch has any influential CLIP tag (`allRecords.Any(r => r.Tags.Influential.Length > 0)`) — verified safe against `MatchingConfig.json`'s actual `ProductType`/`ProductColor` `ClipLabelEnricher` rules. `StringMatcher.ScoreCandidatesByStringTokens` rewritten to reuse Bracket 3's existing inverted token index (via a new `indexScope` parameter carrying the stable per-bracket family superset) instead of an un-indexed per-family scan, preserving exact pre-rewrite `MatchCount`/ordering semantics. 18 tests. Verified against real TinyTest data: identical `FamilyId` assignments with and without `--skip-classification`.

**Problem:** `ImageMatcher.RunBracket4` calls `SemanticMatcher.TryMatch` for every still-unmatched image against all unassigned families. `SemanticMatcher` copies the whole unassigned-family list per image (`[..unassignedFamilies]`) and scores via `StringMatcher.ScoreCandidatesByStringTokens` — the **un-indexed** O(families×tokens) scan (the bracket-3 inverted index does **not** cover this path). After brackets 1–3 leave most images unmatched and most families unassigned, this is O(images × families × tokens) on a single thread. Worse, under **skip-classification there are no CLIP tags**, so bracket 4's CLIP hard filters have nothing to act on — it produces ~no matches yet still scans every family for every unmatched image. Pure wasted compute.

**Evidence:** MMERO26 (4048 images) killed at ~40 min with **1 of 20 cores pegged, 0 MB/s disk I/O, 397 MB RAM** — the signature of single-threaded compute, not import. Bracket 3 (now indexed) is fine; bracket 4 is the residual hot path. Smaller sets (INPUTMA27 569 imgs, INPUTMA23 921 imgs) completed only because images×families is far smaller.

**What to do:**
1. **Skip bracket 4 when the batch carries no CLIP classification signal** (skip-classification, or no record has Tags/phenotype, or `labelRules` empty). Gate it in `RunWaterfall`/`RunBracket4`. Correctness-neutral — bracket 4 is the CLIP-semantic bracket and yields nothing without tags. This matches the intent already documented for `MatchLite` ([PrismService.cs:104-106](jb/src/core/PrismService.cs#L104-L106)).
2. **For the with-classification path**, replace `SemanticMatcher`'s per-family string scan with the same inverted token index used by `StringMatcher` (bracket 3), or pre-filter candidates via the index before scoring. Eliminate the per-image `[..unassignedFamilies]` full copy.
3. Preserve bracket 4 semantics (exactly-one survivor + `SemanticThreshold`).

**Acceptance:**
- Skip-classification MMERO26 completes in minutes (not pinned single-core for tens of minutes); bracket 4 makes 0 assignments under skip-classification (match rate unchanged vs. a bracket-4-disabled baseline).
- With-classification match outcomes unchanged; no O(images×families) per-image scan (verify via a families-count scaling check or timing).
- `dotnet build jb/src/PRISM.sln` clean; existing matcher tests green; add a test asserting bracket 4 is skipped when no tags are present.

**Files:** `jb/src/core/Images/ImageMatcher.cs`, `jb/src/core/Images/Match/SemanticMatcher.cs`, `jb/src/core/Images/Match/StringMatcher.cs`

---


## Verification Rules

- After project/solution setup: `dotnet build jb/src/PRISM.sln`, API/WPF run smoke, web `npm run typecheck` + `npm run build`.
- After each milestone: run relevant smoke and record result in milestone table.
- After todo/doc sync: `rg --files -g 'jbtodo.md' jb/src` and `rg -n "^- \[ \]" jb/src`.
- Before advancing milestones: `git status --short` to confirm edits stayed inside assigned ownership.
