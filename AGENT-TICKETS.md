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
**Blocked-by:** M5 milestone gate — the 2 remaining Classify `jbtodo.md` items are both FROZEN pending prerequisite work, not simply unanswered.
**Board sync (2026-07-24):** re-synced the item list against the source file (was 6 on the board, 2 in the file).
**Board sync (2026-07-25):** correcting the 07-24 note, which claimed "4 closed via the todo lifecycle, decisions in jb/docs" — that flattened five removed items into one disposition. Verified the true history: the Classify `jbtodo.md` went from 7 open items (late June) to 2 (by 8 July, `9144f3e`), and the five that left had **mixed** dispositions, not a uniform lifecycle-close:
- **ONNX session per-run → shared** — resolved: `OnnxSessionFactory.cs` exists, decision recorded in `PRISM-classify.md`/`PRISM-pipeline-core.md`; milestone table dates it 2026-06-29. ✅
- **illustration-technical-drawing scope** — resolved with a real decision, documented (`PRISM-classify.md:171`: no longer a catch-all, requires an `is-illustration` positive signal). ✅
- **interior-shot unreachable in CPU-only** — resolved: `Analyzer_Interior.cs` implemented, sets `interior-detected` feeding the phenotype, config-driven. ✅
- **Gate phenotypes** — not "closed," **implemented and live by design**: the `BypassPhenotypes` PoC flag (`ImageTransformer`) is ON, so routing ignores `SelectedPhenotype` and basic transforms run off geometry alone. It flips off only once phenotype assignment is validated — the same gate as FROZEN item 2 below.
- **`RecordUnknownFeatures()` stub** — **still a live stub** (`ImageFeatureAnalyzer.cs:326`, marks 35+ features UNKNOWN). Not closed with a doc decision; its remaining work — replacing each UNKNOWN with a real measurement — is exactly what [[T-4000]]'s per-feature Analyzer backlog does, so it's effectively relocated to T-4000, not resolved here.

Tracks the 2 remaining items in `jb/src/core/Services/Matching/Classify/jbtodo.md`, both `FROZEN`:
1. ImageNGP taxonomy/feature-combination reconciliation — **answered for real by [[T-4700]] (2026-07-27)**, superseding the earlier "no reconciliation action needed" answer: the taxonomy was actually trimmed to 37 features / 20 phenotypes, all real/reachable, and a `jb/docs/ImageNGP/HowToAddAPhenotype.md` guide now documents the reconciliation process going forward.
2. Phenotype production validation (labeled set, confusion matrix, <5% misassignment across **20** phenotypes, not 26 — [[T-4700]] removed 6 unreachable ones) — still FROZEN: "Premature. Revisit after per-feature Analyzer stubs are substantially resolved and BypassPhenotypes flip is planned." A lighter first-pass validation approach (before/after `prism-evidence-report` diff on the standard dataset, not the full 200-images/phenotype protocol) is defined as part of the Transform-routing follow-up ticket that collapses DetOrderRules.json to 5 product types — that lighter bar is the near-term next step, not this FROZEN item's full bar.

**Why this ticket is genuinely blocked (not just unattended):** item 2 depends on features no longer being UNKNOWN, which depends on [[T-4000]] replacing the `RecordUnknownFeatures` stub analyzer-by-analyzer. Until enough analyzers land, full phenotype assignment validation can't happen, so `BypassPhenotypes` stays on and item 2 stays frozen. T-2600 is downstream of T-4000, full stop. Item 1 is no longer blocking — it's closed.

Per-feature CLIP confidence calibration remains a live open concern feeding into this ticket (referenced by `AGENTFEEDBACK.md`'s S109 entry and T-4400's phase-2 closeout review) but is not currently a tracked checkbox in Classify's own `jbtodo.md` — it surfaces wherever a new confidence literal is discovered elsewhere in the codebase.

M5 gate condition: item 1 answered ✅ ([[T-4700]], 2026-07-27); item 2 thaws once T-4000's Analyzer stubs are substantially landed + a BypassPhenotypes flip decision is made; ONNX session migrated to shared/singleton ✅ already done.

**Files:** `jb/src/core/Services/Matching/Classify/jbtodo.md`, `jb/src/core/Services/Matching/Classify/ImageFeatureAnalyzer.cs`

---

