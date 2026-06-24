# Match Stage Open Decisions

## Spec deviations

-------
- [ ] Numeric scoring formula does not match PRISM-match.md.
  - File: `jb/src/core/Images/Match/NumericMatcher.cs`.
  - Spec says: Multi-component deduction model — start at 100%, then deduct (a) 5% × (token_count − 1), (b) edit_distance / string_length ratio, (c) length_difference ratio. The result is a confidence in [0, 1].
  ****
- Current behavior: Bracket 1 returns a fixed `FinalScore = 1.0` for all single-token exact matches. Bracket 2 uses `TokenizedConcatenationDistance` converted to confidence, which captures token-count cost but not the edit-distance or length-difference components.
  
  - Why it deviates: The TCD formula was implemented for header-matching (Excel column matching) and repurposed for Bracket 2. It was not designed to match the spec's three-component deduction model. Bracket 1 was given a fixed score as a placeholder.
  
  - Fix: Implement the three-component formula for both brackets. For Bracket 1, the edit distance component is always 0 (exact match), so only the token-count penalty (5% × 0 = 0%) and length-difference component apply. For Bracket 2, compute all three components against the best-matching concatenation.




-------
- [ ] MatchEvidence is missing three fields required by PRISM-models.md.
  - File: `jb/src/core/Images/Match/MatchEvidence.cs`.
  - Spec says: PRISM-models.md defines three fields that must appear on every MatchEvidence record: (1) `ThresholdStatus` — whether `FinalScore` exceeded the configured match threshold; (2) `RejectedNearTieEvidence` — bounded list of near-tie candidates from Brackets 1–2 that were passed over; (3) per-matcher weights and confidence scores for each matcher that contributed evidence, not just `AcceptedMatcherName`.
  
- Current behavior: `ThresholdStatus` is absent. `RejectedNearTieEvidence` is absent. The only matcher attribution stored is `AcceptedMatcherName` (a single string).
  - Why it deviates: The three fields were identified as required after the initial MatchEvidence record was designed and populated. No ticket was created to add them.

  - Fix: Add the three fields to `MatchEvidence`. Populate `ThresholdStatus` in the waterfall after the score is computed. Populate `RejectedNearTieEvidence` at the pass-through points in `RunBracket1` and `RunBracket2`. Add a `MatcherWeights` collection to replace the single-string `AcceptedMatcherName`.

-------
- [ ] StringMatcher Bracket 3 missing "no duplicate image type in same FamilyID" guard.
  - File: `jb/src/core/Images/Match/StringMatcher.cs` lines 41–50.
  - Spec says: PRISM-match.md specifies Bracket 3 accepts an assignment only when (a) the image matches exactly one FamilyID AND (b) that FamilyID does not already have a non-KO candidate image of the same image type (SelectedPhenotype).
  
- Current behavior: Condition (a) is implemented (`candidates.Count != 1` → null). Condition (b) is not implemented. A string-bracket match is accepted even when the target FamilyID already has an image of the same phenotype, which can produce duplicate image-type assignments in the same family.
  - Why it deviates: Condition (b) was not identified until after T-600 was completed.

  - Fix: Before accepting a Bracket 3 match, check `context.LambdaRecords` for any non-KO record already assigned to the same FamilyID with the same `SelectedPhenotype`. Requires passing the current lambda list into `StringMatcher.TryMatch` or performing the check in `ImageMatcher.RunBracket3` before committing the assignment.

-------
- [ ] Original pre-normalization token text not preserved in StringTokenEvidence.
  - File: `jb/src/core/Images/Match/StringMatcher.cs` lines 100–108.
  - Spec says: PRISM-match.md requires the original (pre-normalization) filename token text to be preserved in evidence records for diagnostics and workbench display.
  
- Current behavior: `TokenEvidenceItem.FilenameToken` stores the normalized form of the filename token (lowercase, diacritics stripped). The original text is not retained.
  - Why it deviates: `ExtractImageTokens` normalizes tokens before returning them, and the normalized form is what gets stored in evidence. The original text is discarded after normalization.

  - Fix: Pass both the original and normalized token through `ExtractImageTokens` (return tuples or a wrapper type), and store the original text in `TokenEvidenceItem.FilenameToken` while using the normalized form for comparison only.


-------
- [ ] `Weight_MatchingSignalsConverging` is parsed from config but never consumed, and never range-validated.
  - Files: `jb/src/core/PrismConfiguration.cs` (property line 41, parsed line 139, `Validate()` lines 190–193), `jb/src/core/Prism_Config.json` (value 0.25).
  - Intent: A score bonus applied when multiple matching signals agree — the "convergence" case where NumToken + String + Classification all point to the same FamilyID. Config and property exist but no matcher or scorer reads it yet.

  - Fix: (1) Add `AssertInRange(Weight_MatchingSignalsConverging, 0.0, 1.0, cfgPath, "Classification.Weights.CONVERGENCE_WEIGHT")` after the four existing weight checks in `Validate()`. (2) Implement the convergence bonus in the matcher waterfall — decide where in the scoring pipeline the bonus applies and which signal combinations qualify as "converging".

## User decisions required

-------
- [ ] Cross-bracket tie resolution requires user decision.
  - Issue: PRISM-match.md says "KO the image unless it can sit at the exact same det order position in every matching FamilyID" when an image remains a candidate for multiple FamilyIDs after all brackets. The current implementation silently passes ties to the next bracket with no cross-bracket candidacy accumulator. An image that ties across all brackets is KO'd by `KoUnmatched` with reason `MATCH_NOT_FOUND`, not `MATCH_TIE`.
  - Block: Implementing the spec behavior requires a candidacy accumulator that collects all tied FamilyIDs across Brackets 1–3 and defers the tie-break to a det-position comparison step. This is moderate complexity and requires an explicit decision on whether the current pass-through-on-tie behavior is accepted for V1 or must be replaced.
  - Options: (a) Implement the accumulator and det-position tie-break per spec. (b) Accept the current behavior and document it as a known V1 limitation. Either decision must be recorded here before a developer starts work.
  - Answer (context from existing docs, decision still yours — pending approval):
    The spec already defines the *target* behavior: PRISM-match.md "Waterfall Matching Gates" → "Image remains candidate for multiple FIDs → KO unless it can sit at the exact same `_det` position in every matching FID." Option (a) is the spec-compliant implementation of that line; option (b) is an accepted deviation. Existing data does not, by itself, decide whether V1 must ship spec-compliant or may defer — that is a scope call only you can make. Two things to note if (a) is chosen: it needs a cross-bracket candidacy accumulator (current code passes ties forward bracket-by-bracket), and the KO reason should become `MATCH_TIE` rather than the current `MATCH_NOT_FOUND` so the manifest distinguishes ties from genuine no-matches. No new data required for either option — only your scope decision.
