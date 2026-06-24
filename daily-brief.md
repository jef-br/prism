# Daily Brief

Scope of this pass: review all `jbtodo.md` files, `AGENT-TICKETS.md`, `jb/docs/`, and
`AGENTFEEDBACK.md`; improve open-todo answers **using only existing documented data**;
do not finalize/close anything without approval; do not touch `AGENT-TICKETS.md`,
`jb/docs/` or `AGENTFEEDBACK.md`.

## Files reviewed

- 7 `jbtodo.md` files: `jb/src/api/`, `jb/src/core/IO/`, and `jb/src/core/Images/{Transform,Match,Generate,Classify,Order}/`.
- `jb/docs/` (all 18 PRISM-* docs + `ImageNGP/` + `meta/`).
- `AGENT-TICKETS.md` and `AGENTFEEDBACK.md` — now present on `main` as of `63ec17a`.

## What changed (all proposals, nothing finalized)

Every edit is a proposed answer written into the existing `Answer:` field, prefixed
`Answer (proposed … pending approval)`, citing the exact doc section it was synthesized from.
**No checkboxes were ticked; no new data invented; no project course changed.**

| Todo | File | Basis (existing doc) |
|---|---|---|
| Transform-facing IF/INGP output | `Transform/jbtodo.md` | PRISM-transform-generate "Transform-Facing Classification Tags" + UNKNOWN routing; PRISM-classify thresholds |
| Transform failure/fallback/fill-KO policy | `Transform/jbtodo.md` | PRISM-transform-generate UNKNOWN→Problem + Border Intersection; PRISM-models ITR; KO-vs-Failed |
| Crop decision output | `Transform/jbtodo.md` | PRISM-models ITR; PRISM-transform-generate Salient Bounds + Border Intersection |
| Background fill policy | `Transform/jbtodo.md` | PRISM-transform-generate Background Extension/Identification + "External SaaS NOT Permitted" |
| Resize decision output | `Transform/jbtodo.md` | PRISM-models ITR resize fields |
| Border-intersecting detail-crop result | `Transform/jbtodo.md` | PRISM-transform-generate No-Reposition rule; cross-linked to failure/KO policy |
| `ghost-front` ordering bug | `Classify/jbtodo.md` | PRISM-classify first-match-wins rule engine — deterministic bug fix |
| Final ImageNGP taxonomy (pointer) | `Classify/jbtodo.md` | PRISM-classify Taxonomy config; existing `ImageNGP.json`/`ImageRoles.json`/`imagePhenotypes.md` |
| `illustration-technical-drawing` scope | `Classify/jbtodo.md` | PRISM-classify UNKNOWN States; no positive "schematic" CLIP prompt exists yet → recommend (b) |
| SD-13 JSON `images` journey shape | `api/jbtodo.md` | PRISM-api JSON Output per-journey-item shape; PRISM-models IRL/IRO |
| Cross-bracket tie resolution (context) | `Match/jbtodo.md` | PRISM-match Waterfall tie-break line; flagged spec-compliant option (a) |

## Proposed next steps (your call)

1. **Approve / adjust the proposed answers above.** Highest-confidence, ready-to-finalize:
   - `ghost-front` ordering bug (pure correctness fix, no new data).
   - SD-13 JSON journey shape (realigns impl to the existing PRISM-api contract).
   - The 5 Transform "define output" answers (all map directly to existing ITR / transform-facing-tag docs).
   These can move to implementation immediately on your OK.

2. **Decisions that need *you*, not more data:**
   - **Match — cross-bracket tie resolution:** spec-compliant (a) vs accept-as-V1-limitation (b). Scope call.
   - **Classify — `illustration-technical-drawing`:** recommend (b) null-assignment until a proven CLIP "schematic" prompt exists; confirm.
   - **Classify — `interior-shot` unreachable** (`packaging-visible` always UNKNOWN in CPU-only): add a prompt/analyzer or relax the rule.
   - **Order — det0 SIDE fallback:** existing answer truncated mid-sentence ("DET-ORDER-GAPS-ALLOWED…") — please finish or confirm the intended param semantics.

3. **Blocked on new data / external choices:**
   - **Generate — image-generation backend:** ComfyUI + Flux.1-schnell recommended; needs hardware commitment.
   - **Classify — `RecordUnknownFeatures()` stub (35+ IFs UNKNOWN):** needs CLIP prompts/detectors wired in.
   - **Classify — phenotype production validation:** needs labeled image set + threshold tuning.
   - **Transform — quality-threshold numbers and resize limits:** new CFG values required.
   - **Transform — detail-crop micro-decisions** (saliency, headcut thresholds, greedy retention, cleanup): gated behind classifier features.
   - **IO — `Fetch_WeTransfer`:** feasibility Low; no V1 action.
