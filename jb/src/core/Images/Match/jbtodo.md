# Match Stage Open Decisions

- [ ] MatchEvidence is missing three fields required by PRISM-models.md.
  - File: `jb/src/core/Images/Match/MatchEvidence.cs`.
  - Missing fields: (1) ThresholdStatus — boolean or enum indicating whether FinalScore exceeded the configured match threshold; (2) RejectedNearTieEvidence — bounded list of near-tie candidates from Brackets 1–2 that were passed over; (3) per-matcher weights and confidence scores (only AcceptedMatcherName string is stored, not individual matcher weights/confidence).
  - Fix: Add the three fields to MatchEvidence and populate them during waterfall execution. Design is unambiguous from PRISM-models.md; complexity is in populating near-tie evidence at pass-through points.

- [ ] StringMatcher Bracket 3 missing "no duplicate image type in same FamilyID" guard.
  - File: `jb/src/core/Images/Match/StringMatcher.cs` lines 41–50.
  - Issue: PRISM-match.md specifies Bracket 3 allows multiple-string-token matches only when (a) image matches exactly one FamilyID AND (b) that FamilyID does not already have a candidate image of the same image type. Condition (b) is not implemented.
  - Fix: Before accepting a string-bracket match, check whether the candidate FamilyID already has a non-KO LambdaRecord with the same SelectedPhenotype.

- [ ] Numeric scoring formula does not match PRISM-match.md.
  - File: `jb/src/core/Images/Match/NumericMatcher.cs`.
  - Issue: Spec defines multi-component deduction model: start at 100%, deduct 5%×(token_count−1), deduct edit_distance/string_length ratio, deduct length_difference ratio. Implementation uses fixed FinalScore=1.0 for Bracket 1 and TokenizedConcatenationDistance for Bracket 2. The three-component deduction model is absent.
  - Fix: Implement the full formula. No user decision needed — formula is fully specified in PRISM-match.md.

- [ ] Original pre-normalization token text not preserved in StringTokenEvidence.
  - File: `jb/src/core/Images/Match/StringMatcher.cs` lines 100–108.
  - Issue: Evidence records store the normalized form of the filename token. PRISM-match.md requires original token text to be preserved for diagnostics.
  - Fix: Store both the original text and the normalized text in TokenEvidenceItem.

- [ ] Match stage has zero unit tests.
  - Issue: No test directory exists for the Matched stage. NumericMatcher, StringMatcher, ImageLabelingMatcher, and ImageMatcher waterfall orchestration have no unit tests at all.
  - Required tests include: all happy paths, KO path (MATCH_NOT_FOUND), tie pass-through behavior, NoiseFilter integration in string path, synonym resolution via TranslationConfig, and multi-FamilyID candidacy.
  - Fix: Create `jb/src/tests/Prism.Core.Tests/Match/` and add tests for all public match logic.

- [ ] Cross-bracket tie resolution requires user decision.
  - Issue: PRISM-match.md says "KO the image unless it can sit at the exact same det order position in every matching FamilyID" when an image remains a candidate for multiple FamilyIDs after all brackets. The current implementation does not accumulate multi-FamilyID candidacy across brackets — a tie silently passes to the next bracket. There is no cross-bracket candidacy accumulator, so the "same det position" resolution cannot be applied.
  - Block: Implementing this requires either a candidacy accumulator across all brackets (moderate complexity) or an explicit decision that the current pass-through-on-tie behavior is accepted. Requires user decision before a developer can implement it.
