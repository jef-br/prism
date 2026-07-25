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
**Tracks:** `jb/src/core/Services/Matching/Match/jbtodo.md` (triaged 2026-07-11).
**Board sync (2026-07-24):** this entry previously listed 3 items; the source `jbtodo.md` has always had a 4th (the fuzzy-fallback future-work note) that was never added to the board. Added below as item 3, matching file order.
**Board sync (2026-07-25):** correcting the 07-24 pass — it described items 1 and 4 as unstarted decisions/changes, but the source `jbtodo.md` Answer fields show all the code already landed on main (commit `e2e1f84`, 2026-07-17, rescued from an uncommitted worktree) and was even review-fixed (`f40beed` moved the fuzzy thresholds to `MatchingConfig.json`). Verified the methods exist on main: `StringMatcher.CollectFuzzyCategoricalEvidence` (`:309`), `StringMatcher.CountFilenameTokens` (`:544`), `SemanticMatcher` using it (`:79`), `SubstringRescuePerfMeasurement.cs`. So items 1/2/4 are **implemented, pending an empirical validation run** — not pending implementation. No `**Review:** Approve` line has ever been recorded on this block, so /ticket-finish is blocked until a reviewer verdict is added regardless of validation.

Tracks four open items, each fully detailed (impact, industry-standard framing, recommended solution) in the source `jbtodo.md`. Every item's `Answer:` field carries the current state:
1. **StringMatcher edit-distance gap** — **implemented on main** (`e2e1f84`): `StringMatcher.CollectFuzzyCategoricalEvidence`, categorical columns only, edit-distance ≤ 1, both sides ≥ 4 chars, evidence score 0.75 (thresholds in `MatchingConfig.json` `stringMatcher.fuzzy*`). Doc-vs-code call resolved (code was wrong, tolerance added; `PRISM-match.md` updated). Pending: before/after validation on a labeled set / expanded CiMini (needs a fuzzy Bracket-3 case, e.g. grey/gray).
2. **`TryMatchBySubstringRescue` perf** — **measured on main** (`e2e1f84`, `SubstringRescuePerfMeasurement.cs`): 250 unmatched ≈ 336 ms, 2,500 ≈ 1.1 s at 3,000-family scale — "not worth an n-gram index." Ready for `/todo-finish`, no code change, no validation dependency.
3. **Fuzzy-fallback future-work note** (record-only, not an active bug) — forward-looking: how far the current single-bound Levenshtein fallback is from a genuine 4-layer (dictionary + stemming + fuzzy + semantics) matcher. The `jbtodo.md` recommendation already resolves it as "do not build speculatively." Ready for `/todo-finish` as a documentation-move into `jb/docs/`, no code, no validation dependency.
4. **`SemanticMatcher.totalImageTokens` precision** — **implemented on main** (`e2e1f84`): `totalImageTokens = stringMatcher.CountFilenameTokens(filename)`; candidate-pool size no longer leaks into `stringSignal`. Unit tests pass. Pending: before/after on a labeled set / expanded CiMini to confirm no accept/reject flips near `SemanticThreshold` — genuinely blocked because 0 of 14 current CiMini goldens reach Bracket 4 (see root `jbtodo.md` CiMini coverage gap).

**What to do:** Items 2 and 3 close now with no code and no validation dependency — run `/todo-finish` on each. Items 1 and 4 are code-complete and only await an empirical validation run, which is blocked on the CiMini expansion (Bracket-3 fuzzy case for item 1, a Bracket-4-reaching image for item 4). **Before any of these reach Done, a reviewer must be spawned on the `e2e1f84`+`f40beed` diff and an `**Review:** Approve` line recorded here** (P1-feature-worker gate) — the earlier review only produced a fix commit, never a recorded verdict.

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
