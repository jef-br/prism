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

## DetermineTieBreaker's full rescan can mislabel which tiebreaker actually decided the winner

- [ ] ImageOrderer.DetermineTieBreaker rescan: after a winning image is
  assigned to a det slot, `DetermineTieBreaker` scans the *entire* candidate
  list for the family to find "competitors" (same slot, same phenotype
  rank), and reports the first tiebreaker level where *any* competitor
  differs from the winner. With 3+ competitors that lose for different
  reasons, this can name the wrong tiebreaker as the deciding one.
  - Is the rescan itself needed? Yes — some form of it is, because "who else
    was competing for this exact slot" isn't known ahead of time without
    looking. But it does not need to scan the *full* list of every candidate
    for every winner: the candidate list is already sorted by slot then
    phenotype rank first (`CompareCandidates`), so all candidates sharing a
    slot+phenotype-rank sit next to each other in one contiguous block
    already. The competitors for any winner could be found by grouping the
    sorted list into these blocks once per family, instead of rescanning the
    whole family's candidate list from scratch for every single winning
    assignment.
  - Performance impact: low. Families are small (most well under 20 images),
    so the rescan cost itself is not a real slowdown — this is a correctness
    issue in the labeling logic, not a performance problem.
  - Example where it's correct: winner has NgpConfidence=5. One other image
    also wanted this slot+phenotype with NgpConfidence=3. The function
    reports "ngp-confidence" — correct, that really is what decided it.
  - Counter-example where it's likely wrong: winner has NgpConfidence=5,
    HintScore=1. Three other images also wanted this same slot+phenotype:
    Image B has NgpConfidence=2 (clearly lower — never actually threatened
    the winner); Image C has NgpConfidence=5 (tied with the winner) and
    HintScore=0 (this is the *real* closest competitor — the winner only
    beat C because of the filename hint). The function checks "does *any*
    competitor have a different NgpConfidence than the winner?" — yes,
    Image B does — so it immediately reports "ngp-confidence." But that's
    misleading: Image B was never close (it lost purely on confidence), and
    the real deciding factor against the true closest competitor, Image C,
    was the filename hint, not confidence.
- Impact:
  - Low - the actual `DetOrder` assigned to each image never changes; only
    the `OrderEvidence.TieBreakerWon` diagnostic text can be wrong, which
    affects manifest readability/debugging, not output correctness.
  - Effect on other TODOs: none.
- Industry standard:
  When explaining why option A beat option B in a sorted ranking, compare A
  only against its immediate runner-up (the very next-best option), not
  against every other option in the list — comparing against a
  much-worse option can name the wrong reason for a close win.
- Recommended solution:
  Change `DetermineTieBreaker` to compare the winner only against the
  immediate runner-up within its sorted slot+phenotype-rank block (the next
  candidate in that block that wasn't already claimed by an earlier
  assignment elsewhere), not against "any" competitor in the full list. This
  fixes the mislabeling, and as a side effect also replaces the full-list
  rescan with a lookup into a one-time grouping of the already-sorted
  candidate list — a free performance improvement, though the correctness
  fix is the actual goal.
- Answer:
