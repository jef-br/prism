# Images Todo

- [ ] Define filename token metadata: say how tokens remember their source filename, position, type, and original text.
  - Impact:
    - Project progress: High - Token metadata is the foundation of explainable filename matching and ordering.
    - Effect on other TODOs: Blocks - It feeds numeric matching, string matching, ordering hints, and matcher evidence retention.
  - Industry standard:
    Data matching systems keep token provenance, offsets, normalized value, and token type so scores can be audited and tuned.
  - Recommended solution:
    Store token ID, source filename, start/end position, original text, normalized text, token type, and parser confidence.
  - Answer:

- [ ] Define output filename and export path collision handling: say what happens when two images want the same final filename or zip/JSON output path.
  - Impact:
    - Project progress: High - Collision handling prevents overwrites, silent data loss, and inconsistent exports.
    - Effect on other TODOs: Blocks - It affects suffix assignment, zip duplicate filename handling, JSON names, output filenames, and manifest rows.
  - Industry standard:
    Exporters reserve final artifact names before writing and resolve conflicts deterministically with manifest evidence.
  - Recommended solution:
    Resolve collisions during order/rename by assigning deterministic suffixes, then reject or uniquely disambiguate any remaining collision before export. Zip entries and JSON output names must use the resolved final paths recorded in the manifest.
  - Answer:

- [ ] Define forbidden filesystem character handling: say how invalid filename characters are removed or replaced.
  - Impact:
    - Project progress: Medium - Sanitization prevents invalid archive entries and cross-platform file issues.
    - Effect on other TODOs: Influences - It affects output filename rules, zip export, JSON names, and collision handling.
  - Industry standard:
    Export systems sanitize filenames using a deterministic allowlist and keep the original name separately for provenance.
  - Recommended solution:
    Normalize final filenames to a conservative ASCII-safe allowlist, replace invalid characters with `_`, and record original filenames separately.
  - Answer:
