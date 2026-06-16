# PRISM — Image Matching

## Overview

`ImageMatcher.cs` (`jb/src/core/Images/ImageMatcher.cs`) calculates the most probable association between image and FamilyID.

- Uses strategy design pattern to load matcher classes.
- Scoring logic must be **readable by a 10-year-old and easy to update**.
- Rules and their values are located near the top of the file, grouped per matcher class.
- More than one image can belong to a single FamilyID.

---

## Waterfall Matching Gates

Matching is a waterfall pipeline with hard gates. After every bracket, already-matched images are removed from consideration.

**Stage 1:** Only permit numerical **single-token** matches with **edit distance 0**.

**Stage 2:** Permit numerical **multiple-token** matches with final **TCD distance ≤ 2.55**.

**Stage 3:** Permit multiple-string-token matches only if:
- The image matches exactly one FamilyID, AND
- That FamilyID does not already have a candidate image of that image type.

**Stage 4 cleanup:** KO remaining unmatched images. Not renamed, not processed, kept in manifest with original filename.

**Stage 5 finalize:** Finalize image-to-FamilyID combinations, clustering the image collection into FamilyID clusters. Matching is done — move to det-ordering.

**Tie-breaking:** If an image remains a candidate for multiple FamilyIDs after all brackets, KO the image unless it can sit at the exact same `_det` order position in every matching FamilyID.

---

## `NumericMatcher.cs`

Parses any input string to a tokenized numerical-only string, then compares all tokens against numerical columns of the IEM by edit distance.

**Identical match required.** Shortest distance between the entire input string and FamilyID wins. Fewer tokens = higher score.

**Numeric scoring rules** (scoring starts at 100%):
- Single token + identical match: −0% (keep 100%)
- Token count: deduct `5% × (number of tokens − 1)` — e.g. 1 token = −0%, 2 tokens = −5%
- Edit distance: deduct `edit distance / string length` — e.g. `ABC` vs `ABD`: edit distance 1, length 3 → 1 − 1/3 = 67%
- Length difference: if token set is otherwise identical but column token is longer than filename token: subtract `1 − (length difference / total column length)` — e.g. `abcde` vs `abcdefgh`: 1 − 5/8 = −0.375

Only scores close to 100% (threshold set in `ImageMatcher.cs`) result in an image/FamilyID candidacy.

**Numeric token combination rules:**
- Tokens may be joined when filename order is preserved and the joined token can form a configured FamilyID candidate.
- Current FamilyIDs are 8 digits. Single exact 8-digit token has TCD 0 (strongest). Multiple tokens may combine into 8-digit candidate but record a token-count cost.

**Exact matcher threshold:**
- Uses TCD for exact-character numeric candidates (not classical Levenshtein typo tolerance).
- Single exact 8-digit FamilyID token: TCD 0 — strongest numeric identity evidence.
- Numeric fragments may form 8-digit candidate only when concatenation exactly equals candidate ID and TCD ≤ numeric rule `maxDistance` in `MatchingConfig.json`.
- Current `maxDistance: 1` — allows low-fragmentation exact-character combinations, but does **not** allow a one-character Levenshtein mismatch.
- Reordered, incomplete, character-mismatched, or above-threshold candidates → rejected or retained as rejected evidence in `MatchEvidence`.

**Numeric false-positive handling (dimensions, dates, units):**

Numeric noise excluded before scoring:
- Dimension patterns: `800x1200`
- Date-like values: `2024-05-18`, `18/05/2024`, `05.18.24`
- Unit-adjacent numbers: `25cm`, `2 kg`, `100%`, `30mm`, `5m`
- Numbers directly tied to date words: `date 2024`, `expires 05`

Trusted identifier columns preserved (not noise-filtered): `FamilyID`, `FamID`, `EAN`, `SKU`, `Ref`, `Reference`.

`NoiseFilter.cs` owns the filtering code. Trusted numeric ID columns are not cleaned as noise.

---

## `StringMatcher.cs`

Parses input string to logical string tokens, then compares against categorical, descriptive, and mixed columns of the IEM.

