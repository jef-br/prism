# How Matching Actually Works (Right Now)

*Plain-language description of the code in `jb/src/core/Images/Match/`.*

---

## The Big Idea

Imagine a big pile of photos arrived in the mail — but none of them have proper labels on the front.
You also have a product catalog (an Excel file) that lists every product with its ID number, name, color, and material.

**Matching** is the job of figuring out which photo belongs to which product.

Each product has a **FamilyID** — think of it like the product's home address.
Each photo's filename is the clue. The matcher reads the filename, looks it up in the catalog, and writes the FamilyID on the photo.

If it can't figure out the address → the photo gets a **KO stamp** ("rejected") and is kept in the output with its original name so you can inspect it later.

---

## The Four Attempts (the Waterfall)

The matcher doesn't give up after one try. It runs **four attempts in a row**. Once a photo is matched, it's removed from the pile. Unmatched photos move on to the next attempt.

```
Photo pile
   │
   ├ Attempt 1: Look for a single exact product code   : matched? → done
   │
   ├ Attempt 2: Assemble a product code from pieces    : matched? → done
   │
   ├ Attempt 3: Match on words (name, color, material) : matched? → done
   │
   ├ Attempt 4: (Not a matching step — adds extra clues to already-matched photos)
   │
   └ Still here? → KO (rejected, kept in manifest)
```

---

## Attempt 1 — Find a Single Exact Product Code

**Code:** `NumericMatcher.TryMatchBracket1()`

**What it does:**
1. Pull out every run of digits from the filename. `IMG_50123456_hero.jpg` → digits found: `50123456`
2. Check each number against every product's ID (FamilyID) and EAN in the catalog — **one digit wrong = no match**.
3. If exactly one product matches → the photo goes home. **Score = 1.0 (perfect).**
4. If zero products match, or if two products both have that number → no result; move to Attempt 2.

**Example:**
- Filename: `PROD_50123456_front.jpg`
- Digit token: `50123456`
- Catalog row with FamilyID `50123456` → **Match! Score 1.0.**

**Why "exactly one"?** If two different products share the same code in the catalog (data quality problem), the matcher refuses to guess. It's better to reject than to be wrong.

---

## Attempt 2 — Assemble a Product Code from Multiple Pieces

**Code:** `NumericMatcher.TryMatchBracket2()`

Sometimes the filename splits the product code across multiple parts:
`IMG_501_23456_pack.jpg` → pieces `501` and `23456` → together they make `50123456`.

**What it does:**
1. Take every combination of 2 or more consecutive number sequences.
2. Join them together and see if the result matches a product code exactly.
3. Measure how "messy" the split was using TCD (TokenizedConcatenationDistance).
   - TCD = 0 → a clean two-piece split, almost as good as a single code.
   - TCD = 1 → a bit messy but still acceptable.
   - TCD > 1 (config default: maxDistance = 1.0) → rejected.
4. Convert TCD to a confidence score: `score = exp(−0.5 × TCD)`
   - TCD = 0 → score ≈ 1.0
   - TCD = 1 → score ≈ 0.61
5. If exactly one product matches → done.

**Note on scoring:** The spec describes a different formula (three penalties: token count, edit distance, length difference). The code uses the simpler TCD-based formula instead. This is a known deviation (see Known Gaps below).

---

## Attempt 3 — Match on Words in the Filename

**Code:** `StringMatcher.TryMatch()`

If no product code was found, try matching the words in the filename against the product's name, color, material, or description.

**Step 1 — Clean up the filename:**
- Make everything lowercase. `Blue-Jacket` → `blue jacket`
- Remove accents. `café` → `cafe`
- Split on punctuation and separators. `blue-jacket_2024.jpg` → `blue`, `jacket`, `2024`
- Drop pure numbers (those are for Attempts 1–2).
- Drop stop words: `the`, `of`, `and`, `product`, `color`, `image`, etc.
- Apply the noise filter: remove dates (`2024-06-18`), dimensions (`800x1200`), units (`50mm`, `2kg`), etc.

**Step 2 — Compare against each product in the catalog:**
- Skip numeric columns (ID, EAN) — already handled.
- Check the product's name, color, material, description columns.
- Does any filename word appear in the product's data? Synonyms also count (`bleu` = `blue`).

