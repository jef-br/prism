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









### T-4900 · ESRGAN toggle + unified final-size upscale (epic)
**Status:** Ready | **Profile:** P0-orchestrator
**Found by:** 2026-07-28 upscale-perf investigation (see `memory/project_transform_upscale_bottleneck.md`)

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
**Status:** Review | **Profile:** P4-critical-architecture
**Found by:** [[T-4900]]

**Implemented this session (2026-07-28) — awaiting reviewer Approve.** The committed `Real-ESRGAN_x2plus.onnx`
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
**Status:** Blocked | **Profile:** P4-critical-architecture
**Blocked-by:** [[T-4905]]
**Found by:** [[T-4900]]

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
**Status:** Blocked | **Profile:** P1-feature-worker
**Blocked-by:** [[T-4910]], [[T-4930]]
**Found by:** [[T-4900]]

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
**Status:** Blocked | **Profile:** P1-feature-worker
**Blocked-by:** [[T-4905]]
**Found by:** [[T-4900]]

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
**Status:** Blocked | **Profile:** P1-feature-worker
**Blocked-by:** [[T-4930]]
**Found by:** [[T-4900]]

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

## Verification Rules

- After project/solution setup: `dotnet build jb/src/PRISM.sln`, API run smoke, web `npm run typecheck` + `npm run build`.
- After each milestone: run relevant smoke and record result in milestone table.
- After todo/doc sync: `rg --files -g 'jbtodo.md' jb/src` and `rg -n "^- \[ \]" jb/src`.
- Before advancing milestones: `git status --short` to confirm edits stayed inside assigned ownership.
