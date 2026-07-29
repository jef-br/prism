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

- `P1-feature-worker` / `P4-critical-architecture` tickets: spawn the reviewer agent on the completed diff and record its verdict on the ticket block as `**Review:** Approve|Request Changes (YYYY-MM-DD)`. Only `Approve` makes the ticket eligible for `Done` — /ticket-finish enforces this and will refuse without it.
- `P0`/`P2`/`P3` tickets: orchestrator judgment suffices → mark `Done`.
- Incomplete but salvageable → correction to same agent or follow-up ticket.
- Missing product intent → ask user, then unblock agent.
- Milestone gates are authoritative: later tickets stay blocked until the gate passes.

## Ticket Format

Status: Ready, Blocked, Active, Review, Done. Agent type: `explorer`, `worker`, or orchestrator.
P1/P4 tickets carry a `**Review:** <verdict> (YYYY-MM-DD)` line once reviewed; `Approve` is required before Done.
Done tickets are moved to `AGENT-TICKETS-ARCHIVE.md` (via /ticket-finish) — this file holds open tickets only.

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
| M5 Classification Groundwork | ImageNGP taxonomy + rule correctness | All Classify `jbtodo.md` decisions answered; ONNX session migrated to singleton ✅ (done 2026-06-29); taxonomy trimmed to real/reachable-only ✅ ([[T-4700]], 2026-07-27) |
| M6 Human & Model Detection | **Superseded** — `hero-is-human`, `has-human`, `head-visible` (real, unaffected); ~~`contains-mannequin`, `face-visible`~~ (stub-only, removed [[T-4700]] 2026-07-27) | Re-defined only if/when the removed features are re-introduced — see `Analyzers/jbtodo.md`'s "Removed" section |
| M7 Orientation & Pose | **Superseded** — `hero-orientation` (real, unaffected); ~~`pose-type`, `camera-angle`, `top-view`~~ (stub-only, removed [[T-4700]] 2026-07-27) | Re-defined only if/when the removed features are re-introduced |
| M8 Product & Packaging | **Superseded** — `product-type-label`, `multiple-products` (real, unaffected); ~~`packaging-visible`~~ (stub-only, removed [[T-4700]] 2026-07-27) | Re-defined only if/when `packaging-visible` is re-introduced |
| M9 Composition & Spatial | `product-coverage-ratio`, `image-occupancy`, `salient-bbox`, `vertical-centering`, `horizontal-centering` | Composition phenotypes measured; overflow slot assignment accuracy confirmed |
| M10 Semantic & Content | **Superseded** — `dominant-colors` (real, unaffected, not yet consumed by any phenotype rule); ~~`text-present`, `logo-present`, `lighting`~~ (stub-only, removed [[T-4700]] 2026-07-27) | Re-defined only if/when the removed features are re-introduced |
| M11 Production Validation | All 20 phenotypes | < 5% misassignment on labeled validation set; no systematic error on any single phenotype |

## Tickets

### T-2600 · M5 Classify groundwork
**Status:** Blocked | **Profile:** P0-orchestrator
**Blocked-by:** [[T-4970]] (first-pass phenotype validation), which is itself blocked by the [[T-4900]] epic.
**Board sync (2026-07-29):** rewritten against the code on main after [[T-4700]] and [[T-4800]].
Tracks the 2 items in `jb/src/core/Services/Matching/Classify/jbtodo.md`.

**Item 1 — ImageNGP taxonomy reconciliation: settled, no work left.** [[T-4700]] trimmed the taxonomy to real/reachable features only; [[T-4800]] re-declared `shadow-present`, so it now stands at **38 features /
20 phenotypes**, every one with a live producer. `jb/docs/ImageNGP/HowToAddAPhenotype.md` documents the
process going forward. Tick this checkbox when item 2 closes.

**Item 2 — phenotype production validation: the only open item.**

State of play, verified on main 2026-07-29:
- **Every feature has a producer.** 14 are written by the `Refine` analyzers, 4 by CLIP prompts
  (`hero-is-human`, `head-visible`, `body-visible`, `product-type-label`), the rest measured in phase 1.
  `RecordUnknownFeatures` (`ImageFeatureAnalyzer.cs:300`) is a **phase-1 placeholder that `Refine`
  overwrites**, not a stub backlog. There is no UNKNOWN-forever feature left in the taxonomy.
- **Phenotype assignment produced zero results on real images until 2026-07-28.** `Refine` threw on every
  image and `MatchingService`'s non-fatal catch hid it. Fixed; refinement failures went 86 → 0 on
  SPACINI29. **Nobody has yet reported what the assigned phenotypes actually are** — that measurement is
  [[T-4970]] and it is the real next step.
- **`BypassPhenotypes` is still `true`** (`ImageTransformer.cs:32`). [[T-4850]] gives Transform good
  geometry straight from the subject detector, so flipping it now buys routing nuance rather than basic
  correctness. Lower urgency than it used to be, not higher.

- **The classical-CV subject producer stays. For now.**. Only after the PRISM pipeline is well up and running will we revisit this. (Probably around  the same time we include image generation via ComfyUI) This pertains to archived [[T-4810]], current [[T-4000]], and a listing in `Analyzers/jbtodo.md`
 

**Ordering rule — permanent, do not break.** `SubjectDetector` runs in `ImageFeatureAnalyzer.Refine`
wave 3, *before* `FinalizePhenotype` (`ImageFeatureAnalyzer.cs:152-157`). Any detector-backed feature must
be written before the rules evaluate, or it reads UNKNOWN forever and every phenotype requiring it becomes
unreachable.

**What is left to do, in order:**
1. Finish the [[T-4900]] epic.
2. [[T-4970]] — run the first-pass phenotype validation and report the real distribution.
3. Fix [[T-4955]] (derived edge features go stale on promotion), then decide the `BypassPhenotypes` flip
   from T-4970's data.
4. Only then open the full bar: labeled set, confusion matrix, <5% misassignment across the 20 phenotypes,
   no systematic error on any one. Commission **one** labeled set — [[T-4945]] needs the same asset.