**Step 3 — Decide:**
- If exactly one product has matching words → accepted.
  - Score = fraction of filename words that matched: e.g. 3 out of 4 words matched → score = 0.75
- If zero products match, or two+ products both have those words → no result; move to KO.

**Known gap:** The spec requires a second check before accepting: the target product must not already have a confirmed image of the same type (e.g., two "front view" images for the same product). This check is **not yet implemented**. Two front-view images can both get matched to the same product via Attempt 3, and the Ordering stage has to deal with the collision.

**Example:**
- Filename: `blue-softshell-jacket-2024.jpg`
- After cleaning: tokens `blue`, `softshell`, `jacket`
- Only one product row matches all three: "Softshell Jacket Blue" → **Match! Score = 3/3 = 1.0**

---

## Attempt 4 — Add CLIP Label Evidence (Evidence Only, No New Matches)

**Code:** `ImageLabelingMatcher.BuildEvidence()`

This is NOT a matching step. It runs on photos that **already have** a FamilyID.

The Classify stage ran an AI model (CLIP) on every photo and produced tags like `blue`, `jacket`, `cotton`. Attempt 4 checks if those tags also appear in the matched product's catalog data (color, type, material).

If they do, it adds a **label evidence note** to the photo's record — saying something like "the AI said 'blue', and this product is indeed listed as Blue (ProductColor match, weight 1.0)."

**This only adds supporting notes. It never changes a FamilyID or creates new matches.**

Evidence weights:
| Column matched | Weight |
|---|---|
| ProductColor | 1.0 |
| ProductType | 0.8 |
| ProductMaterial | 0.5 |
| Any other column | 0.6 |

---

## What Happens to Rejected Photos (KO)

Any photo still unmatched after Attempts 1–3 is marked:
- `IsKo = true`
- `KoReason = "MATCH_NOT_FOUND"`
- Original filename kept (not renamed)
- Still included in the output manifest so you can see it

---

## The Evidence Record (MatchEvidence)

Every photo — matched or rejected — gets a `MatchEvidence` record that explains what the matcher found.

| Field | What it stores |
|---|---|
| `FinalFamilyId` | The matched product's ID, or null if rejected |
| `FinalScore` | Confidence 0–1 (1.0 = perfect, 0.6 = acceptable, 0 = rejected) |
| `AcceptedMatcherName` | Which attempt succeeded (`"NumericMatcher.Bracket1"`, `"StringMatcher.Bracket3"`, etc.) |
| `NumericTokenEvidence` | Which digit tokens matched which product fields |
| `StringTokenEvidence` | Which word tokens matched which product columns |
| `ClassificationLabelEvidence` | Which CLIP tags matched product data (Attempt 4) |
| `IsKo` | True if rejected |
| `KoReason` | Why it was rejected (`"MATCH_NOT_FOUND"`) |
| `SafeExplanation` | Human-readable sentence: "Matched via numeric token '50123456' → FamilyID 50123456" |

---

## Known Gaps (Deviations from Spec)

These are tracked in `jb/src/core/Images/Match/jbtodo.md`.

| # | What the spec says | What the code does |
|---|---|---|
| 1 | **Scoring formula**: token count + edit distance + length difference | **Bracket 1** uses fixed 1.0; **Bracket 2** uses TCD exponential decay |
| 2 | **MatchEvidence** should store `ThresholdStatus`, `RejectedNearTieEvidence`, per-matcher weights | Only `AcceptedMatcherName` stored; the other fields are absent |
| 3 | **Bracket 3 duplicate-type guard**: reject if the target product already has an image of the same phenotype | No check exists; duplicates silently allowed |
| 4 | **Original token text** should be preserved in evidence (e.g. `Café` before normalization) | Only the normalized form (`cafe`) is stored |
| 5 | **Cross-bracket tie resolution**: if an image ties, check whether it fits the same det-slot position in all tied products | Tied images are silently passed to the next bracket and eventually KO'd as `MATCH_NOT_FOUND` |

---

## Configuration Files

| File | What it controls |
|---|---|
| `jb/src/core/Images/Match/MatchingConfig.json` | Which Excel columns to match against; TCD `maxDistance`; label evidence weights |
| `jb/src/core/Images/Match/Translate/TranslationDictionary.json` | Synonyms (e.g. `bleu` = `blue`); stop words to ignore |
