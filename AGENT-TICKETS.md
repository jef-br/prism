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
| M5 Classification Groundwork | ImageNGP taxonomy + rule correctness | All Classify `jbtodo.md` decisions answered; ONNX session migrated to singleton ✅ (done 2026-06-29) |
| M6 Human & Model Detection | `hero-is-human`, `contains-mannequin`, `has-human`, `head-visible`, `face-visible` | On-model and ghost phenotypes (`front-on-model-*`, `ghost-front/back/side`) fire correctly on labeled images |
| M7 Orientation & Pose | `hero-orientation`, `pose-type`, `camera-angle`, `top-view` | Packshot orientation-split phenotypes (`front-packshot`, `back-packshot`, `side-packshot`) fire from real signal |
| M8 Product & Packaging | `packaging-visible`, `product-type-label`, `multiple-products` | packshot phenotypes fire from CLIP; `packaging-visible` no longer always UNKNOWN |
| M9 Composition & Spatial | `product-coverage-ratio`, `image-occupancy`, `salient-bbox`, `vertical-centering`, `horizontal-centering` | Composition phenotypes measured; overflow slot assignment accuracy confirmed |
| M10 Semantic & Content | `text-present`, `logo-present`, `dominant-colors`, `lighting` | Content features populated; transform routing that depends on them verified |
| M11 Production Validation | All 26 phenotypes | < 5% misassignment on labeled validation set; no systematic error on any single phenotype |

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
1. ImageNGP taxonomy/feature-combination reconciliation — FROZEN: "Taxonomy is captured in canonical files (ImageNGP.json, ImageRoles.json, imagePhenotypes.md, ImageFeatures.md). No reconciliation action needed at this time."
2. Phenotype production validation (labeled set, confusion matrix, <5% misassignment across 26 phenotypes) — FROZEN: "Premature. Revisit after per-feature Analyzer stubs are substantially resolved and BypassPhenotypes flip is planned."

**Why this ticket is genuinely blocked (not just unattended):** both FROZEN items depend on features no longer being UNKNOWN, which depends on [[T-4000]] replacing the `RecordUnknownFeatures` stub analyzer-by-analyzer. Until enough analyzers land, phenotype assignment can't be validated, so `BypassPhenotypes` stays on and item 2 stays frozen. T-2600 is downstream of T-4000, full stop.

Per-feature CLIP confidence calibration remains a live open concern feeding into this ticket (referenced by `AGENTFEEDBACK.md`'s S109 entry and T-4400's phase-2 closeout review) but is not currently a tracked checkbox in Classify's own `jbtodo.md` — it surfaces wherever a new confidence literal is discovered elsewhere in the codebase.

M5 gate condition: both FROZEN items thaw and get answered (needs T-4000's Analyzer stubs substantially landed + a BypassPhenotypes flip decision); ONNX session migrated to shared/singleton ✅ already done.

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

### T-4000 · Per-feature Analyzer TOC: calibration + stub implementation backlog
**Status:** Ready | **Profile:** P0-orchestrator
**Tracks:** `jb/src/core/Services/Matching/Analyzers/jbtodo.md` (triaged 2026-07-11) — a TOC of 26 items across 3 sections, none previously represented on the ticket board.
**Board sync (2026-07-24):** this entry previously claimed 27 items incl. a 4th "OPEN(1)" item — centralize per-analyzer `*Config.cs` files into a single `AnalyzerConfig.cs` with nested objects. That item is no longer in the source `jbtodo.md` (not present in current file, no history of it being explicitly closed either) and the underlying concern was functionally superseded this week by T-4400's S109 pass: single-consumer `*AnalyzerConfig.cs` classes (`Interior`, `Exposure`, `IsIllustration`, `SubjectGeometry`, `FilenameEvidence`, `MultipleProducts`) were folded as nested `Config` types into their owning `Analyzer_*.cs` files — the opposite direction (decentralized-per-file, not one shared `AnalyzerConfig.cs`), but it resolves the same "scattered standalone config files" complaint. `ColorAnalyzerConfig`/`YoloAnalyzerConfig` stay standalone (genuinely multi-consumer). Item count corrected to 26.

**Problem:** `Analyzers/jbtodo.md` is a checklist pointing at per-analyzer working docs, split into:
1. **Implemented, calibration open (11)** — `Analyzer_ProductType`, `Analyzer_FilenameEvidence`, `Analyzer_HasHuman`, `Analyzer_SubjectGeometry`, `Analyzer_DominantColors`, `Analyzer_ProductColor`, `Analyzer_BackgroundColor`, `Analyzer_Exposure`, `Analyzer_MultipleProducts`, `Analyzer_Interior`, `Analyzer_IsIllustration` — each has a named open calibration/validation question in its own `.md`.
2. **Stubs, implementation open (10)** — `Analyzer_FacePose` (highest value: 6 features, unblocks most on-model phenotypes), `Analyzer_TextPresent`, `Analyzer_Mannequin`, `Analyzer_LogoPresent`, `Analyzer_CameraAngle`, `Analyzer_IndoorOutdoor`, `Analyzer_ShadowReflection`, `Analyzer_Packaging`, `Analyzer_MaterialTexture`, `Analyzer_LightingDetail`.
3. **Cross-cutting (5)** — retire `ImageOrderer.ResolveProductType`'s value-sniffing fallback once `Analyzer_ProductType` is validated; unify `ProductTypeMap.json`/`TranslationDictionary.json` vocabulary; segmentation-model milestone for true coverage-ratio masks; `Analyzer_Symmetry` stays dropped unless an orientation rule wants it; standardize CLIP-vs-analyzer write precedence.

**This ticket is an index, not a single unit of work.** Individual items are gated by the Milestone Gates table (M6 Human & Model Detection through M10 Semantic & Content each name the specific analyzers they depend on); pick items in milestone order, starting with `Analyzer_FacePose` (blocks the most downstream phenotypes, milestone-independent, can start anytime).

**What to do:** Orchestrator splits this into per-analyzer or per-milestone-batch follow-up tickets as work is picked up, rather than one agent attempting all 26 items at once.

**Acceptance:** Each analyzer's `.md` open question is answered and its `jbtodo.md` checkbox checked, in milestone order; `jb/src/core/Services/Matching/Analyzers/jbtodo.md` reflects real remaining state at all times (not batch-updated at the end).

**Files:** `jb/src/core/Services/Matching/Analyzers/jbtodo.md`, `jb/src/core/Services/Matching/Analyzers/*.md`, `jb/src/core/Services/Matching/Analyzers/*.cs`.

---

## Verification Rules

- After project/solution setup: `dotnet build jb/src/PRISM.sln`, API run smoke, web `npm run typecheck` + `npm run build`.
- After each milestone: run relevant smoke and record result in milestone table.
- After todo/doc sync: `rg --files -g 'jbtodo.md' jb/src` and `rg -n "^- \[ \]" jb/src`.
- Before advancing milestones: `git status --short` to confirm edits stayed inside assigned ownership.
