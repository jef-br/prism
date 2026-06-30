# Daily Brief

Scope of this pass: review all `jbtodo.md` files, `AGENT-TICKETS.md`, `jb/docs/`, and
`AGENTFEEDBACK.md`; improve open-todo answers **using only existing documented data**; do not
finalize/close anything without approval; do not invent data, change course, or edit
`AGENT-TICKETS.md`, `jb/docs/`, or `AGENTFEEDBACK.md`.

## State since last brief

Commit `21c8791` ("onnx to singleton, matching testing, Tx_* implementations") has landed since
the previous brief. Verified against the working tree:

- **ONNX session is now app-scoped (singleton).** `MatchingService` builds `_sharedClassifier`
  once and initializes the CLIP ONNX session "for the app lifetime"
  (`jb/src/core/Services/MatchingService.cs:16,27,33`); each job's `ClassificationService` wraps
  the shared classifier and no longer disposes it. The previous brief's "ONNX singleton NOT yet
  implemented" finding is now **resolved** — the M5 gate's `✅ (done 2026-06-29)` in
  `AGENT-TICKETS.md` is accurate.
- **Transform pixel work, current code state:** `Tx_CropSquare`, `Tx_ProblemImageProcessor`, and
  `Tx_util_BgStretch` are implemented; `Tx_CenterAndStretch` and `Tx_DetailCropper` still throw
  `NotSupportedException` from `Process()` (gated) — matching tickets **T-2000 / T-2100 (Blocked)**.
  `Tx_util_HeadCutter.cs` exists but is an **empty placeholder** (no content), consistent with
  **T-2200** being unstarted.

## Files reviewed

- 5 `jbtodo.md`: repo-root (`web workbench` + `pipeline fusion`), `jb/src/` (Excel parsing),
  `Images/{Classify,Generate,Transform}/`.
- `jb/docs/` (PRISM-index + PRISM-* + `ImageNGP/`), `AGENT-TICKETS.md`, `AGENTFEEDBACK.md`.

## What changed this pass

**No `jbtodo.md` answers were filled this pass.** Every currently-open `Answer:` field is one of:

1. A **user product decision** reserved by ticket **T-2300 / T-2200** (Transform: saliency-region
   crop placement, headcut confidence thresholds, greedy-crop minimum content retention; HeadCutter:
   landmark model, family threshold, cut-line style, return format). `jb/docs/PRISM-transform-generate.md`
   documents general margin / border-intersection / fill-tier policy but contains **no** accepted
   headcut-threshold or content-retention values — so filling these would mean guessing reserved
   policy. Left untouched per the team rule "unresolved product decisions stay in `jbtodo.md` — do
   not guess policy."
2. An **implementation-task spec** (the `Classify/jbtodo.md` `Analyzer_*` items). The method is
   already described inline; no additional accepted-doc data resolves them further. These are gated
   by milestones **M6–M11** and tracked under **T-2600 (Blocked)**.

So there was nothing to improve strictly from existing data without inventing or deciding.

## Proposed next steps (your call)

1. **One doc-sync inconsistency to confirm.** `AGENTFEEDBACK.md` still reads *"ONNX singleton …
   Not yet implemented; tracked in T-2600,"* but the code now implements it (above) and `AGENT-TICKETS.md`
   already marks the M5 gate done. On your OK I'll update that one `AGENTFEEDBACK.md` line to "implemented"
   (it's the only stale claim I found). *Not edited this pass — `AGENTFEEDBACK.md` is off-limits without
   approval.*

2. **Unblock the Transform stage (T-2000 / T-2100 / T-2200).** These are the critical path and are all
   blocked on the **three T-2300 product decisions** + the **HeadCutter spec**. The `jbtodo.md` entries
   already carry "Industry standard" + "Recommended solution" drafts for each — they need a yes/no/adjust
   from you, not more research. Answering T-2300 unblocks `Tx_CenterAndStretch` and `Tx_DetailCropper`
   pixel flows immediately (`Tx_util_BgStretch` fill is already available).

3. **M5 Classify groundwork (T-2600).** With the singleton done, the remaining gate items are: confirm
   `ImageNGP.json` ↔ `imagePhenotypes.md` ↔ `ImageRoles.json` agree on the 26 phenotypes, then start the
   `Analyzer_*` stubs in milestone order — **M6** (`Analyzer_HasHuman` → `Analyzer_HasFace`) is the first
   real-signal step and a prerequisite for most downstream analyzers. Replacing `RecordUnknownFeatures()`
   with real CLIP/CPU measurements is the dependency that gates phenotype production validation.

4. **Two root-level `jbtodo.md` items are unticketed** — consider whether they should become tickets:
   - **Web workbench UX refresh** (less beige, dark mode, compact match/transform review, import/export
     progress feedback, keep upscaling implicit). Self-contained, parallelizable with backend work.
   - **Pipeline architecture: fuse Import + Match** to remove double image I/O, while keeping the matching
     service publicly exposable. This is an architecture change (P4) and would interact with the
     `MatchingService` shared-classifier work above — worth a design note before any code.

5. **Excel parsing refinement (`jb/src/jbtodo.md`)** remains the largest spec'd-but-unstarted backend
   item (AUTOMAT2/HEROAUT3 header-detection failures: ES/FR/DE/NL header translation, sample-based
   FamilyID column confirmation, refco auto-generation). The user's handwritten plan invites a
   logical-consistency review **before implementing** — recommend that review be its own scoped pass when
   this is picked up, rather than editing the plan in place now.

## Nothing finalized

No checkboxes ticked, no answers closed, no data invented, no course change. Awaiting your decisions on
items 1–2 to unblock the Transform critical path.
</content>
