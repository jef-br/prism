# PRISM — Excel Processing (IEM)
*Abbreviations: `GLOSSARY.md`*

## Config

PK rules from `RecordPrimaryKey` and `FamilyIDProperties` in XCFG. Canonical header source: `HeaderRowIndicators`.

---

## PK Rules

- PK must match configured numeric requirement.
- PK must be **exactly 8 digits** (current config).
- Each data row belongs to exactly one FID. Duplicate FIDs cannot exist in IEM.
- Header cell with edit distance 0 to `RecordPrimaryKey` → that cell is the PK column.

---

## Header Row Detection

- Candidate header row: ≥ **50%** of columns must match configured indicators.
- Edit distance > **12%** of cell length → not a match.
- Edit distance 1 → **75% confidence**; distance 2 → **50% confidence**.
- Exact match (distance 0): use TCD (`jb/src/core/Excel/TCD FOR EXCEL COLUMN HEADER.cs`). TCD uses Levenshtein + Kendall Tau for token count and reordering → 100% confidence.

---

## Column Validity

- Valid column: non-null/non-empty values in ≥ **20%** of rows.
- Drop columns below 20% fill.
- Fill empty cells with `""` **after** column is accepted.

---

## Duplicate Column Handling

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