Per-feature CLIP confidence calibration stays parked here (see `AGENTFEEDBACK.md`'s S109 entry): newly
discovered confidence literals get named-const treatment, not config, until this ticket resolves.

M5 gate: item 1 ✅ ([[T-4700]]); ONNX shared session ✅ (2026-06-29); item 2 closes after step 3 above.

**Files:** `jb/src/core/Services/Matching/Classify/jbtodo.md`, `jb/src/core/Services/Matching/Classify/ImageFeatureAnalyzer.cs`

---

### T-3800 · Match bracket todos: validate the fuzzy-categorical and totalImageTokens changes
**Status:** Blocked | **Profile:** P1-feature-worker
**Blocked-by:** CiMini expansion (root `jbtodo.md`, **no ticket owns it**).
**Review:** Approve (2026-07-25) on the `e2e1f84`+`f40beed` diff. Code, tests and config all verified;
the missing empirical validation was recorded as a known caveat, not a defect. **The review gate is
already satisfied** — no second review is needed to finish this ticket.

All code is on main and unchanged. Two of the four original `jbtodo.md` items closed on 2026-07-25
(substring-rescue perf, fuzzy-fallback future-work note — decisions live in `PRISM-match.md`). The two
below are implemented and unit-tested; only real-data validation is missing.

**What is left to do — nothing else:**

- [ ] **Expand CiMini** with the two missing cases (root `jbtodo.md` owns the full wish-list; only these
      two block this ticket): a Bracket-3 fuzzy case (e.g. filename `grey`, Excel colour `gray`) and an
      image that actually reaches Bracket 4.
- [ ] **Item 1 — fuzzy categorical matching.** `StringMatcher.CollectFuzzyCategoricalEvidence`. Run
      before/after on the expanded set; confirm the fuzzy case matches in Bracket 3 and the guardrails
      still reject distance-2 / sub-4-char / non-categorical hits.
- [ ] **Item 4 — `totalImageTokens` precision.** `SemanticMatcher` now uses
      `stringMatcher.CountFilenameTokens(filename)`. Run before/after; confirm no accept/reject flips
      near `SemanticThreshold`. Blocked today because 0 of the 14 CiMini goldens reach Bracket 4.
- [ ] Close both `jbtodo.md` items per the todo lifecycle, then `/ticket-finish`.

**Two things an agent needs to know before starting:**
1. **The goldens do not need re-blessing.** Checked 2026-07-29: `expected-match.json` holds only
   `SourceReference → FamilyId`, `expected-manifest.json` adds only `Status`/`FinalFileName`/`DetOrder`.
   Neither carries features, phenotypes or subject geometry, so [[T-4800]] did not invalidate them.
   Re-capture only after *adding images*, per `test/datasets/CiMini/README.md`.
2. **Run the pipeline-integration project on its own.** `dotnet test jb/src/PRISM.sln` currently fails all
   7 `PipelineIntegrationTests.CiMini_*` tests for an unrelated reason ([[T-4942]]: GPU contention between
   parallel test projects). The project passes 142/142 in isolation. A red solution run is not a
   regression you caused.

**Files:** `jb/src/core/Services/Matching/Match/jbtodo.md`, `jb/src/core/Services/Matching/Match/StringMatcher.cs`, `jb/src/core/Services/Matching/Match/SemanticMatcher.cs`, `jb/docs/PRISM-match.md`, `test/datasets/CiMini/`.

---

### T-4000 · Per-feature Analyzer TOC: calibration backlog (stub-implementation item retired by T-4700)
**Status:** Ready | **Profile:** P0-orchestrator
**Tracks:** `jb/src/core/Services/Matching/Analyzers/jbtodo.md` (triaged 2026-07-11) — a TOC of items across 3 sections, none previously represented on the ticket board.
**Board sync (2026-07-24):** this entry previously claimed 27 items incl. a 4th "OPEN(1)" item — centralize per-analyzer `*Config.cs` files into a single `AnalyzerConfig.cs` with nested objects. That item is no longer in the source `jbtodo.md` (not present in current file, no history of it being explicitly closed either) and the underlying concern was functionally superseded this week by T-4400's S109 pass: single-consumer `*AnalyzerConfig.cs` classes (`Interior`, `Exposure`, `IsIllustration`, `SubjectGeometry`, `FilenameEvidence`, `MultipleProducts`) were folded as nested `Config` types into their owning `Analyzer_*.cs` files — the opposite direction (decentralized-per-file, not one shared `AnalyzerConfig.cs`), but it resolves the same "scattered standalone config files" complaint. `ColorAnalyzerConfig`/`YoloAnalyzerConfig` stay standalone (genuinely multi-consumer). Item count corrected to 26.
**Board sync (2026-07-27, [[T-4700]]):** item 2 below (the 10 stubs) is **no longer a pending-implementation backlog — those analyzers were deleted**, not left unstarted. Their features made 6 phenotypes mathematically unreachable (`PhenotypeRuleSet` never treats `UNKNOWN` as satisfying a `required` condition), so T-4700 removed the stub `.cs`/`.md` files, their features from `ImageNGP.json`, and the phenotypes/DetOrderRules entries that depended only on them. Each stub's proposed workings are preserved in `Analyzers/jbtodo.md`'s new "Removed (deferred pending future re-introduction)" section and in git history. Re-introduction is gated on a reliable DetOrderRules catch-all proving out first (see the Transform-routing follow-up ticket) — pick analyzers back up one at a time then, not as a batch.

**Problem:** `Analyzers/jbtodo.md` is a checklist pointing at per-analyzer working docs, split into:
1. **Implemented, calibration open (11)** — `Analyzer_ProductType`, `Analyzer_FilenameEvidence`, `Analyzer_HasHuman`, `Analyzer_SubjectGeometry`, `Analyzer_DominantColors`, `Analyzer_ProductColor`, `Analyzer_BackgroundColor`, `Analyzer_Exposure`, `Analyzer_MultipleProducts`, `Analyzer_Interior`, `Analyzer_IsIllustration` — each has a named open calibration/validation question in its own `.md`. **This is now the only live backlog item** — the 10 stubs (item 2, below) are deleted, not pending.
2. ~~**Stubs, implementation open (10)**~~ — **removed by [[T-4700]] (2026-07-27)**: `Analyzer_FacePose`, `Analyzer_TextPresent`, `Analyzer_Mannequin`, `Analyzer_LogoPresent`, `Analyzer_CameraAngle`, `Analyzer_IndoorOutdoor`, `Analyzer_ShadowReflection`, `Analyzer_Packaging`, `Analyzer_MaterialTexture`, `Analyzer_LightingDetail`. See the board-sync note above.
3. **Cross-cutting (4, was 5)** — retire `ImageOrderer.ResolveProductType`'s value-sniffing fallback once `Analyzer_ProductType` is validated; unify `ProductTypeMap.json`/`TranslationDictionary.json` vocabulary; segmentation-model milestone for true coverage-ratio masks; standardize CLIP-vs-analyzer write precedence (for whichever stub is re-introduced first). The `Analyzer_Symmetry` bullet closed out for good in T-4700 — `symmetry-score` itself was removed from `ImageNGP.json`, not just left dropped.

**This ticket is an index, not a single unit of work.** Only item 1 (11 real analyzers) remains open now; pick items up individually as calibration/validation work is prioritized. The old Milestone Gates table rows (M6–M8, M10) that depended on the deleted stubs are marked **Superseded** — see the table above.

**What to do:** Orchestrator splits item 1 into per-analyzer follow-up tickets as calibration work is picked up.

**Acceptance:** Each of the 11 remaining analyzers' `.md` open question is answered and its `jbtodo.md` checkbox checked; `jb/src/core/Services/Matching/Analyzers/jbtodo.md` reflects real remaining state at all times (not batch-updated at the end).

**Files:** `jb/src/core/Services/Matching/Analyzers/jbtodo.md`, `jb/src/core/Services/Matching/Analyzers/*.md`, `jb/src/core/Services/Matching/Analyzers/*.cs`.

---









### T-4900 · ESRGAN toggle + unified final-size upscale (epic)
**Status:** Review | **Profile:** P0-orchestrator
**Found by:** 2026-07-28 upscale-perf investigation (see `memory/project_transform_upscale_bottleneck.md`)

**All five children are implemented (2026-07-29).** [[T-4905]] is Done (reviewer Approve). T-4910/T-4920/
T-4930/T-4940 are code-complete and green but sit at `Review` — the P1/P4 reviewer gate has not run on them.
Decisions in `jb/docs/PRISM-transform-generate.md` → "Unified upscale"; API field in `PRISM-api.md`.

**Three defects the epic uncovered and fixed along the way** (user decisions, 2026-07-29 — all three were
blocking the epic's own premise, not scope creep):
1. **The bounding box was never rescaled after upscale.** `UpscaleAsync` enlarged the bytes while
   `lambda.BoundingBox` stayed in original-image pixels, so `Tx_CenterAndStretch` cropped an
   original-coordinate rect out of an enlarged image — wrong region, and the canvas was still sized off the
   un-scaled bbox, so the output never reached 800px anyway. The ON path was paying full ESRGAN cost for an
   output that met neither the crop nor the size it claimed. Geometry now scales with the pixels.
2. **`Tx_CropSquare.Transform` never applied its crop.** It recorded a `CropRectangle` on the OutputRecord
   without touching `ProcessedBytes`, and Export ships `ProcessedBytes` — so the exported file was the whole
   frame while the manifest claimed a square. Under `BypassPhenotypes = true` that is the route every
   intersecting image takes. It now crops the bytes.
3. **Upscale sized against the pre-promotion box.** Subject promotion and shadow accounting ran in
   `ImageTransformer` *after* preprocessing, so upscale measured a box Transform then replaced. Promotion +
   shadow accounting moved into `ImageTransformer.FinalizeGeometry`, called from `PreprocessAsync` before the
   upscale decision.

Tracking ticket. **Problem:** the upscale stage (Real-ESRGAN, in `ImagePreProcessor.UpscaleAsync`) is the
pipeline's dominant cost — measured **122.9s per 800×800 image on the GPU** with the old fixed-64 model,
and even after the dynamic-model fix (T-4905) it's ~**10s/image** of genuine Real-ESRGAN compute. On a
~1900-image set that is still hours, and desktop users without a capable GPU will not tolerate it.
**Goal:** make ESRGAN opt-in. Add a user-set toggle (**default OFF**); when OFF, upscale with plain
Lanczos, and only *as little as needed* to clear the final-image 800px bar (capped at +33%). When ON,
ESRGAN runs (now fast via the dynamic model). Both paths target the **same** exact final-output-size bar
(unified — user decision 2026-07-28).

**Settled decisions (user, 2026-07-28):** (1) shortfall — if the applicable cap can't reach the bar,
**KO the image** (fail-loud, like today's upscale-exceeded KO); (2) targeting — **unified**: ON and OFF
both target final ≥ bar (ON caps at the existing ESRGAN `MaxUpScaleFactor`, OFF caps at the new
Lanczos-only cap); (3) scope — **includes the workbench UI** toggle; (4) bleed images — target the output
dimension **directly, no margin term** (only zero-intersection images get the `×(1+2·margin)` discount);
(5) **exactly one upscale location** is mandatory — the final size is *exactly* computable pre-transform
from the already-known bbox + intersection state + margin config (reuse each routing's canvas-size
formula), so upscale stays where it is (`ImagePreProcessor.UpscaleAsync`) with an exact final-size calc —
no post-transform move, no split, no prediction/approximation.

**All values from config, never hardcoded** (no-shadow-defaults rule): reuse `MinOutputWidth` (800) as the
FINAL-image bar; new Lanczos-only cap key (proposed `Output.Images.Resize.MAXIMUM_UpScale_LanczosOnly` =
1.33 → `PrismConfiguration.MaxLanczosOnlyUpScaleFactor`); margin from `CropTransformSettings.WhiteSpaceMargin`
(0.042, transform_Config — note the cross-config read). Children: T-4905 (done, review pending), T-4910,
T-4920, T-4930, T-4940. Index ticket, not a unit of work.

**Files:** `AGENT-TICKETS.md`, `memory/project_transform_upscale_bottleneck.md`.

---

### T-4905 · Dynamic-shape ESRGAN export + even-dimension padding
**Status:** Done | **Profile:** P4-critical-architecture
**Review:** Approve (2026-07-29)
**Found by:** [[T-4900]]

**Reviewer verdict (2026-07-29): Approve, no defects.** The review did not take the ticket's prose on faith —
it loaded both `.onnx` files and hashed all 702 initializers in each: identical SHA256, identical 1226-node
graph, the sole difference being the declared input (`[batch_size,3,64,64]` → `[batch,3,height,width]`). The
even-padding math was traced by hand for the dynamic branch: `overlap=0`/`discard=0` forces exactly one tile,
every in-bounds pixel gets weight 1.0 so `NormalizeAccumulator` never divides by zero, and the bounds checks
drop precisely the padded-then-doubled rows — a top-left crop to `src×2` with no off-by-one. Fixed-64 tiling
confirmed untouched. Upscale suite run in the foreground: 17/17. One non-blocking observation: the new test is
black-box at `Upscaler.Upscale` level, so it would also pass on the old tiling path — not a gap for the
shipped config, but a more surgical `RunTiled`/`RoundUpToEven` unit test would be sharper.

**Implemented 2026-07-28.** The committed `Real-ESRGAN_x2plus.onnx`
had a fixed `[1,3,64,64]` input, so an 800px image was upscaled as **625 serialized 64×64 tile Runs**
(~0.2s DirectML dispatch overhead each = 122.9s). The RRDBNet is already spatially size-agnostic
internally (pixel_unshuffle derives shape from `Shape(input)`; both Resize use scales `[1,1,2,2]`); only
the declared input shape pinned it to 64. A **metadata-only** edit (input dims → dynamic `height`/`width`,
weights untouched, bit-identical output) makes it accept whole images in one Run. Proven on the GPU:
**122.9s → 10.19s, ~12×**, correct 1600×1600 output. Changes landed: `Prism_Config.json`
`Models.Upscale.Path` → `Real-ESRGAN_x2plus_dynamic.onnx`; `Upscaler.RunTiled` rounds the whole-image
(dynamic) tile up to even H/W — the `pixel_unshuffle(2)` rejects odd dims and the existing pad+accumulator
clips the ×2 overshoot back; new `UpscalerTests.Upscale_OddSizedImage_ProducesExactlyDoubledOutput` (401×399
→ 802×798 real inference). Whole-image single-pass is the chosen mode; a configurable capped tile (e.g.
512) is the documented fallback if a large image ever OOMs the GPU. Acceptance: reviewer confirms the
metadata-only diff is lossless and the even-padding math; Upscale suite green (17/17). The dynamic `.onnx`
is gitignored (too big for git) and lives in the source tree next to the fixed-64 backup.

**Files:** `jb/src/core/config/Prism_Config.json`,
`jb/src/core/Services/Upscale/Engine/Upscaler.cs`,
`jb/src/tests/Prism.Services.Upscale.Tests/Upscale/UpscalerTests.cs`.

---

### T-4910 · Exact final-output-size calculator (shared helper)
**Status:** Review | **Profile:** P4-critical-architecture
**Found by:** [[T-4900]]

**Implemented 2026-07-29 — awaiting reviewer Approve.** New `FinalOutputSize`
(`jb/src/core/Services/Transform/FinalOutputSize.cs`, compiled into the `Prism.Services.Transform` Engine
assembly so `Tx_CenterAndStretch` can reach it; `Prism.Core` references that assembly, so `ImagePreProcessor`
can too). It owns four things: `HasEdgeIntersect`, `RoutesToCenterAndStretch` (the routing predicate, now
also used by `ImageTransformer.SelectTransformer` and `ApplyShadowAccounting` — one predicate, no copies),
`CenterAndStretchCanvasSize` (which `Tx_CenterAndStretch.CropResizeAndStretch` now calls instead of holding
its own copy of the formula), and the forward/inverse pair `LongestDimension` / `MinimalScaleToReach`.

The inverse is not solved algebraically: it takes the continuous inverse of the canvas formula — provably
never above the answer, since floor/even/trim only ever shrink the canvas — and steps up against the forward
function until the bar is cleared. Converges in ≤3 passes and cannot land a pixel short the way hand-derived
algebra can.

**Scope grew past "no behavior change yet"** because two of the three defects listed on [[T-4900]] sit inside
this ticket's remit: geometry promotion had to move ahead of upscale (new `ImageTransformer.FinalizeGeometry`,
called from `PreprocessAsync`; `TransformSeed.Resolve` moved above the preprocess call in `TransformService`;
promotion result now recorded on `ImageRecord_LAMBDA.SubjectGeometryPromoted` so the evidence line survives
the move), and `Tx_CenterAndStretch` had to be made to read the shared helper for the "single source of
truth" acceptance to mean anything.

**Acceptance met.** `FinalOutputSizeTests` (10 assertions across 8 facts) pins literal pixel counts, not
re-derivations of the same expression: the 1800→1948 worked example, the bleed case (`min(W,H)`, no margin
term), the 740/739 boundary from both sides, minimality at 741 (no scale) vs 739 (scale), and the routing
predicate's three cases. Transform suite 83/83.

Original spec follows.

Extract a single deterministic function that, given the salient bbox + intersection state + margin, returns
the **exact** final-output longest dimension the pipeline will produce — reusing each routing's own
canvas-size formula so upscale and the Transform stage never disagree. Two branches (user decision 4):
**zero-intersection** → `Tx_CenterAndStretch` canvas geometry: `canvasSize = (floor(bbox_longest·(1+2·margin))`
`made even) − 2`; **bleed/intersection** → the bleed routing's output longest dim, **no margin term**. The
routing split (zero-intersection vs bleed) must use the *same* predicate as `ImageTransformer.SelectTransformer`
so the calc matches the routing that will actually run. Both the upscale-scale logic (T-4920) and, ideally,
the Tx stage reference this one helper. Cross-stage note: the calc lives where upscale runs
(`ImagePreProcessor`, preprocess) but encodes Transform-stage geometry — keep it a pure function of
(bbox, intersection, margin, routing-config) with no side effects. Acceptance: unit tests pin exact sizes
against `Tx_CenterAndStretch`'s worked example (bbox 1800, margin 0.042 → canvas 1948) and a bleed case;
helper is the single source of truth. No behavior change yet.

**Files:** `jb/src/core/Services/Matching/ImagePreProcessor.cs` (or a new shared geometry helper class),
`jb/src/core/Services/Transform/Engine/Tx_CenterAndStretch.cs`,
`jb/src/core/Services/Transform/ImageTransformer.cs`.

---

### T-4920 · Unified upscale-scale + ESRGAN/Lanczos gate + KO
**Status:** Review | **Profile:** P1-feature-worker
**Found by:** [[T-4900]]

**Implemented 2026-07-29 — awaiting reviewer Approve.** `UpscaleAsync` rewritten to the unified model:
minimal scale from `FinalOutputSize.MinimalScaleToReach(MinOutputWidth, …)`, then the toggle picks resampler
and cap only — ESRGAN (local session or the remote host) to `MaxUpScaleFactor`, local Lanczos4 to the new
`MaxLanczosOnlyUpScaleFactor`. Past the applicable cap → `PREPROCESS_UPSCALE_EXCEEDED`, and the OFF message
appends "Enable ESRGAN upscaling to process this image." The too-small KO is retained and now measures the
promoted box. New config key `Output.Images.Resize.MAXIMUM_UpScale_LanczosOnly` = 1.33, `RequireDouble` +
`AssertPositive` + a new invariant that it may not exceed `MAXIMUM_UpScale`.

**Also here (T-4900 defects 1 and 2):** `ScaleGeometryToUpscaledImage` moves `BoundingBox` and
`LegacySalientBox` into the enlarged space and the BGR `Mat` handed downstream is re-decoded from the new
bytes; width and height are scaled first and never clamped so the longest side lands on exactly the pixel
count the scale was derived from, with the origin absorbing the ≤1px rounding overhang. `Tx_CropSquare` now
writes its cropped bytes and crops against the decoded image's own dimensions. Deliberately not scaled:
`ImageRecord_Base.Width`/`Height` (the original-resolution contract Export's upscale-manifest todo depends
on) and `lambda.Subject` (pre-upscale evidence, self-consistent with its own mask).

**Two consequences worth knowing before tuning any of these numbers:**
- **740, not 800, is the pass-through threshold** on the centre-and-stretch route. Images with a 740–800px
  bbox used to be upscaled and now are not — that is the "reduces ESRGAN work" effect, and it is why a
  re-run's KO/upscale counts will not match older evidence.
- **The Lanczos-only cap is unreachable on the centre-and-stretch route at current config values.** A bbox at
  the 570px input floor needs 740/570 = 1.30×, already inside 1.33×. The OFF-mode KO can only fire on the
  bleed route, for images whose *shorter side* is under 602px. This falls out of the numbers; it is not a
  designed guarantee, and changing `MinInputSizeInPixels`, `MinOutputWidth`, `WhiteSpaceMargin` or either cap
  changes it. Documented in `PRISM-transform-generate.md` and asserted by the test comments.

**Acceptance met.** `UpscaleGateTests` (8 facts, `jb/src/tests/Prism.Core.Tests/Services/`) — no-upscale when
already clear, OFF→Lanczos locally with zero calls to the ESRGAN service, ON→ESRGAN service reached, OFF cap
KO with the toggle named, ON processing the same image, ON past 1.42 KO'ing without the remedy sentence,
too-small KO retained, and geometry-follows-pixels measured against the returned image rather than against
the computed scale. Geometry is pinned by putting an exact `SubjectDetection` on the record rather than by
crafting pixels the detector has to rediscover, so the tests are deterministic and GPU-free. `RemoteUpscale
RoutingTests` updated to the new bar and now also asserts the final size clears it. Core 153/153 (incl. 10
CiMini pipeline-integration), Transform 83/83, Matching 230/230, Upscale 17/17, Generate 10/10.

Original spec follows.

Rewrite `ImagePreProcessor.UpscaleAsync` to the unified model. Using T-4910's exact final-size calc,
compute the **minimal** scale `s ≥ 1.0` such that the computed final output ≥ `MinOutputWidth` (as little as
possible to cross the bar). Then branch on the toggle: **ON** → ESRGAN (dynamic model), cap `s ≤`
`MaxUpScaleFactor` (existing, 1.42); **OFF (default)** → Lanczos, cap `s ≤ MaxLanczosOnlyUpScaleFactor`
(new config, 1.33). If the required `s` exceeds the applicable cap → **KO** (reuse `PREPROCESS_UPSCALE_EXCEEDED`;
OFF message names the toggle: "enable ESRGAN upscaling to process this image"). Retain the existing
too-small KO (`largest < MinInputSizeInPixels`). Add the new config key following no-shadow-defaults
(`required`, no in-code default). Note the current ON path targets the *bbox* reaching `MinOutputWidth`;
unifying moves it to the *final-image* bar (margin-aware for zero-intersection), which reduces ESRGAN work.
Acceptance: unit tests for OFF (Lanczos, +33% cap, KO past it, margin discount on zero-intersection, direct
on bleed), ON (ESRGAN, 1.42 cap), and the minimal-scale property; the Lanczos path uses the same resampler
family as the existing top-up. Lanczos-only default keeps a full run's upscale cost near-zero.

**Files:** `jb/src/core/Services/Matching/ImagePreProcessor.cs`,
`jb/src/core/config/Prism_Config.json`,
`jb/src/core/config/` (new `MaxLanczosOnlyUpScaleFactor` binding + its config class),
`jb/src/tests/Prism.Services.Matching.Tests/` (or the suite owning ImagePreProcessor).

---

### T-4930 · ESRGAN toggle plumbing (per-job parameter, default OFF)
**Status:** Review | **Profile:** P1-feature-worker
**Found by:** [[T-4900]]

**Implemented 2026-07-29 — awaiting reviewer Approve.** `PrismProcessingParameters.AllowEsrganUpscale`
(no initializer, so an omitted field is false), `PrismProcessRequest.AllowEsrganUpscale`, mapped in
`PrismProcessIngressReader`, read once in `TransformService` and passed to `PreprocessAsync`.

**Deviation from the spec, deliberate:** the flag is read off `matched.Ingest.Parameters` inside
`TransformService` rather than threaded as a method argument like `headcut`. The parameters already ride
inside `MatchingResult` across the matching→transform HTTP boundary — the ServiceHost route reads `Transform`
and `Headcut` exactly this way — so one read cannot be dropped at a call site, and the alternative was
signature churn across `ITransformService`, `Pipeline`, `PrismService`, the ServiceHost route and the HTTP
client for a boolean already on the record.

`PreprocessAsync` has only two call sites (`TransformService` and `RemoteUpscaleRoutingTests`); the parameter
is required, not defaulted, so a new call site cannot silently inherit the wrong mode. Match-stage usage
checked: there is none.

**Acceptance met** except one item that has no home: `ProcessingParametersRoundTripTests` covers the
service-boundary round-trip under `JsonSerializerDefaults.Web`, omitted-means-false, and explicit-true. The
get-only-dict trap does not apply — these are `bool { get; init; }`. **Not covered:** the
`PrismProcessRequest` → `PrismProcessingParameters` mapping itself, because there is no `Prism.Api` test
project and the request record is `internal`. Follow-up ticket territory, not a defect in this work.

Original spec follows.

Add a per-job boolean (proposed `AllowEsrganUpscale`, **default false**) to `PrismProcessingParameters`,
accept it on the `POST /PRISM/process` multipart request, and thread it through `TransformService` →
`ImagePreProcessor.PreprocessAsync`/`UpscaleAsync` so the T-4920 gate can read it. Confirm every call site
of `PreprocessAsync` (at least `TransformService`; verify Match-stage usage) receives it. Default-off means
an omitted field yields Lanczos-only. Acceptance: request round-trips the flag; default-off verified when
absent; a job with the flag on routes to ESRGAN; service-boundary round-trip test (mind the get-only-dict
trap from the microservices split — `[JsonConstructor]` if needed). Scope: plumbing only; the OFF/ON
behavior is T-4920.

**Files:** `jb/src/core/Models/PrismProcessingParameters.cs` (or wherever job params live),
`jb/src/api/` (process endpoint), `jb/src/core/Services/Transform/TransformService.cs`,
`jb/src/core/Services/Matching/ImagePreProcessor.cs`.

---

### T-4940 · Workbench UI toggle for ESRGAN upscaling
**Status:** Review | **Profile:** P1-feature-worker
**Found by:** [[T-4900]]

**Implemented 2026-07-29 — awaiting reviewer Approve.** Added as a fifth entry in
`JobParameterPanel`'s `binaryParameterFields` ("High-quality upscaling (ESRGAN — slower)",
`request.allowEsrganUpscale`), so it renders through the same checkbox path as the existing four rather than
introducing a parallel control. `allowEsrganUpscale` added to the `PrismProcessingParameters` TS interface,
to `defaultParameters` in `WorkbenchShell` as `false`, and to both request builders in `prismApiClient`
(the match-lite builder hardcodes `false` alongside its other disabled options). `npm run typecheck` and
`npm run build` both green.

Note: Headcut is on `PrismProcessingParameters` server-side but is not on `PrismProcessRequest` and has no UI
control — it can't be set by any caller today. Out of scope here; worth its own ticket.

Original spec follows.

Surface the toggle in the Next.js workbench (`jb/src/workbench/web`) as an unchecked-by-default checkbox
(e.g. "High-quality upscaling (ESRGAN — slower)"), wired to the T-4930 request field. Match existing
process-option controls (Transform/Headcut). Acceptance: unchecked by default; submitting checked sends the
flag on; `npm run typecheck` + `npm run build` green. Scope: UI + request wiring only.

**Files:** `jb/src/workbench/web/` (process-options component + API client).

---

### T-4942 · PipelineIntegrationTests fail when the solution runs projects in parallel
**Status:** Ready | **Profile:** P4-critical-architecture
**Found by:** [[T-4800]] completion pass, 2026-07-28 — **blocks Done on the T-4800 children**

`dotnet test jb/src/PRISM.sln` fails all 7 `PipelineIntegrationTests.CiMini_*` tests, each in under 1ms —
the signature of the shared `PipelineFixture` failing to construct, not seven independent failures. The
same project passes **142/142 when run on its own**, reproducibly.

**Cause:** the runner executes test projects in parallel, and the T-4800 stage move made the Matching
suite heavy and long-running (~3s → ~95s of OpenCV subject detection plus the shared DirectML YOLO
session). It now overlaps `Prism.Core.Tests`'s pipeline fixture, which runs a whole real pipeline of its
own. **Effect:** two projects contend for the same GPU/ONNX and job-temp resources at the same time.
**Consequence:** the solution-wide command — the one in `CLAUDE.md` and the one CI runs — is red, while
every project is green individually. Verified reproducible across three solution runs.

**Already fixed and NOT part of this ticket:** the intermittent `Test host process crashed` in the
Matching suite was root-caused to a test bug, not contention — `img.Set(y, x, new Scalar(...))` against a
`CV_8UC3` Mat in three `SubjectDetectorTests` cases. `Mat.Set<T>` writes `sizeof(T)` bytes, and `Scalar`
is four doubles (32 bytes) into a 3-byte pixel: a 29-byte overrun per call that ran off the end of the
buffer and corrupted the native heap. Fixed by using `Vec3b`. The Matching suite now runs 230/230 clean,
six times consecutively. **That fix also uncovered a real failure the corruption had been masking** — see
[[T-4948]].

**Production exposure investigated and CLEARED (2026-07-28).** The obvious worry was that the same driver
fault could hit real deployments, since the three GPU guards (`ImageClassifier.RunLock`,
`YoloDetector.RunLock`, `Upscaler._sessionLock`) are `static` and therefore coordinate threads within one
process only. Two measurements say otherwise:

1. **One process, 5 concurrent jobs** (the configured `MaxConcurrentJobs`): all 5 completed, 14/14 images
   OK each. Durations 73/85/100/112/124s — an even ~12s staircase, which is the signature of the existing
   locks already serializing GPU work. Nothing runs truly simultaneously, so there is nothing to fault.
2. **Two real processes on one GPU** — a dedicated `PRISM_SERVICE=upscale` ServiceHost running
   Real-ESRGAN alongside the API running CLIP + YOLO, wired by `PRISM_UPSCALE_URL`: 4 concurrent jobs, all
   100% OK, no fault. Confirmed non-vacuous: the upscale host logged real `POST /prism-service/upscale`
   calls returning 200 after 38.8s / 45.6s / 51.8s, and the API created its own ONNX sessions concurrently.

So the multi-process deployment is **not** demonstrably exposed, and no product-side GPU coordination
(named mutex, startup GPU-ownership check, queue rework) is justified on current evidence. Caveat: the
fault is timing-dependent — it reproduces only ~4 runs in 7 even where it does occur — so a single clean
4-job run is good evidence, not proof.

**What that leaves.** The distinguishing feature of the test harness is not steady-state inference but
**session churn**: `PipelineFixture` builds a whole `PrismService` (146 MB CLIP + 37 MB YOLO into fresh
sessions) and disposes it, while the Matching suite does its own session work concurrently. Device
init/teardown, not inference, is the likely fragile point. Treat this as a test-harness defect.

**Next steps:** serialise the two GPU-touching test projects (`-m:1` in the documented command and
`ci.yml`), or give the fixtures a cross-process mutex around *session acquisition* specifically. Either
way, CI should also assert an expected minimum test count — a crashed run still prints
`Passed! - Failed: 0, Passed: 176`, which reads as success unless you notice the count is short.

**Files:** `jb/src/tests/Prism.Tests.Shared/PipelineFixture.cs`, `jb/src/tests/Prism.Core.Tests/`,
`jb/src/tests/Prism.Services.Matching.Tests/`, `.github/workflows/ci.yml`, `CLAUDE.md` (test commands).

---

### T-4948 · White-on-white detection has an undocumented contrast floor (~40 grey levels)
**Status:** Ready | **Profile:** P1-feature-worker
**Found by:** [[T-4800]] completion pass, 2026-07-28

Subject detection opens with `Cv2.BilateralFilter(bgr, denoised, 5, 40, 40)` before the texture measure
runs. A bilateral filter deliberately smooths variation *below* its `sigmaColor`, so with `sigmaColor = 40`
any surface texture weaker than roughly 40 grey levels is erased before it can be measured. **Cause:** the
denoise step and the texture measure disagree about what counts as signal. **Effect:** measured directly —
an achromatic 80×80 weave at amplitude 15 (240 vs 255 on white) is not detected at all and the detector
falls back to whole-frame; the same pattern at amplitude 60 (195 vs 255) is detected cleanly.
**Consequence:** white-on-white is one of the four scenarios this detector was ported to solve, and it
works only above a contrast floor nobody has characterised against real product photography. Low-contrast
white fabric on a white sweep — the canonical hard case — may sit under that floor.

Note the reference prototype ran detection at 2400px against our 1024 (`MaxAnalysisSize`), which changes
how much a fixed-amplitude weave survives downscaling; the two knobs interact and should be calibrated
together, not in isolation.

**Next steps:** measure the achromatic-contrast distribution of real white-on-white product shots, then
decide whether to lower `sigmaColor`, move the denoise after the texture measure, or accept the floor and
document it as a known limitation. The unit test
`SubjectDetectorTests.Detect_WhiteOnWhiteWithFineTexture_BoxesTextureRegion_NoIntersects` pins the
currently-supported amplitude and carries a comment saying explicitly not to lower it to force a pass.

**Files:** `jb/src/core/Services/Matching/SubjectDetector.cs` (`BuildAnalysisLayers`),
`jb/src/core/config/ClassifyConfig.json`.

---

### T-4945 · Validate the hard-shadow threshold against labelled data + visual A/B
**Status:** Ready | **Profile:** P1-feature-worker
**Found by:** [[T-4800]] SPACINI29 evidence run, 2026-07-28

Two related calibration gaps left open by the T-4800 completion pass, both needing data this repo does
not have yet.

**Hard-shadow threshold.** `HardShadowEvidenceFraction` was 0.01, which fired on 86/86 SPACINI29 images —
no discrimination, while trimming 6% off the bottom of every centred image. It is now **0.05** (23/86
fire), chosen by the user against the measured distribution (min 0.0113 / median 0.0371 / p90 0.0702 /
max 0.1243). That is a reasoned choice on an *unlabelled studio set*, not ground truth. Label a set for
hard vs soft shadow and re-tune. `SubjectDetection.HardShadowStrippedFraction` carries the raw per-image
measurement precisely so this can be redone without re-instrumenting. The user has flagged intent to
refine the shadow detector itself later, which would change the distribution.

**Centering A/B.** [[T-4850]]'s acceptance asks that the subject box show "equal-or-better centering" than
the legacy salient box. The measured comparison (71 promoted images) shows close agreement on the bulk —
centre shift median 15.5px on ~3500px images, 51/71 within 50px, area ratio median 1.027 — with a tail of
~20 disagreements clustered at mid confidence (0.48–0.61). Geometry alone cannot say which box is
*better* centred. Eyeball the disagreement tail (port `save_debug_overlay` from the reference prototype)
or score against labelled product bounds, then close this out.

**Also unexercised:** SPACINI29 is entirely `SOLIDCOLOR`, so toggle (b) never fired and the B2
`HeroDetectionOnSteroids` path has no real-data coverage at all. Needs a real-life-background dataset.

**Files:** `jb/src/core/config/ClassifyConfig.json`, `jb/src/core/Services/Matching/SubjectDetector.cs`,
`jb/docs/reference/process_images.py` (overlay reference).

---

### T-4950 · SubjectMask crosses the service boundary unread
**Status:** Ready | **Profile:** P1-feature-worker
**Found by:** [[T-4800]] completion pass, 2026-07-28

`SubjectDetection.MaskPng` is produced by both producers (`classical-cv` encodes a full-resolution binary
PNG per image; `alpha` likewise) and asserted in tests, but **no production code reads it** — Transform
routes and crops on `Box` plus the intersect flags, and T-4870's evidence deliberately excludes the pixel
mask. Cause: the mask was designed for a v2 consumer (mask-aware fill / seam-carving, deferred) and built
ahead of it. Effect: since the T-4800 completion pass moved detection upstream into the Classify
refinement chain, the mask is now created in Matching and serialized across the Matching→Transform HTTP
boundary in a distributed deployment — where previously it was created inside the Transform service and
never left the process. Consequence: every image pays a base64 PNG round-trip for a payload nothing
consumes; on a ~1900-image batch that is real bandwidth for zero benefit. Not a correctness bug and not
urgent — the mask is a deliberate forward-looking part of the contract. Decide between: keep as-is;
`[JsonIgnore]` it so it stays in-process only; or gate production behind a config flag until a consumer
exists. Measure the actual per-image payload before choosing.

**Files:** `jb/src/core/Models/SubjectDetection.cs`, `jb/src/core/Services/Matching/SubjectDetector.cs`,
`jb/src/core/lib/Ingress/AlphaSubjectCapture.cs`.

---

### T-4955 · Derived edge features go stale when the subject box is promoted
**Status:** Ready | **Profile:** P1-feature-worker
**Found by:** [[T-4800]] review of [[T-4850]], 2026-07-28

`ImageTransformer.PreferSubjectGeometry` overwrites `intersects-top/bottom/left/right` with the detector's
signals, but leaves `intersection-count`, `fully-in-frame` and `occlusion-level` holding values
`ImageFeatureAnalyzer` derived earlier from the *old* heuristic intersects. Cause: promotion updates the
four source features but not the three derived from them. Effect: after a promotion the feature snapshot
is internally inconsistent — the intersect booleans describe the detector's geometry while the derived
three describe the salient-box geometry. Consequence: harmless today, because nothing in Transform reads
the derived three and phenotype assignment has already run by that point. It becomes a live bug the moment
anything downstream of Transform reads them, or if phenotype-driven routing is revived. Either recompute
the three at promotion time or document them as pre-promotion-only.

**Files:** `jb/src/core/Services/Transform/ImageTransformer.cs`,
`jb/src/core/Services/Matching/Classify/ImageFeatureAnalyzer.cs`.

---

### T-4960 · Alpha-derived box should retire SubjectGeometry's colour-distance fallback
**Status:** Ready | **Profile:** P1-feature-worker
**Found by:** [[T-4800]] completion pass, 2026-07-28

`jb/src/core/Services/Matching/Analyzers/Analyzer_SubjectGeometry.md` carries an open todo: *"Fallback box
on transparent-background images should use alpha instead of color distance."* The T-4830 ingress alpha
path now captures exactly that — an exact box and mask built from the real transparency channel before
normalization flattens it onto white — and puts it on the record as `SubjectDetection` with
`Producer = "alpha"`. Cause: the two pieces were built for different tickets and are not yet connected.
Effect: `Analyzer_SubjectGeometry` still falls back to colour distance on transparent-background images
even though exact geometry is now sitting on the same record. Consequence: measurably worse geometry
features on precisely the images where the best possible answer is already available for free. Wire the
analyzer to prefer the alpha subject, then close that todo per the todo lifecycle.

**Files:** `jb/src/core/Services/Matching/Analyzers/Analyzer_SubjectGeometry.cs`,
`jb/src/core/Services/Matching/Analyzers/Analyzer_SubjectGeometry.md`.

---

### T-4970 · First-pass phenotype assignment validation
**Status:** Blocked | **Profile:** P1-feature-worker
**Blocked-by:** the [[T-4900]] epic (user decision, 2026-07-29)
**Found by:** [[T-2600]] rewrite, 2026-07-29 — the near-term step T-2600 had described but never assigned
to a ticket.

Phenotype assignment produced **zero** results on real images until 2026-07-28: `ImageFeatureAnalyzer.Refine`
threw on every image and `MatchingService`'s non-fatal catch swallowed it, so the counter read 0 and nobody
noticed. That is fixed (refinement failures 86 → 0 on SPACINI29), but **no one has yet looked at what the
pipeline now assigns.** Every claim about phenotype quality on this board predates a pipeline that was
assigning phenotypes at all.

This is the **light** first pass, not [[T-2600]]'s full acceptance bar. No labeled set, no confusion matrix,
no <5% target — those stay with T-2600 and share their labeled-set dependency with [[T-4945]].

**What to do:**
1. Run `prism-evidence-report` on the standard dataset (SPACINI29 for volume; add a second dataset with a
   non-`SOLIDCOLOR` background if one exists — SPACINI29 is entirely solid-colour).
2. Report the actual distribution: how many images get a phenotype, how many fall through to the
   provisional pick, how many get none, and which of the 20 phenotypes never fire.
3. Spot-check by eye whether the assignments are plausible for the images they landed on.
4. State a verdict in plain terms: is assignment good enough to base Transform routing on, or not.

**Why it waits for T-4900:** the measurement instrument is a repeated full-dataset run, and upscale is the
pipeline's dominant cost until the T-4900 toggle lands. Running this first means paying hours per
iteration for a report that will need re-running anyway.

**Acceptance:** a written distribution + verdict recorded on this ticket and in `jb/docs/`, enough for the
orchestrator to make the `BypassPhenotypes` flip decision. Note [[T-4955]] must be fixed before the flip
itself, not before this measurement.

**Files:** `jb/docs/` (report destination), `jb/src/core/Services/Matching/Classify/ImageFeatureAnalyzer.cs`
(read-only — no code change expected).

---

## Verification Rules

- After project/solution setup: `dotnet build jb/src/PRISM.sln`, API run smoke, web `npm run typecheck` + `npm run build`.
- After each milestone: run relevant smoke and record result in milestone table.
- After todo/doc sync: `rg --files -g 'jbtodo.md' jb/src` and `rg -n "^- \[ \]" jb/src`.
- Before advancing milestones: `git status --short` to confirm edits stayed inside assigned ownership.
