# PRISM — Excel Processing (IEM)
*Abbreviations: `GLOSSARY.md`*

## Config

PK rules from `RecordPrimaryKey` and `FamilyIDProperties` in XCFG. `HeaderRowIndicators` lists **canonical English header keys** (e.g. `familyid`, `ean`, `color`); each key is expanded to its multilingual term set via the `headerGroups` section of `TranslationDictionary.json` (the same dictionary that feeds value matching, kept in a separate `headerGroups` array so header vocabulary never contaminates value synonyms). Supported header languages: DE, EN, ES, FR, IT, NL.

---

## PK Rules

- PK must match configured numeric requirement.
- PK must be **exactly 8 digits** (current config).
- Each data row belongs to exactly one FID. Duplicate FIDs cannot exist in IEM.
- FamilyID column resolved by **header-name OR cell-pattern** (whichever fires):
  - **Header name:** any token of the column header resolves to the `familyid` header group. Single candidate → chosen; multiple → disambiguated by cell-pattern, else leftmost.
  - **Cell pattern (fallback for unrecognized-language headers):** the one column where **every non-empty cell is a valid 8-digit FamilyID and all those values are unique within the column**.
  - Header-name carries sheets that repeat a FID across rows; cell-pattern carries sheets whose header text is in an unrecognized language. A column identified by name but holding non-compliant values (e.g. refco `1234567890-01`) is still selected, then its rows KO as `excel.invalid_primary_key`.

---

## Header Row Detection

- Header cells are matched **token-by-token**, not as whole strings: each cell is diacritics-folded (so `código`→`codigo`), tokenized, general stop-words (`de`, `la`, `of`…) dropped, then each remaining token is tested against the active indicator set. Domain stop-words (`color`, `style`, `size`…) are **not** dropped here — they are meaningful column headers.
- A token matches when it is an active indicator literally, or resolves through a `headerGroups` entry to an active canonical id; edit distance 1 (token length ≥ 4) gives typo tolerance at `EditDistanceOneConfidence`.
- Candidate header row: ≥ **`MinimumMatchedColumnRatio`** (currently 40%) of non-empty cells must match.
- Among qualifying rows the winner is the one with the **most matched columns** (ties broken by average confidence). This rejects sparse single-cell title rows that would otherwise score a perfect ratio.

---

## Column Validity

- Valid column: non-null/non-empty values in ≥ **20%** of rows.
- Drop columns below 20% fill.
- Fill empty cells with `""` **after** column is accepted.

---

## Duplicate Column Handling

- Recognized single-concept columns are canonicalized to their English header id (e.g. `Descripción`/`DESCRIPCION` → `description`, `COMPOSICIÓN` → `material`), so cross-language duplicates collapse. Canonicalization is conservative — only unambiguous headers in a safe-merge set are renamed; mixed-concept headers (e.g. `Reference-colour`) keep their raw name to avoid wrong merges.
- Identical headers → duplicate candidates.
- Content must be identical for direct duplicate.
- Headers differ but > **20% of cells** appear in both → merge and deduplicate.
- When duplicate columns disagree for same FID: tokenize both non-empty values, merge unique normalized tokens into canonical property, retain original cell values as conflict evidence.

---

## Duplicate Row / FID Handling

- All cells identical → deduplicate row.
- Same FID in multiple rows → merge all non-empty data into one FR; unique values preserved; conflicting values kept as tokenized evidence.

---

## Invalid / Missing Key

- Missing, malformed, or non-compliant PK rows → KO in `manifest.json`. Does **not** stop Excel parsing.
- Worksheet with no PK column → KO in `manifest.json`.

---

## Merged Cell Handling

Only merge cells in the same column when their value is identical.

---

## Provenance

No provenance retained beyond processing — cleanup removes all temporary batch files.

---

## IEM → FR Mapping

Each valid FID maps to exactly one FR (from XCFG): numeric PK, exactly 8 digits; duplicate rows merged; empty cells → `""` after column accepted; columns <20% fill dropped; duplicate columns deduplicated/merged; conflicts kept as tokenized evidence; invalid-key rows and key-less worksheets → KO.
