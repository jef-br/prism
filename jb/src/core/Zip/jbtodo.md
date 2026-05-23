# Zip Todo

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

- [ ] Define zip layout folder configurability: say whether `OK`, `KO`, and `manifest.json` can change through `ZipLayout.json`.
  - Impact:
    - Project progress: Medium - Layout configurability affects consumers but should not alter manifest semantics.
    - Effect on other TODOs: Influences - It affects zip response model, output parity, and folder-local config placement.
  - Industry standard:
    Export layouts can be configurable when external consumers require it, but canonical manifest names and meaning should remain stable.
  - Recommended solution:
    Allow OK/KO folder names to be configured through `ZipLayout.json`, but keep `manifest.json` at the archive root unless a versioned export contract changes.
  - Answer:
