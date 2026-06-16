# IO Module Todo

- [ ] Implement Exporter.cs.
  - File: `jb/src/core/IO/Exporter.cs` — currently 11 lines of comments only; no class, no method bodies.
  - Block: Ticket T-1100 (Exported Stage) is the owner of this work. T-1100 is blocked by T-1000 (Transformed Stage).
  - Fix: When T-1100 is activated, implement the two export modes described in the file header:
    A. ZIP: all renamed images + `manifest.json`.
    B. JSON: manifest object with `images.ok[]`, `images.ko[]`, and optional `originalImages`.
    Wire into `ExportStageShell.Run()` in `Pipeline/StageShells.cs`.

- [ ] Implement Fetch_HTTPS_DirectFile.cs.
  - File: `jb/src/core/IO/Fetchers/Fetch_HTTPS_DirectFile.cs` — empty (1 line).
  - Block: No ticket assigned yet. The import stage accepts direct HTTPS file URLs (see `PRISM-io-import.md`) but the concrete fetcher strategy is not implemented. The importer works today because URL inputs are handled elsewhere; this class is a placeholder for the dedicated fetch strategy.
  - Fix: Implement the `IFetchStrategy` (or equivalent interface) to download a file from a direct HTTPS URL, validate against `HostRules.json` policy, stream to a temp path, and return an `ImageRecord_INPUT`. Create a ticket before starting — ensure `HostRules.json` redirect/timeout/blocked-host rules are applied.

- [ ] Implement Fetch_DropBox.cs.
  - File: `jb/src/core/IO/Fetchers/Fetch_DropBox.cs` — empty (1 line).
  - Block: No ticket assigned yet. DropBox URL support is a deferred fetch strategy; not required for V1.
  - Fix: When DropBox support is prioritized, implement URL normalization (shared links → direct download), then delegate to the HTTPS direct-file fetcher logic. Create a scoped ticket first.

- [ ] Implement Fetch_WeTransfer.cs.
  - File: `jb/src/core/IO/Fetchers/Fetch_WeTransfer.cs` — empty (1 line).
  - Block: No ticket assigned yet. WeTransfer URL support is a deferred fetch strategy; not required for V1.
  - Fix: When WeTransfer support is prioritized, implement link scraping or API integration to resolve a download URL, then delegate to the HTTPS direct-file fetcher. Create a scoped ticket first.

- [ ] ExcelConfig.json is located at job-run time in ImportStageShell, not at server startup.
  - File: `jb/src/core/Pipeline/StageShells.cs` ImportStageShell.Run.
  - Issue: CLAUDE.md spec requires PrismApiConfiguration.Load() to validate all config and model assets at startup. ExcelConfig.json is a required folder-local config that should fail fast on missing/invalid at startup, not silently succeed until the first job attempts import.
  - Fix: Move ExcelConfig.json discovery and validation into PrismApiConfiguration.Load() or equivalent startup path. Pass the validated ExcelConfig object to ImportStageShell rather than discovering it per-job.

- [ ] Media kind triage uses extension-only detection, not byte-header triage.
  - File: `jb/src/core/IO/Importer.cs` DetectMediaKind().
  - Issue: PRISM-io-import.md specifies "Media kind is triaged from bytes, not only from filename or MIME type." The current implementation uses Path.GetExtension(originalFileName) for the triage decision. No byte-header probe is performed.
  - Fix: Add a byte-header or magic-byte probe step before accepting a file. The file should be identified from its content bytes, with the extension serving only as a secondary hint.

- [ ] Directory input has no production implementation in Importer.
  - File: `jb/src/core/IO/Importer.cs`.
  - Issue: PRISM-io-import.md requires support for local folder scanning with recursive descent and a byte-size recursion guard. No ScanDirectory or equivalent method exists in Importer.cs.
  - Fix: Implement ScanDirectory in Importer with SearchOption.AllDirectories, filtering by extension and size limits per config.
