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
**Blocked-by:** M5 milestone gate — all Classify `jbtodo.md` decisions must be answered first.

Tracks the five open items in `jb/src/core/Services/Matching/Classify/jbtodo.md`:
1. Gate phenotypes (bypass flag — stays open until phenotypes validated).
2. Confirm ImageNGP taxonomy: `ImageNGP.json` ↔ `imagePhenotypes.md` ↔ `ImageRoles.json` agree on 26 phenotypes and their IF combinations.
3. Resolve `illustration-technical-drawing` scope (option (b) = null/no-phenotype recommended).
4. Replace `RecordUnknownFeatures()` stub with real CLIP measurements (after taxonomy + prompts are settled).
5. Phenotype production validation: labeled set, confusion matrix, <5% misassignment rate across 26 phenotypes.
6. Per-feature CLIP confidence calibration (carried over from the 2026-07-04 input_ids fix): influential-tag bars are per feature in `Prism_Config.json` `Classification.Confidence_Thresholds`, but orientation/head prompts rarely clear their bar on real batches — 2026-07-10 full-pipeline test saw 2 of 25 images get a phenotype because `hero-orientation` stayed UNKNOWN everywhere. Calibrate (e.g. per-feature softmax over the prompt group) before item 5's validation makes sense.

M5 gate condition: all Classify decisions answered; ONNX session migrated to singleton.

**Files:** `jb/src/core/Services/Matching/Classify/jbtodo.md`, `jb/src/core/Services/Matching/Classify/ImageFeatureAnalyzer.cs`

---





### T-3800 · Match bracket todos: edit-distance gap, substring-rescue perf, totalImageTokens precision
**Status:** Ready | **Profile:** P1-feature-worker
**Tracks:** `jb/src/core/Services/Matching/Match/jbtodo.md` (triaged 2026-07-11).

Tracks three open items, each fully detailed (impact, industry-standard framing, recommended solution) in the source `jbtodo.md`:
1. **StringMatcher edit-distance gap** — `jb/docs/PRISM-match.md` documents typo-tolerant string matching; `StringMatcher.cs` only does exact token matching via an inverted index. Decide whether the doc or the code is wrong; if the code should gain tolerance, reuse the Levenshtein helper already in `jb/src/core/lib/Excel/ModelBuilder.cs` (bounded distance ≤ 1, categorical columns only).
2. **`TryMatchBySubstringRescue` perf** — brute-force substring scan over the digit index, `O(unmatched images × rescue tokens × index size)`, unmeasured. Add a `Stopwatch` measurement against a representative/CiMini batch before deciding whether an n-gram index is warranted.
3. **`SemanticMatcher.totalImageTokens` precision** — mixes matched-token count with candidate-family count, so `stringSignal` can drift for reasons unrelated to the filename itself. Replace with a real filename-token count (reuse `StringMatcher`'s tokenizer); verify accept/reject decisions don't shift on CiMini before rollout.

**What to do:** Pick off each item independently per its `jbtodo.md` recommended solution; each needs its own decision/measurement step before code changes, not a blind implementation.

**Acceptance:**
- Each of the 3 items has either a code change + passing tests, or a documented "measured, not worth it" close with no code change.
- `jb/src/core/Services/Matching/Match/jbtodo.md` items closed and moved to `jb/docs/` per the todo lifecycle.

**Files:** `jb/src/core/Services/Matching/Match/jbtodo.md`, `jb/src/core/Services/Matching/Match/StringMatcher.cs`, `jb/src/core/Services/Matching/Match/NumericMatcher.cs`, `jb/src/core/Services/Matching/Match/SemanticMatcher.cs`, `jb/src/core/lib/Excel/ModelBuilder.cs`, `jb/docs/PRISM-match.md`.

---


### T-4000 · Per-feature Analyzer TOC: calibration + stub implementation backlog
**Status:** Ready | **Profile:** P0-orchestrator
**Tracks:** `jb/src/core/Services/Matching/Analyzers/jbtodo.md` (triaged 2026-07-11) — a TOC of ~27 items across 3 sections, none previously represented on the ticket board.

**Problem:** `Analyzers/jbtodo.md` is a checklist pointing at per-analyzer working docs, split into:
1. **Implemented, calibration open (11)** — `Analyzer_ProductType`, `Analyzer_FilenameEvidence`, `Analyzer_HasHuman`, `Analyzer_SubjectGeometry`, `Analyzer_DominantColors`, `Analyzer_ProductColor`, `Analyzer_BackgroundColor`, `Analyzer_Exposure`, `Analyzer_MultipleProducts`, `Analyzer_Interior`, `Analyzer_IsIllustration` — each has a named open calibration/validation question in its own `.md`.
2. **Stubs, implementation open (10)** — `Analyzer_FacePose` (highest value: 6 features, unblocks most on-model phenotypes), `Analyzer_TextPresent`, `Analyzer_Mannequin`, `Analyzer_LogoPresent`, `Analyzer_CameraAngle`, `Analyzer_IndoorOutdoor`, `Analyzer_ShadowReflection`, `Analyzer_Packaging`, `Analyzer_MaterialTexture`, `Analyzer_LightingDetail`.
3. **Cross-cutting (5)** — retire `ImageOrderer.ResolveProductType`'s value-sniffing fallback once `Analyzer_ProductType` is validated; unify `ProductTypeMap.json`/`TranslationDictionary.json` vocabulary; segmentation-model milestone for true coverage-ratio masks; `Analyzer_Symmetry` stays dropped unless an orientation rule wants it; standardize CLIP-vs-analyzer write precedence.
4. **OPEN (1)** — centralize per-analyzer `*Config.cs` files into a single `AnalyzerConfig.cs` with nested objects.

**This ticket is an index, not a single unit of work.** Individual items are gated by the Milestone Gates table (M6 Human & Model Detection through M10 Semantic & Content each name the specific analyzers they depend on); pick items in milestone order, starting with `Analyzer_FacePose` (blocks the most downstream phenotypes) and the config-centralization item (independent of any milestone, can start anytime).

**What to do:** Orchestrator splits this into per-analyzer or per-milestone-batch follow-up tickets as work is picked up, rather than one agent attempting all 27 items at once.

**Acceptance:** Each analyzer's `.md` open question is answered and its `jbtodo.md` checkbox checked, in milestone order; `jb/src/core/Services/Matching/Analyzers/jbtodo.md` reflects real remaining state at all times (not batch-updated at the end).

**Files:** `jb/src/core/Services/Matching/Analyzers/jbtodo.md`, `jb/src/core/Services/Matching/Analyzers/*.md`, `jb/src/core/Services/Matching/Analyzers/*.cs`.

---

## Verification Rules

- After project/solution setup: `dotnet build jb/src/PRISM.sln`, API run smoke, web `npm run typecheck` + `npm run build`.
- After each milestone: run relevant smoke and record result in milestone table.
- After todo/doc sync: `rg --files -g 'jbtodo.md' jb/src` and `rg -n "^- \[ \]" jb/src`.
- Before advancing milestones: `git status --short` to confirm edits stayed inside assigned ownership.
