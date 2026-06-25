# Match Stage Open Decisions

## Spec deviations

-------




-------
- [ ] `Weight_MatchingSignalsConverging` convergence bonus not yet consumed.
  - Files: `jb/src/core/PrismConfiguration.cs`, `jb/src/core/Prism_Config.json` (value 0.25).
  - Status: Range validation (`AssertInRange` in `Validate()`) is done. The weight is now validated on startup but still never read by any matcher or scorer.
  - Intent: A score bonus applied when multiple matching signals agree — the "convergence" case where NumToken + String + Classification all point to the same FamilyID.
  - Fix: Implement the convergence bonus in the matcher waterfall — decide where in the scoring pipeline the bonus applies and which signal combinations qualify as "converging".

## User decisions required

-------
- [ ] Cross-bracket tie resolution requires user decision.
  - Issue: PRISM-match.md says "KO the image unless it can sit at the exact same det order position in every matching FamilyID" when an image remains a candidate for multiple FamilyIDs after all brackets. The current implementation silently passes ties to the next bracket with no cross-bracket candidacy accumulator. An image that ties across all brackets is KO'd by `KoUnmatched` with reason `MATCH_NOT_FOUND`, not `MATCH_TIE`.
  - Block: Implementing the spec behavior requires a candidacy accumulator that collects all tied FamilyIDs across Brackets 1–3 and defers the tie-break to a det-position comparison step. This is moderate complexity and requires an explicit decision on whether the current pass-through-on-tie behavior is accepted for V1 or must be replaced.
  - Options: (a) Implement the accumulator and det-position tie-break per spec. (b) Accept the current behavior and document it as a known V1 limitation. Either decision must be recorded here before a developer starts work.
  - Answer (context from existing docs, decision still yours — pending approval):
    The spec already defines the *target* behavior: PRISM-match.md "Waterfall Matching Gates" → "Image remains candidate for multiple FIDs → KO unless it can sit at the exact same `_det` position in every matching FID." Option (a) is the spec-compliant implementation of that line; option (b) is an accepted deviation. Existing data does not, by itself, decide whether V1 must ship spec-compliant or may defer — that is a scope call only you can make. Two things to note if (a) is chosen: it needs a cross-bracket candidacy accumulator (current code passes ties forward bracket-by-bracket), and the KO reason should become `MATCH_TIE` rather than the current `MATCH_NOT_FOUND` so the manifest distinguishes ties from genuine no-matches. No new data required for either option — only your scope decision.