### T-3800 · Match bracket todos: edit-distance gap, substring-rescue perf, fuzzy-fallback future-work note, totalImageTokens precision
**Status:** Ready | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-25) — reviewer pass on the `e2e1f84`+`f40beed` diff, judging the current code on main. Items 1/2/4 verified: fuzzy categorical matching correctly scoped (categorical columns only, distance ≤ 1, both sides ≥ 4 chars, score 0.75, reusing `ModelBuilder.ComputeLevenshteinDistance` same-assembly); `totalImageTokens` = real filename token count, pool size no longer leaks into `stringSignal`; substring-rescue perf a genuine worst-case measurement, not vacuous. All config-driven with no shadow defaults (re-verified `StringMatcher.Config`/`MatchingConfig` all `required`, `MatchingConfig.Load` fail-loud) — the exact thing `f40beed` fixed, confirmed holding. Tests cover fuzzy hits AND misses (distance-2, sub-4-char, non-categorical all correctly rejected) and a real threshold-straddling `totalImageTokens` proof. Build 0 errors, 202/202 Matching tests green (~8 min, foreground). Two non-blocking style nits (Allman braces + property-level XML docs in `StringMatcher.cs`) are pre-existing patterns this diff inherited, not introduced — flagged for a future cleanup, not this ticket. Empirical validation of items 1/4 is a known separately-tracked caveat, not a code defect.
**Tracks:** `jb/src/core/Services/Matching/Match/jbtodo.md` (triaged 2026-07-11).
**Board sync (2026-07-24):** this entry previously listed 3 items; the source `jbtodo.md` has always had a 4th (the fuzzy-fallback future-work note) that was never added to the board. Added below as item 3, matching file order.
**Board sync (2026-07-25):** corrected the 07-24 pass (which mislabeled implemented items as unstarted), then **closed items 2 and 3** via `/todo-finish` (commit `6c60450`) and recorded the reviewer **Approve** above. All four items' code is on main (`e2e1f84`, review-fixed by `f40beed`); verified methods `StringMatcher.CollectFuzzyCategoricalEvidence` (`:309`), `CountFilenameTokens` (`:544`), `SemanticMatcher` using it (`:79`), `SubstringRescuePerfMeasurement.cs`.

Two items remain open in the source `jbtodo.md`, both **implemented on main, awaiting empirical validation** (not implementation):
1. **StringMatcher edit-distance gap** — `CollectFuzzyCategoricalEvidence`, categorical columns only, edit-distance ≤ 1, both sides ≥ 4 chars, score 0.75 (thresholds in `MatchingConfig.json` `stringMatcher.fuzzy*`). Doc-vs-code resolved (code was wrong, tolerance added; `PRISM-match.md` updated). Pending: before/after validation on a labeled set / expanded CiMini (needs a Bracket-3 fuzzy case, e.g. grey/gray).
4. **`SemanticMatcher.totalImageTokens` precision** — `totalImageTokens = stringMatcher.CountFilenameTokens(filename)`; candidate-pool size no longer leaks into `stringSignal`. Unit tests pass. Pending: before/after on a labeled set / expanded CiMini to confirm no accept/reject flips near `SemanticThreshold` — genuinely blocked because 0 of 14 current CiMini goldens reach Bracket 4 (see root `jbtodo.md` CiMini coverage gap).

Closed this session (no longer in `jbtodo.md`): item 2 (`TryMatchBySubstringRescue` perf — measured, "not worth an n-gram index," documented at `PRISM-match.md:66`) and item 3 (fuzzy-fallback 4-layer future-work note — recorded as a "Future Work" section in `PRISM-match.md`, decision "do not build speculatively").

**What to do:** The code and the review are done. The only remaining work is the empirical validation of items 1 and 4, which is blocked on the CiMini expansion (root `jbtodo.md`): a Bracket-3 fuzzy case for item 1, a Bracket-4-reaching image for item 4. Once that golden coverage exists and the before/after runs confirm no unwanted accept/reject shifts, items 1 and 4 close and T-3800 is eligible for `/ticket-finish` (review gate already satisfied).

**Acceptance:**
- Each of the 4 items has either a code change + passing tests, or a documented "measured, not worth it" close with no code change.
- `jb/src/core/Services/Matching/Match/jbtodo.md` items closed and moved to `jb/docs/` per the todo lifecycle.

**Files:** `jb/src/core/Services/Matching/Match/jbtodo.md`, `jb/src/core/Services/Matching/Match/StringMatcher.cs`, `jb/src/core/Services/Matching/Match/NumericMatcher.cs`, `jb/src/core/Services/Matching/Match/SemanticMatcher.cs`, `jb/src/core/lib/Excel/ModelBuilder.cs`, `jb/docs/PRISM-match.md`.

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

### T-4800 · Model-aware subject isolation for Transform (epic)
**Status:** Ready | **Profile:** P0-orchestrator
**Found by:** [[T-4700]] follow-up; folds in the removed root note `TRANSFORM-SUBJECT-ISOLATION-NOTE.md`

