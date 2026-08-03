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
| [T-2600](T-2600.md) | Blocked | M5 Classify groundwork | Re-measure phenotype coverage at the shipped thresholds, then close the M5 gate |
| [T-3800](T-3800.md) | Blocked | Match bracket validation | Add a fuzzy-colour image and a Bracket-4 image to CiMini, then validate both matcher changes on real data |
| [T-4000](T-4000.md) | Ready | Analyzer calibration backlog | Split the 11 analyzer calibration questions into their own tickets, one at a time |
| [T-4942](T-4942.md) | Ready | Test projects fight over the GPU | Make the two GPU-heavy test projects run one at a time, and make CI fail on a short test count |
| [T-4945](T-4945.md) | Ready | Hard-shadow threshold | Label a set for hard vs soft shadow and re-tune the threshold against it |
| [T-4948](T-4948.md) | Ready | White-on-white contrast floor | Measure real white-on-white contrast, then set the denoise strength to match |
| [T-4950](T-4950.md) | Ready | SubjectMask crosses the wire unread | Measure the per-image mask payload, then decide keep / `[JsonIgnore]` / config-gate |
| [T-4955](T-4955.md) | Ready | Stale derived edge features | Recompute `intersection-count` and `fully-in-frame` when the subject box is promoted |
| [T-4960](T-4960.md) | Ready | Alpha box vs colour fallback | Make `Analyzer_SubjectGeometry` prefer the alpha-derived box |
| [T-4980](T-4980.md) | Review | CiMini E2E golden red | Rework the fix to use the subject box honestly, then make `dotnet test` read the goldens |
| [T-4990](T-4990.md) | Ready | Detector under-counts intersections | Make the detector count every edge the subject actually touches, then re-score |
| [T-5000](T-5000.md) | Ready | Filename orientation false positives | Stop the filename analyzer reading garment nouns as camera views |
| [T-5010](T-5010.md) | Review | Centre-and-stretch unreachable | Fix the 6 stale Transform test fixtures, then verify the routing on SPACINI29 |

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
| M11 Production Validation | All 21 phenotypes | < 5% misassignment on a labeled validation set; no systematic error on any single phenotype |

## Verification Rules

- After project/solution setup: `dotnet build jb/src/PRISM.sln`, API run smoke, web `npm run typecheck` + `npm run build`.
- After each milestone: run relevant smoke and record result in milestone table.
- After todo/doc sync: `rg --files -g 'jbtodo.md' jb/src` and `rg -n "^- \[ \]" jb/src`.
- Before advancing milestones: `git status --short` to confirm edits stayed inside assigned ownership.
