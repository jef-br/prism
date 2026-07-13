# jbtodo — Order

## det numbering for overflow images (T-2830)

**Question:** CLAUDE.md's domain vocabulary says `_det#` is zero-based per family ("the first image in
any family's det order should be `_det0`"). The implementation gives overflow images (no qualifying
phenotype) `lastConfiguredSlot + 1` and up — with det0–det7 configured for every product type, a family
whose images all fail phenotype qualification starts at `_det8`, and `_det0`–`_det7` are never used.
Which is correct?

- **(a) Compact per family:** after assignment, renumber each family's images to consecutive det0..detN
  (preserving relative order). Output filenames always start at `_det0`; the slot semantics ("det1 = back")
  are lost in the filename but retained in OrderEvidence.
- **(b) Keep semantic slots, fix the docs:** `_det#` encodes the *configured slot* (det0 = front, det1 =
  back, …) and overflow intentionally starts after the configured range; CLAUDE.md and
  `jb/docs/PRISM-order-rename.md` get corrected to describe slot semantics instead of zero-based numbering.

**Context:** As of 2026-07-03 overflow images are ordered by filename keyword hint → natural filename
order (deterministic), but numbering still starts at det8. Once B2 threshold calibration makes phenotypes
fire, real slot assignments (det0–det7) will appear and mixed families will have both semantic slots and
overflow slots — the decision affects how consumers interpret the suffix.

**Answer:** (a) Compact per family. Implemented as the `DET-ORDER-GAPS-ALLOWED` gap policy (default
`false` = compact) already designed in `jb/docs/PRISM-order-rename.md`. Export renumbers each family to
contiguous det0..detN, preserving relative order (never reorders); the Order stage is untouched.
Code: `ImageOrderer.CompactDetOrder` called from `Exporter.Run` (and the MatchLite / MatchOnly paths in
`PrismService`), gated by `PrismConfiguration.DetOrderGapsAllowed`.

Note: this fixes the *numbering* (families now start at det0 instead of det8). It does **not** make real
semantic det0–det7 slots appear — that still depends on phenotypes firing. Today every image overflows
because per-feature analyzers return `UNKNOWN`, so `PhenotypeRuleSet.Assign` returns null. The
phenotype-gating fix ("BypassPhenotypes flip" / real analyzers) is tracked in
`jb/src/core/Images/Classify/jbtodo.md`. Until then, compaction yields det0-based numbering over the
overflow (filename-hint → natural-filename) order.
