# PRISM — Excel Processing (IEM)

## Config Source

Primary key rules come from `RecordPrimaryKey` and `FamilyIDProperties` in `ExcelConfig.json`.
Canonical header source: use `HeaderRowIndicators` to find the header row before selecting canonical column names.

---

## Primary Key Rules

- A primary key cannot be accepted unless it matches the configured numeric requirement.
- A primary key cannot be accepted unless it is **exactly 8 digits** under the current config.
- Every single data row belongs to **one and only one** FamilyID.
- Duplicate FamilyID records **cannot** exist in the Internal Excel Model.
- When a detected header row contains a cell with edit distance 0 to `RecordPrimaryKey`, use that cell as the primary key column.

---

## Header Row Detection

- At least **50%** of columns in a candidate header row must match configured indicators.
- An edit distance greater than **12%** means the cell does not qualify as an indicator match.
- Edit distance 1 → **75% confidence**
- Edit distance 2 → **50% confidence**
- Exact match (edit distance 0): use Tokenized Concatenation Distance (TCD).
  - The method is in `jb/src/core/Excel/TCD FOR EXCEL COLUMN HEADER.cs`.
  - TCD is a version of Levenshtein that uses the Kendall Tau correlation coefficient to account for token count and reordering to achieve 100% confidence.

---

## Column Validity Rules

- A column must contain non-null and non-empty values in at least **20%** of its rows to be valid.
- Drop columns that do not contain enough useful values.
- Fill empty cells with an empty string **after** deciding the column itself is valid.

---

## Duplicate Column Handling

- Identical headers → two columns are duplicate candidates.
- Content must be identical before two columns are considered direct duplicates.
- If headers differ but more than **20% of cells** appear in both columns → merge and deduplicate the cells.
- When duplicate columns disagree for the same FamilyID: tokenize both non-empty cell values, merge unique normalized tokens into the canonical property, and keep the original cell values as conflict evidence for manifest/workbench review.

---

## Duplicate Row / FamilyID Handling

- Deduplicate the entire row when all other cells in the involved records contain duplicate information.
- When the same FamilyID appears in multiple rows: merge all non-empty data into one FamilyID record, preserve unique values, and keep conflicting values as tokenized evidence instead of overwriting them.

---

## Invalid / Missing Key Handling

- Rows with missing, malformed, or non-config-compliant primary key values: skip the row, report as KO in `manifest.json`. Does **not** stop Excel parsing.
- When a worksheet has no primary key column: skip that worksheet, report as KO in `manifest.json`.

---

## Merged Cell Handling

Only merge cells in the same column when their value is identical.

---

## Provenance

Do not keep provenance beyond processing — cleanup removes all temporary batch files.

---

## IEM → FamilyRecord Mapping

The IEM maps each valid FamilyID to exactly one `FamilyRecord`.

Mapping rules:
1. Use `RecordPrimaryKey` and `FamilyIDProperties` from `ExcelConfig.json`.
2. Primary key must satisfy numeric requirement and be exactly 8 digits.
3. Every valid data row belongs to one and only one FamilyID.
4. Duplicate rows merge into one `FamilyRecord`.
5. Empty cells become empty strings after the column is accepted as valid.
6. Columns without enough useful values are dropped.
7. Duplicate columns deduplicated or merged per duplicate column rules.
8. Conflicting duplicates preserve unique values and retain conflicting values as tokenized evidence.
9. Invalid-primary-key rows and worksheets without usable primary key column are skipped and reported KO.
