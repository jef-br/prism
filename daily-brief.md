# Daily Brief

Scope of this pass: review all `jbtodo.md` files, `AGENT-TICKETS.md`, `jb/docs/`, and
`AGENTFEEDBACK.md`; improve open-todo answers **using only existing documented data**; do
not finalize/close anything without approval; do not invent data, change course, or touch
`AGENT-TICKETS.md`, `jb/docs/`, or `AGENTFEEDBACK.md`.

## State since last brief

Commit `0c509ec` landed: **Tx_CropSquare + Tx_ProblemImageProcessor implemented**, and
**Match #1 (numeric scoring)** plus the Transform "define output" answers closed out. The
previous brief's "finishable now" list is therefore largely consumed — this brief reflects
what is actually left.

## Files reviewed

- 5 `jbtodo.md`: `api/`, `Images/{Transform,Match,Generate,Classify}/`. (No `IO/` or `Order/`
  `jbtodo.md` exist on disk — the `AGENTFEEDBACK.md` Open-Work table lists folders whose local
  todo files were already removed; nothing actionable there this pass.)
- `jb/docs/` (all PRISM-* docs + `ImageNGP/` + `meta/`), `AGENT-TICKETS.md`, `AGENTFEEDBACK.md`.

## What changed this pass (proposals only — nothing finalized)

Four previously-**empty** `Answer:` fields were filled by **transcribing/cross-referencing
existing accepted specs** into the todo. Each is prefixed `Answer (proposed … pending
approval)` and cites the exact source. No checkboxes ticked, no new data, no course change.

| Todo | File | Basis (existing data) |
|---|---|---|
| ONNX `InferenceSession` scope | `Classify/jbtodo.md` | `PRISM-classify.md` "ONNX Ownership" ("Sessions application-scoped") + M5 gate wording in `AGENTFEEDBACK.md` → application-scoped singleton |
| Implement `Tx_util_BgStretch` (tiered fill) | `Transform/jbtodo.md` | `PRISM-transform-generate.md` "Fill Method — Tiered by Extension Ratio" + ticket T-1700 |
| `Tx_CenterAndStretch` three-step flow | `Transform/jbtodo.md` | todo body + `PRISM-transform-generate.md` "Repositioning/Background Extension"; cross-ref to already-answered cleanup + BgStretch |
| `ImagePreProcessor` 5-step flow | `Transform/jbtodo.md` | todo body + `PRISM-classify.md` "Border Intersection Detection"; config limits already named in body |

## Proposed next steps (your call)

1. **Approve the 4 proposed answers above.** All are reconciliation of *existing* specs into
   empty answer fields — highest confidence, ready to move to implementation on your OK:
   - **ONNX singleton** is the M5 gate item. Verified against code on 26/06/26: it is **NOT
     yet implemented** — the session is still created per matching run at `MatchingService.cs:34`
     (`using IClassificationService … = ClassificationService.Create(...)`, which builds
     `new ImageClassifier()` and disposes per job). The todo's old `ShellStage_Classify` pointer
     is stale, but the per-job lifecycle is real, so the M5 gate is still open. Approving the
     proposed answer + doing the hoist closes it.
   - **`Tx_util_BgStretch`** is ticket **T-1700** (Status: Ready) — answer + ticket now agree;
     it can be picked up by a P1 worker immediately.
   - **`ImagePreProcessor` + `Tx_CenterAndStretch` flow** are the next implementation steps for
     the Transform stage now that CropSquare/ProblemImageProcessor exist.

2. **Match stage — mechanical fixes already fully specified (need your go, not more data):**
   - MatchEvidence missing 3 fields; StringMatcher Bracket-3 duplicate-phenotype guard;
     original pre-normalization token text; `Weight_MatchingSignalsConverging` AssertInRange +
     bonus placement. Each has a concrete `Fix:` in `Match/jbtodo.md`.

3. **Decisions that need *you*, not data (left untouched):**
   - Match **cross-bracket tie resolution** — scope call: spec-compliant accumulator (a) vs.
     accept V1 limitation (b).
   - Classify **`illustration-technical-drawing`** scope — recommend null-assignment (b) until a
     positive "schematic" CLIP prompt exists.
   - Classify **ONNX taxonomy reconciliation** — confirm `ImageNGP.json` ↔ `imagePhenotypes.md`
     ↔ `ImageRoles.json` agree on the 26 phenotypes.

4. **Still genuinely blocked (need new data/design — not touched):**
   - Transform: detail-crop **saliency map**, **headcut thresholds**, **greedy crop**,
     **`Tx_LowContrastEnhancement`** scope, **`Tx_util_HeadCutter`** landmark-model choice — all
     have open design questions requiring decisions, not transcription.
   - Generate: real generation backend (ComfyUI + Flux.1-schnell recommended; needs a server).
   - Classify: **`RecordUnknownFeatures()`** (blocked on taxonomy) and **phenotype production
     validation** (needs a labeled image set).

## Note on push target

This session is assigned development branch `claude/hopeful-dirac-4b7mm7`, while `CLAUDE.md`
says daily-brief commits go directly to `main`. These conflict. I committed the `jbtodo.md`
improvements **and** this brief to the assigned branch and did **not** push to `main`. Tell me
if you want the brief cherry-picked to `main` per the usual convention.
