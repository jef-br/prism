# PRISM — Image Matching
*Abbreviations: `GLOSSARY.md`*

## Overview

`ImageMatcher.cs` (`jb/src/core/Images/ImageMatcher.cs`) calculates the most probable image↔FID association. Uses strategy pattern to load matcher classes. Scoring logic must be readable and easy to update. Rules and values are grouped per matcher class at the top of the file. More than one image can belong to a single FID.

---

## Waterfall Matching Gates

Single pass. Matched images removed from consideration after each bracket.

**Bracket 1:** Only numerical **single-token** matches with **edit distance 0**.
**Bracket 2:** Numerical **multiple-token** matches with final **TCD ≤ 2.55**.
**Bracket 3:** Multiple-string-token matches only if: image matches exactly one FID AND that FID has no existing candidate of that image type.
**Bracket 4 semantic:** Applies to remaining unmatched images only. Candidate pool is restricted to FamilyIDs with **0 images assigned** in Brackets 1–3. Each image is evaluated against this pool using a combined CLIP + numeric + string signal:
- **CLIP — hard filter (ProductType):** At least one influential CLIP `ProductType` tag must match the candidate family's ProductType column value. Candidates without a matching product type are excluded.
- **CLIP — hard filter (ProductColor, conditional):** If the Excel model contains a ProductColor column for any remaining candidate, at least one CLIP color tag must match that candidate's color value. Candidates with a color value that contradicts all CLIP color tags are excluded.
- **Numeric tokens — candidate reduction:** Digit tokens from the filename are compared against candidate family numeric columns. Tokens that match some but not all candidates narrow the pool to only the matching families. Tokens that match all or none of the remaining candidates have no effect.
- **String tokens — candidate scoring:** Filename string tokens are matched against all non-numeric columns of each remaining candidate. The candidate with the most matching tokens wins. If the top match count is shared by multiple candidates, the match is a tie and the image is not assigned.
- **Acceptance:** Exactly one candidate must remain after CLIP and numeric filtering, with a unique highest string-token score ≥ `SemanticThreshold` (`MatchingConfig.json`).
- **Weighting:** All three signals use `SemanticWeight` (`MatchingConfig.json`) when computing `MatchEvidence.FinalScore`.

**Bracket 5 cleanup:** KO remaining unmatched images. Not renamed; kept in manifest with original filename.
**Bracket 6 finalize:** Finalize image→FID assignments; cluster into FID groups → move to DO.

**Tie-breaking:** Image remains candidate for multiple FIDs → KO unless it can sit at the exact same `_det` position in every matching FID.

---

## `NumericMatcher.cs`

Parses any input string to a tokenized numerical-only string; compares tokens against numerical IEM columns. An **exact (identical) match is required** — there is no edit-distance tolerance. Fewer tokens used to reach the exact match = higher score.

**Scoring (TCD — token-count only):**
Both brackets require an exact/perfect numeric match (the token, or the in-order concatenation of tokens, must equal the family numeric value). Because the match is always exact, edit-distance and length-difference are always 0 and contribute nothing. The only scoring axis is the number of tokens used to achieve the perfect match (TCD).
- Bracket 1 — single identical token: fixed confidence `1.0` (TCD 0).
- Bracket 2 — multiple tokens concatenated to an exact value: ranked by **fewest tokens used** (lower TCD = higher confidence); accepted only when the concatenation exactly equals the candidate value AND TCD ≤ `maxDistance`.

Only exact matches qualify → FID candidacy.

**Token combination rules:**
- Tokens may be joined when filename order is preserved and joined token forms a valid FID candidate.

- Current FIDs: 8 digits. Single exact 8-digit token → TCD 0 (strongest). Multiple tokens may combine → record token-count cost.

**Exact matcher threshold:**
- Uses TCD, not classical Levenshtein typo tolerance.
- Single exact 8-digit token → TCD 0.
- Fragments may form 8-digit candidate only when concatenation exactly equals candidate ID AND TCD ≤ `maxDistance` in MCFG.

- Current `maxDistance: 1` — allows low-fragmentation exact-character combinations; does **not** allow one-character Levenshtein mismatch.
- Reordered/incomplete/character-mismatched/above-threshold → rejected or retained as rejected evidence in ME.

**Numeric false-positive handling (excluded before scoring):**
Dimension patterns (`800x1200`), dates (`2024-05-18`, `18/05/2024`, `05.18.24`), unit-adjacent numbers (`25cm`, `2 kg`, `100%`), numbers tied to date words (`date 2024`).

Trusted numeric columns (not noise-filtered): `FamilyID`, `FamID`, `EAN`, `SKU`, `Ref`, `Reference`.

The FamilyID rule resolves against the intrinsic `FamilyIDRecord.FamilyID` (the 8-digit identifier, also the output filename stem) — not a column lookup; other numeric rules (`EAN`, …) resolve against their IEM column values. The FamilyID column name comes from `ExcelConfig.RecordPrimaryKey`.

`NoiseFilter.cs` owns filtering code.

---

## `StringMatcher.cs`

Parses input string → logical string tokens; compares against categorical, descriptive, and mixed IEM columns.

