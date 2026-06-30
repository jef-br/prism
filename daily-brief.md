# Daily Brief

Scope of this pass: review all `jbtodo.md` files, `AGENT-TICKETS.md`, `jb/docs/`, and
`AGENTFEEDBACK.md`; improve open-todo answers **using only existing data**; do not finalize/close
anything without approval; do not invent data, change course, or edit `AGENT-TICKETS.md`,
`jb/docs/`, or `AGENTFEEDBACK.md`.

## State since last brief

- **Matching work landed** (commit `354c3e0`: "multilingual Excel detection,
  FilenameToCellMatcher, bracket-3 index, fast-KO"). As a result `jb/src/jbtodo.md` is no longer
  the Excel-parsing spec the prior brief described — it now holds the per-job run-expectation
  tracker (AUTOMAT2/HEROAUT2/INPUTMA25 → fast KO on missing FamilyID; the rest → high/100% OK),
  dated Pre-30/06 and 01/07. No open `Answer:` field to improve there.
- ONNX singleton remains implemented (`MatchingService.cs:16,27,33`); M5 gate `✅ (done
  2026-06-29)` in `AGENT-TICKETS.md` is accurate.

## What changed this pass

**One open todo improved from existing code — `jb/src/core/IO/Import/jbtodo.md` (fast-path
conforming images).** Its *primary open question* was an explicit instruction to "confirm the
normalized-artifact contract before choosing (a)/(b)" — does any downstream stage read-modify-write
the normalized file? That is a verifiable architectural fact, not a reserved product decision, so I
answered it from the code and left the (a)/(b) choice open:

- **Verified read-only at every consumer.** Transform passes `NormalizedJpgPath` into
  `ImagePreProcessor.Preprocess` as a read-only input (`TransformService.cs:55`); the preprocessor
  `Image.Load`s it (`ImagePreProcessor.cs:125`) and encodes the result into a `MemoryStream`
  (`:134`) — output lives only in `lambda.ProcessedBytes`, never written back to the path. Export
  prefers `ProcessedBytes` and reads the file only as an OK fallback / for KO images
  (`Exporter.cs:90,93-95,107-110`). Match loads it read-only (`MatchingService.cs:199`). No
  `File.Write/Copy/Move` or `Save(<path>)` in core targets `NormalizedJpgPath`.
- **Implication recorded (decision left to you):** the safety blocker the todo names —
  "downstream read-modify-write would corrupt the user's original under option (b)" — **does not
  exist in current code**, so it does not by itself force option (a). Two residual considerations
  remain (both from existing code): Export serves `NormalizedJpgPath` directly into the ZIP for KO
  images and the OK fallback; and the path today always lives inside `jobTempFolder/normalized/`
  (`Importer.cs:296-326`), whereas (b) would point it outside the job temp folder at a source whose
  lifetime is owned elsewhere.

Committed on branch `claude/hopeful-dirac-hdti3d` (not finalized — checkbox left unchecked).

## Everything else: reviewed, nothing to fill from data alone

- **`Classify/jbtodo.md`** — the `Analyzer_*` items are implementation specs with the method
  already described inline; no accepted-doc data resolves them further. Gated by M6–M11, tracked
  under **T-2600 (Blocked)**. Taxonomy + production-validation items are explicitly **FROZEN**.
- **`Transform/jbtodo.md`** — open items are reserved product decisions (HeadCutter Algorithm A
  anatomical constants; HeadCutter landmark model / family threshold / cut-line style / return
  format) or blocked implementation tasks (`Tx_DetailCropper`, gated behind unanswered
  prerequisites + missing classifier features). Guessing these would violate "unresolved product
  decisions stay in `jbtodo.md` — do not guess policy."
- **`Generate/jbtodo.md`** — **FROZEN** ("out of scope while Classify is active").
- **Repo-root `jbtodo.md`** — web-workbench refinement + Import/Match fusion are product/direction
  items with no `Answer:` field; out of "improve from existing data" scope.

## Proposed next steps (your call)

1. **Decide the Import fast-path (a)/(b).** The blocking contract question is now answered — the
   only thing left is your product call: copy a conforming source into `normalized/` (a,
   safe/simple) or reference it in place (b, zero-copy but needs the two residual checks above).
   This unblocks the T-3000 follow-up.
2. **Doc-sync nit (carried from prior pass):** `AGENTFEEDBACK.md` "Behavioral Memory" still reads
   *"ONNX singleton … Not yet implemented; tracked in T-2600,"* but the singleton landed and the M5
   gate is marked done. Worth a one-line correction — left untouched here because `AGENTFEEDBACK.md`
   is off-limits without your approval.
3. **Unblock the Transform critical path (T-2000 / T-2100 / T-2200).** All three are blocked only on
   the **three T-2300 product decisions** + the **HeadCutter spec** — they need a yes/no/adjust from
   you, not more research. Answering them unblocks `Tx_CenterAndStretch` and `Tx_DetailCropper`
   (fill via `Tx_util_BgStretch` is already available).
4. **Sequence the two Ready perf tickets** — **T-3000** (parallelize import) then **T-3100**
   (bracket-4 skip/index). Both are unblocked and were flagged as the MMERO26 (4048-img)
   bottlenecks; T-3100 also notes the import fast-path should layer on top of T-3000.

## Nothing finalized

One todo answer improved with verified code findings (checkbox left unchecked); no answers closed,
no data invented, no course change, and no edits to `AGENT-TICKETS.md`, `jb/docs/`, or
`AGENTFEEDBACK.md`.