Tracking ticket. Design lives in `jb/src/core/Services/Transform/Engine/jbtodo.md` ("Subject Isolation &
Model-Aware Transformation"). Goal: give Transform a real subject mask/box (shadow- and
background-excluded) produced upstream and consumed as pure geometry+fill, plus Excel+CLIP seeding that
steers transform behavior. v1 ports the vendored classical-CV prototype
`jb/docs/reference/process_images.py`; ONNX stays upstream (Transform stays deterministic). Children:
T-4805, T-4810, T-4820 (Wave 0); T-4830 (Wave 1); T-4850, T-4860 (Wave 2); T-4870 (Wave 3). T-4840
(vendor the reference script) is already done. This ticket is an index, not a unit of work.

**Files:** `jb/src/core/Services/Transform/Engine/jbtodo.md`, `AGENT-TICKETS.md`.

---

### T-4805 · Unify Transform/Process entry points (fix latent divergence)
**Status:** Ready | **Profile:** P4-critical-architecture
**Found by:** [[T-4800]]

`Tx_CenterAndStretch.Process` (and the other Tx `Process` methods, per the "precedent" comment in
`Tx_DetailCropper`) ignore the `lambda` parameter and always crop to `FullImageBounds(arr)`, violating
the `IImageTransformation` contract (reuse the lambda's BoundingBox when provided). Not live today — the
deployed transform service routes through `Transform(lambda)` — but a future per-image webservice on
`Process` would diverge from pipeline behavior and ignore the persisted SubjectBox from T-4810.
Acceptance: `Transform(lambda)` and `Process(...,lambda)` funnel through one shared core so identical
geometry → identical output; `Process` reuses the lambda's box when present; all four Tx classes audited;
dead `Tx_LowContrastEnhancement.Enhance` removed (CLAHE moves upstream via T-4830), standalone
`Tx_LowContrastEnhancement.Process` utility retained; build + tests green. Scope: no new transform
behavior — pure de-duplication of the two paths.

**Files:** `jb/src/core/Services/Transform/Engine/IImageTransformation.cs`,
`jb/src/core/Services/Transform/Engine/Tx_*.cs`,
`jb/src/core/Services/Transform/Engine/Utils/Tx_LowContrastEnhancement.cs`.

---

### T-4810 · Persisted subject mask/box contract
**Status:** Ready | **Profile:** P4-critical-architecture
**Found by:** [[T-4800]]

Add a persisted `SubjectMask` + `SubjectBox` (+ per-edge intersect flags) to the image record, produced
upstream and read by Transform. Define the pluggable-producer seam so a segmentation producer
(SAM3 / yolo26s-seg, [[T-2600]]) can replace the v1 classical-CV producer later without touching
Transform. Acceptance: contract types added following the no-shadow-defaults rule; producer interface
defined; round-trips across the service HTTP boundary (get-only dict trap — mirror the microservices-split
`[JsonConstructor]` + round-trip test); no behavior change until a producer populates it. Scope: contract
+ plumbing only, not the detector (T-4830).

**Files:** `jb/src/core/Models/ImageRecord_LAMBDA.cs`, `jb/src/core/Models/ImageRecord_Base.cs`,
`jb/src/core/Services/Matching/ImagePreProcessor.cs`.

---

### T-4820 · Seeding access in Transform
**Status:** Ready | **Profile:** P4-critical-architecture
**Found by:** [[T-4800]]

Thread the already-measured features `product-color`, `background-type`, `background-color`,
`product-type-label` to Transform, and give each lambda access to its `FamilyIDRecord` (today only the
`Family` id string + `ProductTypeId` reach the record). `background-type` is already settled by T-4700
(`SOLIDCOLOR`/`REALLIFE`/`UNKNOWN`) — "flat" = `SOLIDCOLOR`, no reconciliation. (Product-type ids are
being collapsed to 5 by [[T-4710]]; seeding is slug-agnostic.) Acceptance: the four signals +
FamilyIDRecord reachable inside Transform without recomputation; no seeding logic yet (that is T-4860).
Scope: data access only.

**Files:** `jb/src/core/Services/Transform/TransformService.cs`,
`jb/src/core/Services/Matching/Classify/ImageFeatureSnapshot.cs`, `jb/src/core/lib/Excel/FamilyIDRecord.cs`,
`jb/src/core/Models/ImageRecord_LAMBDA.cs`.

---

### T-4830 · Port the v1 subject detector (+ ingress alpha path)
**Status:** Blocked | **Profile:** P1-feature-worker
**Blocked-by:** [[T-4810]] (needs the SubjectMask/SubjectBox contract)
**Found by:** [[T-4800]]

Port the vendored `jb/docs/reference/process_images.py` detector to C#/OpenCvSharp4 in the upstream
producer, one named helper per step (recipe-readable, K&R). Populate `SubjectMask`/`SubjectBox`/intersects/
candidate-shadow evidence. Chroma-plane + texture + shadow-strip-by-shape + Canny corroboration; lightness
never a criterion (shadow exclusion). Add the ingress alpha path: real alpha → build+persist box/mask
before jpg normalization, skip the heuristic path. New detector config follows no-shadow-defaults.
Acceptance: producer populates the contract; unit tests on white-on-white, cast-shadow, gradient
background, bleed-off cases; classify-stage perf delta measured on SPACINI29 vs the 156.5s baseline.

**Files:** `jb/src/core/Services/Matching/ImagePreProcessor.cs` (+ new detector class/config under
Classify), `jb/src/core/lib/Ingress/Importer.cs` (ingress alpha capture, pre-normalization),
`jb/src/core/config/analyzer_Config.json` or `ClassifyConfig.json`.

---

### T-4850 · Consume subject mask/box in Transform
**Status:** Blocked | **Profile:** P1-feature-worker
**Blocked-by:** [[T-4810]], [[T-4830]]
**Found by:** [[T-4800]]

Center/stretch/detail-crop geometry operates on the real SubjectMask/SubjectBox instead of the salient
rectangle; routing (`ImageTransformer.SelectTransformer`) uses the detector's cleaner intersect signals.
Fill stays the existing `Tx_util_BgStretch` (unchanged), just fed the better geometry. Acceptance: routing
+ geometry read the persisted mask/box; A/B vs the current salient box shows equal-or-better centering on
the test set; no fill-tier changes.

**Files:** `jb/src/core/Services/Transform/ImageTransformer.cs`,
`jb/src/core/Services/Transform/Engine/Tx_CenterAndStretch.cs`,
`jb/src/core/Services/Transform/Engine/Tx_DetailCropper.cs`.

---

### T-4860 · Behavior toggles + shadow wiring
**Status:** Blocked | **Profile:** P1-feature-worker
**Blocked-by:** [[T-4820]], [[T-4830]]
**Found by:** [[T-4800]]

Implement the three seeding toggles: (a) product-color ≈ background-color → harder isolation;
(b) background-type not `SOLIDCOLOR` → more hero-detection effort (skip when `SOLIDCOLOR`, for speed);
(c) detector candidate-shadow evidence → shadow-accounting, driving the existing `Tx_CenterAndStretch`
shrink (`shadow-present` was removed by T-4700, so read the detector evidence off the record directly).
Optionally re-declare a detector-measured `shadow-present` feature via `HowToAddAPhenotype.md`. All
thresholds config-driven (no shadow defaults). Acceptance: each toggle unit-tested on a positive and a
negative case; evidence-harness run confirms real behavior, not just green tests.

**Files:** `jb/src/core/Services/Transform/ImageTransformer.cs`,
`jb/src/core/Services/Transform/Engine/Tx_CenterAndStretch.cs`, `jb/src/core/config/transform_Config.json`,
`jb/src/core/Services/Transform/Admin/TransformParameters.cs`.

---

### T-4870 · Extend transform-manifest with detection/toggle evidence
**Status:** Blocked | **Profile:** P1-feature-worker
**Blocked-by:** [[T-4830]], [[T-4850]], [[T-4860]]
**Found by:** [[T-4800]]

Fold detection/mask/box/signal/toggle evidence into the Export `transform-manifest.json` (Todo 4 in
`jb/src/core/lib/Export/jbtodo.md`), via `OutputRecord.SafeSummaryText` per that todo's settled approach.
Do not spawn a parallel evidence store. Acceptance: the transform manifest carries the new evidence for
each image; coordinated with the Export evidence-manifest todo so the two don't collide.

**Files:** `jb/src/core/Services/Transform/Engine/Tx_*.cs`, `jb/src/core/Models/ImageRecord_OUTPUT.cs`,
`jb/src/core/lib/Export/Exporter.cs`, `jb/src/core/config/Prism_Config.json`.

---

## Verification Rules

- After project/solution setup: `dotnet build jb/src/PRISM.sln`, API run smoke, web `npm run typecheck` + `npm run build`.
- After each milestone: run relevant smoke and record result in milestone table.
- After todo/doc sync: `rg --files -g 'jbtodo.md' jb/src` and `rg -n "^- \[ \]" jb/src`.
- Before advancing milestones: `git status --short` to confirm edits stayed inside assigned ownership.