**Column types:**
- **Categorical:** product type, material, color — cells contain up to 4 strings of low-cardinality, 3–12 chars (5–6 ideal)
- **Descriptive:** product info, descriptions, washing instructions, materials, marketing text
- **Mixed:** all columns that don't fit categorical or descriptive criteria

**String scoring:** Similar to numeric scoring. Edit distance for categorical columns is penalized less (spelling mistake is less severe than serial number discrepancy). More string tokens matched → higher score.

**Normalization before matching:**
- Convert casing to lowercase
- Convert diacritics to base alphabetical character
- Split punctuation and separators consistently into token boundaries
- Collapse whitespace
- Preserve original token text in bounded evidence for diagnostics
- Filename offset/start-end metadata is not required. If trace-back is needed, use the retained token value together with the original filename stored on `ImageRecord_INPUT`.

**Descriptive column matching:**
- Sanitize with `NoiseFilter.cs` before matching.
- All image tokens searched against sanitized descriptive text.
- More salient tokens are more valuable. Longer tokens are more valuable.
- If descriptive evidence + other token evidence leaves one Excel row → that FamilyID is a candidate.

**Mixed column matching:**
- Treated like a string after `NoiseFilter.cs` cleanup.
- Trivial classification tags (`ImageRecord_LAMBDA.Tags.Trivial`) are **excluded**.
- String, numeric, and non-trivial classification tokens can participate.
- If any combination of image tokens leaves a single FamilyID row → that FamilyID is a candidate.

**NoiseFilter.cs usage summary:**
- Filename tokens: NOT cleaned with `NoiseFilter.cs`
- Excel numeric columns: NOT cleaned
- Excel string-category columns: NOT cleaned
- Excel mixed columns: cleaned with `NoiseFilter.cs`
- Descriptive text: sanitized with `NoiseFilter.cs`
- A filename string token must match internal Excel data **exactly** to count as matching evidence.

---

## `ImageLabelingMatcher.cs`

Uses `clip-vit-b32-uint8` at `jb/src/core/Images/Classify/ONNX/clip-vit-b32-uint8/`.

Image-label evidence weights (from `MatchingConfig.json`):
| Evidence | Weight | Notes |
|---|---|---|
| `ProductColor` | `1.0` | Strongest — most valuable visual catalog cue |
| `ProductType` | `0.8` | Strong supporting evidence |
| `ProductMaterial` | `0.5` | Weaker — more likely to be misclassified |
| `ALL` image-label overlap | `0.6` | Broad corroborating evidence |

These weights support or weaken candidate confidence but do **not** override exact numeric identity evidence.

Image-label confidence determines which labels are meaningful. Only influential labels (≥ `Confidence_Threshold`) drive decisions.

---

## Language & Synonym Handling

- String matching uses exact normalized token matching first.
- Configured multilingual synonyms count as matching evidence for known product words (especially colors, materials, types).
- Synonym code and mapping files live in `jb/src/core/Images/Match/Translate`.
- Synonym dictionary: `jb/src/core/Images/Match/Translate/TranslationConfig.json`.
- Automatic language detection or translation is **not** part of matching.

---

## Stop Words

Configured in `jb/src/core/Images/Match/Translate/TranslationConfig.json`.

Two categories:
- **General stop words:** `the`, `and`, `of`, `de`, `la`, `les`, etc.
- **Domain stop words:** `product`, `image`, `style`, `size`, `color`, `collection`, etc.

Stop words are ignored by string matching but remain available in diagnostic evidence when diagnostics request ignored-token details.

Trivial classification tags (`ImageRecord_LAMBDA.Tags.Trivial`) remain excluded from mixed-column matching separately.

---

## `MatchEvidence` — Retained Evidence Shape

See `PRISM-models.md` for full field list. Summary:
- Final candidate FamilyID and score
- Threshold status, tie status, safe decision explanation
- Top candidate evidence; rejected near-tie evidence (bounded)
- Numeric token, string token, and classification-label evidence
- Relevant `ImageNGP` summary; matcher names, scores, confidences, weights
- Optional diagnostic snapshot references for heavy/verbose evidence