**Column types:**
- **Categorical**: PT, material, color — low-cardinality cells, 3–12 chars (5–6 ideal)
- **Descriptive**: product info, descriptions, washing instructions, marketing text
- **Mixed**: all columns that don't fit categorical or descriptive

**Scoring:** Unlike numeric matching (which requires an exact match with no edit-distance tolerance), string matching tolerates edit distance — for categorical columns it is less penalized (spelling mistakes less severe than serial number discrepancy). More matched tokens → higher score.

**Normalization before matching:** lowercase, diacritics → base char, split punctuation/separators → token boundaries, collapse whitespace. Original token text retained in bounded evidence.

**Descriptive column matching:**
- Sanitize with `NoiseFilter.cs` before matching.
- More salient/longer tokens = more valuable.
- If descriptive evidence + other token evidence → single FR row → FID candidate.

**Mixed column matching:**
- Treated as string after `NoiseFilter.cs` cleanup.
- `IRL.Tags.Trivial` excluded.
- String, numeric, and non-trivial classification tokens participate.
- Any combination leaving single FR row → FID candidate.

**NoiseFilter.cs:** NOT applied to filename tokens, numeric columns, or string-category columns. Applied to mixed columns and descriptive text. Filename string token must match Excel data **exactly** to count as evidence.

---

## `ClipLabelEnricher.cs`

Uses `clip-vit-b32-uint8` at `jb/src/core/Images/Classify/ONNX/clip-vit-b32-uint8/`.

**Not a matcher** — never assigns FamilyIDs. Provides CLIP label evidence for already-matched records (Brackets 1–3) and supplies the hard-filter signal for `SemanticMatcher` (Bracket 4).

CLIP label evidence weights (from MCFG):

| Evidence | Weight |
|---|---|
| `ProductColor` | `1.0` |
| `ProductType` | `0.8` |
| `ProductMaterial` | `0.5` |
| ALL CLIP label overlap | `0.6` |

Weights support/weaken candidate confidence but do **not** override exact numeric identity evidence. Only influential CLIP labels (≥ `Confidence_Threshold` at classification time) are included in `Tags.Influential`.

---

## Language & Synonym Handling

- Exact normalized token matching first.
- Configured multilingual synonyms count as evidence for known product words (colors, materials, PTs).
- Code + mapping files: `jb/src/core/Images/Match/Translate/`.
- Synonym dictionary: `jb/src/core/Images/Match/Translate/TranslationConfig.json`.
- No automatic language detection or translation.

---

## Stop Words

Configured in `TranslationConfig.json`. General: `the`, `and`, `of`, `de`, `la`, `les`, etc. Domain: `product`, `image`, `style`, `size`, `color`, `collection`, etc. Ignored by matching; retained in diagnostic evidence. `IRL.Tags.Trivial` excluded from mixed-column matching separately.

---

## ME — `MatchEvidence` Shape

See `PRISM-models.md` for full field list. Summary:
- Final candidate FID and score; threshold status; tie status; safe decision explanation
- Top candidate evidence; rejected near-tie evidence (bounded)
- Numeric token, string token, classification-label evidence
- Relevant INGP summary; matcher names, scores, confidences, weights
- Optional diagnostic snapshot refs for heavy/verbose evidence

---

## Ticket Close-Out Notes

**Ticket 2 — MatchEvidence missing fields:** MatchEvidence now has `ThresholdStatus` (bool — true when FinalScore meets or exceeds `matchingConfig.SemanticThreshold`), `RejectedNearTieEvidence` (near-tie candidates passed over in Brackets 1–2), and `MatcherWeights` (per-matcher contributions as `IReadOnlyList<MatcherContribution>`). `AcceptedMatcherName` retained. `RejectedNearTieEvidence` is collected in `RunWaterfall` via a `Dictionary<string, List<CandidateSummary>> rejectedNearTies` keyed by `InitialFullName`, populated by `NumericMatcher.TryMatchBracket1WithTies` and `TryMatchBracket2WithTies` when a tie occurs, and attached to the evidence at the point of match acceptance in Brackets 1–4.

**Ticket 3 — Bracket 3 duplicate-phenotype guard:** Bracket 3 duplicate-phenotype guard is implemented in `ImageMatcher.RunBracket3` via `HasDuplicatePhenotypeInFamily`. Rejects a string match when the target FamilyID already has a non-KO matched record with the same non-null SelectedPhenotype.

**Ticket 4 — Pre-normalization token text:** Pre-normalization token text is preserved via `FilenameToken(string Original, string Normalized)` struct in StringMatcher. Evidence records carry `imageToken.Original` (the raw filename text before diacritics/case normalization) alongside the matched family token.

**Ticket 5 — Weight_MatchingSignalsConverging convergence bonus:** Implemented in `ImageMatcher.FinalizeMatches` (Bracket 6). A record converges when its `MatchEvidence` has at least 2 of: `NumericTokenEvidence.Count > 0`, `StringTokenEvidence.Count > 0`, `ClassificationLabelEvidence.Count > 0`. When converging, `FinalScore = Math.Min(1.0, FinalScore + Weight_MatchingSignalsConverging)` (0.25) and `SafeExplanation` notes the bonus. `PrismConfiguration.Weight_MatchingSignalsConverging` is loaded in `ImageMatcher.Run` and passed through `RunWaterfall` to `FinalizeMatches`.
