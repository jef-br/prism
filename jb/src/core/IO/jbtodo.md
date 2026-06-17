# IO Module Todo

## Fetch strategy stubs

- [ ] Implement Fetch_HTTPS_DirectFile.cs.
  - File: `jb/src/core/IO/Fetchers/Fetch_HTTPS_DirectFile.cs` — empty (1 line).
  - Block: No ticket assigned yet. The import stage accepts direct HTTPS file URLs but the concrete fetcher strategy is not implemented.
  - Estimated feasibility: **High**. Standard `HttpClient` download with streaming to a temp path. `HostRules.json` policy (allowed schemes, blocked hosts, redirect limits, timeout) is already defined and loaded. The only non-trivial part is correctly applying all redirect and timeout rules. Estimated effort: 1–2 days.
  - Fix: Implement `IFetchStrategy` to download a file from a direct HTTPS URL, validate against `HostRules.json` policy, stream to `%TEMP%/prism/{jobID}/`, and return an `ImageRecord_INPUT`. Create a ticket before starting.

- [ ] Implement Fetch_DropBox.cs.
  - File: `jb/src/core/IO/Fetchers/Fetch_DropBox.cs` — empty (1 line).
  - Block: No ticket assigned yet. DropBox URL support is deferred; not required for V1.
  - Estimated feasibility: **Medium**. Public shared links (`dropbox.com/s/...?dl=0`) can be normalized to a direct download URL by changing the query parameter to `?dl=1` and delegating to `Fetch_HTTPS_DirectFile`. Private links require OAuth2 and the Dropbox API v2 (`/files/download`). If only public shared links are in scope, effort is < 1 day. OAuth-gated private links add 2–3 days and a dependency on secure credential storage.
  - Fix: When DropBox support is prioritized, decide scope (public-only vs. authenticated), implement URL normalization, and delegate the actual download to `Fetch_HTTPS_DirectFile`. Create a scoped ticket first.

- [ ] Implement Fetch_WeTransfer.cs.
  - File: `jb/src/core/IO/Fetchers/Fetch_WeTransfer.cs` — empty (1 line).
  - Block: No ticket assigned yet. WeTransfer URL support is deferred; not required for V1.
  - Estimated feasibility: **Low**. WeTransfer has no public download API for anonymous links. Resolving a download URL requires either (a) scraping the WeTransfer HTML download page — fragile, breaks when WeTransfer changes markup, and violates their ToS in most interpretations — or (b) the WeTransfer Business API, which requires a paid partner account and API key. Neither path is straightforward. Estimated effort: 3–5 days for the scraping approach, with high maintenance risk.
  - Fix: Defer until WeTransfer Business API access is available. Do not implement a scraping-based solution without explicit acceptance of the fragility and ToS risk.

## Spec deviations

- [ ] ExcelConfig.json is located at job-run time, not at server startup — deviates from fail-fast spec.
  - File: `jb/src/core/Pipeline/StageShells.cs` `ImportStageShell.Run`.
  - Spec says: `CLAUDE.md` requires `PrismApiConfiguration.Load()` to validate all config and model assets at startup. Missing or invalid config must fail fast and loud before any job is accepted.
  - Current behavior: `ExcelConfig.json` is discovered inside `ImportStageShell.Run`, which runs per-job at pipeline time. A missing `ExcelConfig.json` silently passes startup and only fails when the first import job reaches that stage.
  - Why it deviates: The stage shell was implemented before the startup config list in `Prism.cs.ValidateRequiredFolderLocalConfigs` was finalized. The ExcelConfig path was not added to that list.
  - Fix: Add `"Excel/ExcelConfig.json"` to the `requiredFolderLocalConfigs` array in `Prism.cs.ValidateRequiredFolderLocalConfigs`. Pass the pre-loaded `ExcelConfig` object into `ImportStageShell.Run` rather than discovering it per-job.

- [ ] Media kind triage uses extension-only detection — deviates from byte-header spec.
  - File: `jb/src/core/IO/Importer.cs` `DetectMediaKind()`.
  - Spec says: `PRISM-io-import.md` specifies "Media kind is triaged from bytes, not only from filename or MIME type."
  - Current behavior: `DetectMediaKind()` uses `Path.GetExtension(originalFileName)` for the triage decision. No byte-header probe is performed.
  - Why it deviates: Extension-based detection was the fast path implemented during T-400. Byte-header probing requires reading the first N bytes of each file before the media kind decision, which adds I/O per file and was deferred.
  - Fix: Read the first 16 bytes of the normalized or temp file and match against known magic-byte signatures (JPEG: `FF D8 FF`; PNG: `89 50 4E 47`; WebP: `52 49 46 46...57 45 42 50`). Use the extension as a secondary hint only when the byte header is ambiguous or absent.

## Spec deviations (continued)

- [ ] SD-7: `BatchManifest` missing required fields from `PRISM-models.md`.
  - File: `jb/src/core/IO/BatchManifest.cs`.
  - Spec says: `PRISM-models.md` lists the following required manifest fields that are currently absent:
    - `ClientRequestToken` — optional caller-provided token that must be echoed back unchanged.
    - KO groups / `KoGroups` — grouped KO details with safe reason descriptions.
    - Effective configuration snapshot — a safe summary of the configuration active for this job.
    - Output-format metadata — output format, content types, filenames, byte counts.
    - Transient diagnostic reference — optional link to a diagnostic snapshot for the job.
  - Current behavior: `BatchManifest.cs` has `ImageRows`, `Summary`, and job ID. None of the five fields above are present.
  - Fix: Add the absent fields to `BatchManifest`. Populate `ClientRequestToken` from `PrismJobRequest`. Populate `KoGroups` by grouping KO `ImageRows` by reason code. Populate the config snapshot from `PrismConfiguration`. Populate output-format metadata in `Exporter`.
  - Answer:

- [ ] SD-14: KO zip entry path uses `InitialFullName` which may contain directory separators.
  - File: `jb/src/core/IO/Exporter.cs` line ~101: `AddBytesEntry(zip, $"KO/{lambda.InitialFullName}", ...)`.
  - Spec says: `PRISM-api.md` specifies `KO/` contains "normalized JPG artifacts." A KO entry path should be a safe flat filename, not a potentially path-unsafe original name.
  - Current behavior: `lambda.InitialFullName` may contain `/`, `\`, or other path-unsafe characters if the original input was a zip member or path-based input. This would produce a malformed or nested zip entry.
  - Fix: Sanitize the KO filename before using it as a zip entry name — strip directory separators and replace unsafe characters. Use `Path.GetFileName(lambda.InitialFullName)` as a minimum.
  - Answer:

## Missing implementations (not spec deviations)

- [ ] Directory input has no production implementation in Importer.
  - File: `jb/src/core/IO/Importer.cs`.
  - Issue: `PRISM-io-import.md` requires support for local folder scanning with recursive descent and a byte-size recursion guard. No `ScanDirectory` or equivalent method exists.
  - Fix: Implement `ScanDirectory` with `SearchOption.AllDirectories`, filtering by accepted extensions and size limits from `ImportConfig`. Add a recursion depth guard and a total-file-count limit.
