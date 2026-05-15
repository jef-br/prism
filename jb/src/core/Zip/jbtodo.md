# Zip Todo

- [ ] Define zip output parity with JSON output: say which manifest fields must be identical between zip and JSON exports.
  - Impact:
    - Project progress: High - Export parity keeps downstream clients from seeing different truth depending on output format.
    - Effect on other TODOs: Blocks - It gates JSON export fields, zip layout, manifest projection, and API response models.
  - Industry standard:
    Batch exporters keep one canonical manifest model and project it into each delivery format so audit, retry, and support behavior remain consistent.
  - Recommended solution:
    Use one `BatchManifest` projection for both zip and JSON, with identical summary counts, item rows, KO groups, reasons, and config snapshot.
  - Answer:

- [ ] Define duplicate filename handling in output zip folders: say how Prism avoids overwriting files with the same final name.
  - Impact:
    - Project progress: High - Filename collision policy prevents silent data loss in the output archive.
    - Effect on other TODOs: Unblocks - It affects output filename collision handling, `_det` suffix assignment, and manifest row projection.
  - Industry standard:
    Archive writers treat output paths as unique keys and resolve collisions deterministically while recording the final path in the manifest.
  - Recommended solution:
    Generate unique final paths before writing zip entries, prefer deterministic `_det` ordering, and add a collision suffix only as a last resort with manifest evidence.
  - Answer:

- [ ] Define KO entries for corrupt zip members: list what manifest reason is used when a member cannot be extracted or decoded.
  - Impact:
    - Project progress: High - Corrupt members are common user-data failures and must not collapse healthy archive content.
    - Effect on other TODOs: Unblocks - It aligns with corrupt image KO reasons, user-file failure policy, and manifest KO groups.
  - Industry standard:
    Zip ingestion records member-level failures with archive and entry provenance while continuing extractable entries when the archive structure permits it.
  - Recommended solution:
    Emit a KO record with archive name, member path, stage `zip-extract` or `decode`, and a safe corrupt-member reason code.
  - Answer:

- [ ] Define KO entries for password-protected zip members: choose the manifest reason for encrypted archives and encrypted entries.
  - Impact:
    - Project progress: High - Encrypted archives cannot be processed without credentials and need clear user feedback.
    - Effect on other TODOs: Unblocks - It supports API/workbench errors, zip import policy, and KO reason modeling.
  - Industry standard:
    Pipelines reject encrypted payloads unless credential handling is explicitly supported, and report them as user-fixable KO entries.
  - Recommended solution:
    Treat encrypted archives or entries as KO with a `password-protected` reason and do not prompt for passwords in the core pipeline.
  - Answer:

- [ ] Define KO entries for ignored zip members: say whether ignored non-media files appear in `manifest.json` or are omitted completely.
  - Impact:
    - Project progress: Medium - Ignored-member policy affects manifest noise and support expectations.
    - Effect on other TODOs: Influences - It ties into accepted media types, user-file KO policy, and zip output parity.
  - Industry standard:
    Ingestion systems distinguish unsupported-but-harmless members from failed media records, often summarizing ignored files instead of making them full failures.
  - Recommended solution:
    Omit harmless non-media members from per-image KO rows but include ignored counts and optionally file names in a manifest diagnostics section.
  - Answer:

- [ ] Define zip layout folder configurability: say whether `OK`, `KO`, and `manifest.json` can change through `ZipLayout.json`.
  - Impact:
    - Project progress: Medium - Layout configurability affects consumers but should not alter manifest semantics.
    - Effect on other TODOs: Influences - It affects zip response model, output parity, and folder-local config placement.
  - Industry standard:
    Export layouts can be configurable when external consumers require it, but canonical manifest names and meaning should remain stable.
  - Recommended solution:
    Allow OK/KO folder names to be configured through `ZipLayout.json`, but keep `manifest.json` at the archive root unless a versioned export contract changes.
  - Answer:
