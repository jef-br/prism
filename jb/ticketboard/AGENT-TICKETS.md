# PRISM Agent Tickets

This file is the table of contents. Every open ticket gets one row: its ID, its status, its title, and
the one thing to do next. Nothing else lives here.

**Full detail is in `jb/ticketboard/T-XXXX.md`, one file per ticket.** Read the row, then open only the
ticket file you need. Do not paste ticket bodies back into this file.

Main thread is the orchestrator: owns ticket status, final integration, conflict resolution, and
user-facing summaries.

## Board

| Ticket | Status | Title | Do this next |
|---|---|---|---|
| [T-2600](T-2600.md) | Blocked | M5 Classify groundwork | Land T-5070 and T-5080, then re-score CiMini's 99 labelled rows |
| [T-2840](T-2840.md) | Ready | CLIP batch-composition sensitivity, confirmed (T-2820 recurrence) | Decide the closing criteria — all 4 original families now explained by other tickets or reduced to ordering noise |
| [T-3800](T-3800.md) | Blocked | Match bracket validation | Author a Bracket-4 image and a reference-free fuzzy-colour image; no dataset has either |
| [T-4000](T-4000.md) | Ready | Analyzer calibration backlog | Split the 11 analyzer calibration questions into their own tickets, one at a time |
| [T-4942](T-4942.md) | Ready | Test projects fight over the GPU | Fix the K&R formatting violation in `ModelBuilder.cs` blocking CI, then get a real CI run to pass the floor gate |
| [T-4945](T-4945.md) | Ready | Hard-shadow threshold | Label a set for hard vs soft shadow and re-tune the threshold against it |
| [T-4948](T-4948.md) | Ready | White-on-white contrast floor | Measure real white-on-white contrast, then set the denoise strength to match |
| [T-4950](T-4950.md) | Ready | SubjectMask crosses the wire unread | Measure the per-image mask payload, then decide keep / `[JsonIgnore]` / config-gate |
| [T-4980](T-4980.md) | Blocked | CiMini E2E golden red | [[T-5060]] was reverted, not landed — golden now red on 93 fields (re-measured 2026-08-11); fix belongs to [[T-5120]] |
| [T-5010](T-5010.md) | Blocked | Centre-and-stretch unreachable | Get every SPACINI29 row in `spacini29-image-routing-list.md` a user-blessed intended route |
| [T-5050](T-5050.md) | Ready | `multiple-products` never written false | Make the analyzer write a known value, handling the shoe-pair case |
| [T-5070](T-5070.md) | Ready | Edge-touching shots match no rule | Decide what `intersection-count = 0` should mean; it blocks 7 of 18 phenotypes |
| [T-5080](T-5080.md) | Ready | `hero-orientation` wrong or absent | Dump the per-prompt score vector for 9 known side views, then re-word or re-bar |
| [T-5090](T-5090.md) | Ready | SubstringRescue invents evidence from shot numbers | Fix is correct; still re-check the other 4 pre-existing SubstringRescue matches the ticket's acceptance requires, then close |
| [T-5120](T-5120.md) | Blocked | Filename and folder tokens should feed phenotyping | Resolve the `pack`/`packshot` keyword collision first; start only on a clean, green, roomy session |
| [T-5130](T-5130.md) | Ready | Excel column-fill-rate gate drops sparse description columns | Decide how a sparse-but-real filename-reference column should be treated, then implement |
| [T-5200](T-5200.md) | Ready | SiblingPropagator loose-relation scan is unindexed O(n²) | Add a test covering `crossBracketCandidates` filtering (currently untested), then close |
| [T-5210](T-5210.md) | Ready | SiblingPropagator may reinvent token evidence Brackets 1-3 built | Read both token shapes and decide whether BuildProfile can reuse existing MatchEvidence |
| [T-6910](T-6910.md) | Review | Full-resolution pixel analysis runs twice, second pass single-threaded | Review the parallel-Refine fix; decide whether scaled decode (direction 2) is worth re-blessing the golden |
| [T-6920](T-6920.md) | Ready | Unique-article match should refuse a colour code the family's Excel data doesn't have | Start a `/pair` session on the per-family colour-code membership check in `NumericMatcher` |

Done tickets live in [`AGENT-TICKETS-ARCHIVE.md`](AGENT-TICKETS-ARCHIVE.md). Read it only when you need
history.

## How to write the "Do this next" line

One sentence. Start with a verb. Say the action, not the problem — the problem is already in the ticket
file. Keep it under about 15 words. Plain English, no jargon a newcomer would have to look up.

**Good:** *Make the detector count every edge the subject actually touches, then re-score.*
Someone can start work from that sentence alone.

**Bad:** *Detector under-counts intersections on 1 image in 4 (76% accurate, all errors under-count).*
That is a status report. It says what is wrong and nothing about what to do.

When a ticket is in Review, the next action is usually the verdict plus whatever verification is still
outstanding — say both.

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

- `P1-feature-worker` / `P4-critical-architecture` tickets: spawn the reviewer agent on the completed diff and record its verdict as `**Review:** Approve|Request Changes (YYYY-MM-DD)` in the ticket file. Only `Approve` makes a ticket eligible for `Done` — /ticket-finish enforces this.
- `P0`/`P2`/`P3` tickets: orchestrator judgment suffices → mark `Done`.
- Incomplete but salvageable → correction to same agent or follow-up ticket.
- Missing product intent → ask user, then unblock agent.
- Milestone gates are authoritative: later tickets stay blocked until the gate passes.

## Ticket Format

Each open ticket is one file, `jb/ticketboard/T-XXXX.md`, opening with `### T-XXXX · Title` and then a
`**Status:** ... | **Profile:** ...` line. Keeping the `###` heading means /ticket-finish can append the
block straight into the archive unchanged.

Status: Ready, Blocked, Active, Review, Done. Agent type: `explorer`, `worker`, or orchestrator.
P1/P4 tickets carry a `**Review:** <verdict> (YYYY-MM-DD)` line once reviewed; `Approve` is required before Done.

On Done, /ticket-finish appends the ticket body to `AGENT-TICKETS-ARCHIVE.md`, deletes `T-XXXX.md`, and
removes the row from the board above. Status and the "Do this next" line are the only ticket state that
lives in this file — everything else stays in the ticket file.

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
| M5 Classification Groundwork | ImageNGP taxonomy + rule correctness | Taxonomy trimmed to real/reachable-only ✅ ([[T-4700]]); ONNX singleton ✅ (2026-06-29); phenotype validation open → [[T-2600]] |
| M6 Human & Model Detection | **Superseded** — `hero-is-human`, `has-human`, `head-visible` real; `contains-mannequin`, `face-visible` removed ([[T-4700]]) | Re-defined only if the removed features return — see `Analyzers/jbtodo.md` "Removed" section |
| M7 Orientation & Pose | **Superseded** — `hero-orientation` real; `pose-type`, `camera-angle`, `top-view` removed ([[T-4700]]) | Re-defined only if the removed features return |
| M8 Product & Packaging | **Superseded** — `product-type-label`, `multiple-products` real; `packaging-visible` removed ([[T-4700]]) | Re-defined only if `packaging-visible` returns |
| M9 Composition & Spatial | `product-coverage-ratio`, `image-occupancy`, `salient-bbox`, `vertical-centering`, `horizontal-centering` | Composition phenotypes measured; overflow slot assignment accuracy confirmed |
| M10 Semantic & Content | **Superseded** — `dominant-colors` real (no phenotype rule consumes it yet); `text-present`, `logo-present`, `lighting` removed ([[T-4700]]) | Re-defined only if the removed features return |
| M11 Production Validation | All 18 phenotypes | < 5% misassignment on a labeled validation set; no systematic error on any single phenotype. **Measured 2026-08-05 on `test/datasets/CiMini/expected-phenotype.json` (99 rows, the labeled set; measured as JBComplete before the 2026-08-06 CiGolden/JBComplete → CiMini merge): 30.3% misassignment, 39.4% coverage, `front-packshot` recall 0/25.** SPACINI29's 4.7% is not a pass — it only exercises 2 of the 18. Blocked on [[T-5070]] + [[T-5080]] |

## Verification Rules

- After project/solution setup: `dotnet build jb/src/PRISM.sln`, API run smoke, web `npm run typecheck` + `npm run build`.
- After each milestone: run relevant smoke and record result in milestone table.
- After todo/doc sync: `rg --files -g 'jbtodo.md' jb/src` and `rg -n "^- \[ \]" jb/src`.
- Before advancing milestones: `git status --short` to confirm edits stayed inside assigned ownership.
