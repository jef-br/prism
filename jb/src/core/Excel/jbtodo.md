# Excel Todo

- [x] Define primary key config source: primary key rules come from `RecordPrimaryKey` and `FamilyIDProperties` in `ExcelConfig.json`.
  - Impact:
    - Project progress: High - Primary key config determines how product identity is found across supplier spreadsheets.
    - Effect on other TODOs: Blocks - It gates FamilyID ownership, duplicate records, invalid rows, matcher inputs, and output filename stems.
  - Industry standard:
    Data aggregators externalize source-specific identity rules while keeping the internal product identity contract stable and auditable.
  - Recommended solution:
    Keep FamilyID discovery rules in `ExcelConfig.json` and snapshot the effective rule set into the batch manifest.
  - Answer:

- [x] Define row ownership by FamilyID: every single data row belongs to one and only one FamilyID.
  - Impact:
    - Project progress: High - Row ownership is the core invariant of the product model.
    - Effect on other TODOs: Blocks - It underpins duplicate FamilyID handling, ProductRecord mapping, matcher candidates, and manifest rows.
  - Industry standard:
    Large catalog ingestion pipelines assign every accepted row to one canonical entity key so downstream joins, matching, and deduplication are deterministic.
  - Recommended solution:
    Preserve the one-row-to-one-FamilyID invariant and reject or KO rows whose identity cannot be resolved.
  - Answer:

- [x] Define duplicate FamilyID rule: duplicate FamilyID records cannot exist in the internal Excel model.
  - Impact:
    - Project progress: High - Duplicate entity prevention keeps matching and output naming deterministic.
    - Effect on other TODOs: Blocks - It affects duplicate row conflict handling, ProductRecord fields, matcher tie-breaking, and manifest evidence.
  - Industry standard:
    Aggregators collapse duplicate source records into one canonical entity while preserving conflict evidence for audit.
  - Recommended solution:
    Store exactly one ProductRecord per FamilyID and attach merged source values/conflicts as evidence.
  - Answer:

- [x] Define duplicate row conflict handling: deduplicate the entire row when all other cells in the involved records contain duplicate information.
  - Impact:
    - Project progress: High - Duplicate row handling controls how repeated supplier data becomes one product record.
    - Effect on other TODOs: Influences - It affects conflicting row handling, source value provenance, and manifest diagnostics.
  - Industry standard:
    Catalog pipelines collapse exact duplicate rows early to reduce noise, while keeping enough provenance to explain source counts.
  - Recommended solution:
    Deduplicate exact duplicate rows under the same FamilyID and record duplicate counts rather than emitting separate product records.
  - Answer:

- [x] Define conflicting duplicate row handling: when the same FamilyID appears in multiple rows, merge all non-empty data into one FamilyID record, preserve unique values, and keep conflicting values as tokenized evidence instead of overwriting them.
  - Impact:
    - Project progress: High - Conflict policy preserves supplier evidence without losing deterministic canonical records.
    - Effect on other TODOs: Unblocks - It supports ProductRecord fields, matcher evidence, workbench diagnostics, and manifest projection.
  - Industry standard:
    Data aggregators merge non-conflicting values and retain conflicts as provenance instead of overwriting silently, especially when source quality varies.
  - Recommended solution:
    Keep one canonical FamilyID record, merge unique normalized tokens, and expose conflicting original values as evidence.
  - Answer:

- [x] Define conflicting duplicate column handling: when duplicate columns disagree for the same FamilyID, tokenize both non-empty cell values, merge unique normalized tokens into the canonical property, and keep the original cell values as conflict evidence for manifest/workbench review.
  - Impact:
    - Project progress: High - Column conflict handling affects matcher quality and diagnostic transparency.
    - Effect on other TODOs: Influences - It feeds string matching, ProductRecord mapping, evidence retention, and workbench review.
  - Industry standard:
    Schema-flexible ingestion systems preserve both canonical normalized values and original conflicting source values for audit and explainability.
  - Recommended solution:
    Store merged normalized tokens for matching and keep original cell-level conflicts available to manifest and workbench.
  - Answer:

- [x] Define invalid primary key row handling: rows with missing, malformed, or non-config-compliant primary key values do not stop Excel parsing; skip the row and report it as KO in `manifest.json`.
  - Impact:
    - Project progress: High - Invalid row handling allows useful spreadsheet data to continue while preserving failures.
    - Effect on other TODOs: Unblocks - It aligns Excel parsing with user-file KO policy, BatchManifest, and workbench diagnostics.
  - Industry standard:
    Batch data pipelines isolate bad records into KO/dead-letter outputs and continue processing valid records when entity-level correctness is unaffected.
  - Recommended solution:
    Keep skipping invalid primary-key rows, emit KO rows with worksheet and row provenance, and continue parsing the workbook.
  - Answer:

- [x] Define missing primary key column handling: when a worksheet has no primary key column, skip that worksheet and report the worksheet as KO in `manifest.json`.
  - Impact:
    - Project progress: High - Missing identity columns make a worksheet unusable for matching.
    - Effect on other TODOs: Influences - It affects Excel model summary, KO groups, and health of matcher inputs.
  - Industry standard:
    Ingestion systems reject source partitions that lack required identity columns but continue other partitions from the same batch.
  - Recommended solution:
    Skip the worksheet, add a worksheet-level KO entry, and continue scanning other worksheets and files.
  - Answer:

- [x] Define canonical header source: use `HeaderRowIndicators` to find the header row before selecting canonical column names.
  - Impact:
    - Project progress: High - Header detection is required before any dynamic supplier spreadsheet can become a model.
    - Effect on other TODOs: Blocks - It drives canonical primary key header, column validity, duplicate detection, and ProductRecord mapping.
  - Industry standard:
    Flexible spreadsheet ingestion uses configurable header indicators and confidence scoring before mapping source columns to canonical fields.
  - Recommended solution:
    Keep `HeaderRowIndicators` as the header detection source and record the detected header row and confidence in diagnostics.
  - Answer:

- [x] Define canonical primary key header: when a detected header row contains a cell with edit distance 0 to `RecordPrimaryKey`, use that cell as the primary key column.
  - Impact:
    - Project progress: High - Exact primary key header selection prevents fuzzy identity mistakes.
    - Effect on other TODOs: Influences - It strengthens primary key rules, row ownership, and duplicate handling.
  - Industry standard:
    Entity identity columns should prefer exact configured matches over fuzzy matches because false positives propagate through every downstream join.
  - Recommended solution:
    Use exact `RecordPrimaryKey` matches as authoritative and only use configured alternate FamilyID properties when exact primary key is absent.
  - Answer:

- [x] Define required indicator count for header row detection: at least 50% of columns in a candidate header row must match configured indicators.
  - Impact:
    - Project progress: Medium - The threshold improves header detection confidence but follows the broader header source policy.
    - Effect on other TODOs: Influences - It affects missing header behavior, canonical column names, and Excel KO rates.
  - Industry standard:
    Spreadsheet parsers use configurable confidence thresholds to avoid treating arbitrary data rows as headers.
  - Recommended solution:
    Keep the 50% threshold and expose the score in diagnostics for failed or borderline worksheets.
  - Answer:

- [x] Define header indicator edit-distance cutoff: an edit distance greater than 12% means the cell does not qualify as an indicator match.
  - Impact:
    - Project progress: Medium - The cutoff controls fuzzy matching tolerance for supplier headers.
    - Effect on other TODOs: Influences - It affects header row detection, primary key selection, and worksheet KO decisions.
  - Industry standard:
    Fuzzy schema detection uses strict distance limits for identifiers so minor spelling variations are accepted without allowing unrelated headers.
  - Recommended solution:
    Keep the 12% cutoff and prefer exact or configured aliases for critical identity columns.
  - Answer:

- [ ] Define header indicator score for exact matches: say whether edit distance 0 counts as 100% confidence.
  - Impact:
    - Project progress: Medium - Exact score completes the header scoring scale and removes ambiguity.
    - Effect on other TODOs: Unblocks - It finalizes header detection scoring alongside edit-distance 1 and 2 rules.
  - Industry standard:
    Confidence models usually treat exact schema matches as full confidence while lower scores represent fuzzy evidence.
  - Recommended solution:
    Define edit distance 0 as 100% confidence.
  - Answer:

- [x] Define header indicator score for edit distance 1: a match with edit distance 1 counts as 75% confidence.
  - Impact:
    - Project progress: Medium - The score supports fuzzy header detection but does not define the full model alone.
    - Effect on other TODOs: Influences - It contributes to header row confidence and worksheet acceptance.
  - Industry standard:
    Minor spelling differences in supplier data are accepted with reduced confidence so the parser remains tolerant but explainable.
  - Recommended solution:
    Keep edit distance 1 at 75% and include the matched indicator in diagnostics.
  - Answer:

- [x] Define header indicator score for edit distance 2: a match with edit distance 2 counts as 50% confidence.
  - Impact:
    - Project progress: Medium - The score handles noisier headers while preserving a lower confidence signal.
    - Effect on other TODOs: Influences - It affects borderline header rows and downstream column mapping.
  - Industry standard:
    Lower-confidence fuzzy schema matches should be accepted only within explicit thresholds and remain auditable.
  - Recommended solution:
    Keep edit distance 2 at 50% and require the row-level threshold before accepting the header.
  - Answer:

- [x] Define column validity threshold: a column must contain non-null and non-empty values in at least 20% of its rows.
  - Impact:
    - Project progress: Medium - Column validity reduces noise in the internal model.
    - Effect on other TODOs: Influences - It affects empty column handling, ProductRecord properties, and matcher input quality.
  - Industry standard:
    Data ingestion pipelines filter sparse columns with configurable thresholds before using them for matching or analytics.
  - Recommended solution:
    Keep the 20% useful-value threshold and record dropped columns in diagnostics.
  - Answer:

- [x] Define empty column handling: drop columns that do not contain enough useful values.
  - Impact:
    - Project progress: Medium - Dropping empty columns keeps the model smaller and matching more precise.
    - Effect on other TODOs: Influences - It depends on column validity threshold and affects ProductRecord mapping.
  - Industry standard:
    Sparse or empty source columns are usually excluded from canonical models unless they are required schema fields.
  - Recommended solution:
    Drop invalid sparse columns after header detection and before duplicate column analysis.
  - Answer:

- [x] Define empty cell handling: fill empty cells with an empty string after deciding that the column itself is valid.
  - Impact:
    - Project progress: Medium - Empty cell normalization stabilizes model consumers and avoids null handling drift.
    - Effect on other TODOs: Influences - It affects ProductRecord fields, string matching, and conflict merging.
  - Industry standard:
    Pipelines normalize missing optional scalar values consistently after schema validation to simplify downstream transforms.
  - Recommended solution:
    Use empty strings for missing values in accepted columns while retaining provenance that the source cell was empty if diagnostics need it.
  - Answer:

- [x] Define duplicate column detection by header: identical headers make two columns duplicate candidates.
  - Impact:
    - Project progress: Medium - Header duplication is the first signal for column merge logic.
    - Effect on other TODOs: Influences - It feeds content comparison, fuzzy merge rules, and conflict handling.
  - Industry standard:
    Schema reconciliation treats identical source headers as candidate duplicates but verifies content before merging destructively.
  - Recommended solution:
    Mark identical headers as duplicate candidates and resolve them through content or conflict policy.
  - Answer:

- [x] Define duplicate column detection by content: content must be identical before two columns are considered direct duplicates.
  - Impact:
    - Project progress: Medium - Content equality prevents accidental loss from same-named but different columns.
    - Effect on other TODOs: Influences - It affects duplicate column merge and conflict evidence.
  - Industry standard:
    Data pipelines require content equality or explicit conflict rules before collapsing columns from messy sources.
  - Recommended solution:
    Treat identical content as direct duplicates and preserve conflicting content through the conflict path.
  - Answer:

- [x] Define fuzzy duplicate column merge rule: if headers differ but more than 20% of cells appear in both columns, merge and deduplicate the cells.
  - Impact:
    - Project progress: Medium - Fuzzy merge improves supplier tolerance but can affect matcher evidence.
    - Effect on other TODOs: Influences - It affects canonical properties, conflict records, and string matching.
  - Industry standard:
    Schema-flexible aggregators use overlap thresholds to identify semantically duplicate columns while retaining source evidence.
  - Recommended solution:
    Keep the 20% overlap merge rule and record the merge decision in model diagnostics.
  - Answer:

- [x] Define primary key numeric rule: a primary key cannot be accepted unless it matches the configured numeric requirement.
  - Impact:
    - Project progress: Medium - Numeric enforcement prevents false product identities.
    - Effect on other TODOs: Influences - It supports primary key validation, invalid row KO, and numeric matcher expectations.
  - Industry standard:
    Entity keys are validated against configured type constraints before being used for joins or matching.
  - Recommended solution:
    Enforce the configured numeric rule at Excel ingestion and expose invalid values as row-level KO entries.
  - Answer:

- [x] Define primary key length rule: a primary key cannot be accepted unless it is exactly 8 digits under the current config.
  - Impact:
    - Project progress: Medium - Length enforcement improves identity quality and matcher precision.
    - Effect on other TODOs: Influences - It affects invalid row handling, numeric token matching, and filename stem rules.
  - Industry standard:
    Fixed-format identifiers should be validated at ingestion so downstream matching can use stricter scoring.
  - Recommended solution:
    Keep the 8-digit rule as a config-driven constraint and avoid hard-coding it outside Excel/model validation.
  - Answer:

- [x] Define merged cell handling: only merge cells in the same column when their value is identical.
  - Impact:
    - Project progress: Low - Merged cells are important spreadsheet cleanup but do not define the core model contract alone.
    - Effect on other TODOs: Influences - It affects row value extraction and duplicate/conflict detection.
  - Industry standard:
    Spreadsheet parsers handle merged cells conservatively to avoid copying labels or values into unrelated rows.
  - Recommended solution:
    Keep same-column identical-value merging only and flag ambiguous merged cells in diagnostics if needed.
  - Answer:

- [x] Define worksheet provenance recording: do not keep provenance beyond processing because cleanup removes all temporary batch files.
  - Impact:
    - Project progress: Low - Provenance retention policy matters for audit but does not block parsing behavior.
    - Effect on other TODOs: Influences - It affects manifest fields, cleanup policy, and workbench diagnostics.
  - Industry standard:
    Batch processors keep enough temporary provenance to debug the job and enough manifest metadata to audit outputs without retaining source files indefinitely.
  - Recommended solution:
    Keep worksheet, row, and source filename provenance in memory and manifest-safe diagnostics, then delete temporary source files after processing.
  - Answer:
