# IO Module Todo

## Fetch strategy stubs


- [ ] Implement Fetch_WeTransfer.cs.
  - File: `jb/src/core/IO/Fetchers/Fetch_WeTransfer.cs` — empty (1 line).
  - Block: No ticket assigned yet. WeTransfer URL support is deferred; not required for V1.
  - Estimated feasibility: **Low**. WeTransfer has no public download API for anonymous links. Resolving a download URL requires either (a) scraping the WeTransfer HTML download page — fragile, breaks when WeTransfer changes markup, and violates their ToS in most interpretations — or (b) the WeTransfer Business API, which requires a paid partner account and API key. Neither path is straightforward. Estimated effort: 3–5 days for the scraping approach, with high maintenance risk.
  - Fix: Defer until WeTransfer Business API access is available. Do not implement a scraping-based solution without explicit acceptance of the fragility and ToS risk.

## Spec deviations

- [ ] Media kind triage uses extension-only detection — deviates from byte-header spec.
  - File: `jb/src/core/IO/Importer.cs` `DetectMediaKind()`.
  - Spec says: `PRISM-io-import.md` specifies "Media kind is triaged from bytes, not only from filename or MIME type."
  - Current behavior: `DetectMediaKind()` uses `Path.GetExtension(originalFileName)` for the triage decision. No byte-header probe is performed.
  - Why it deviates: Extension-based detection was the fast path implemented during T-400. Byte-header probing requires reading the first N bytes of each file before the media kind decision, which adds I/O per file and was deferred.
  - Fix: Read the first 16 bytes of the normalized or temp file and match against known magic-byte signatures (JPEG: `FF D8 FF`; PNG: `89 50 4E 47`; WebP: `52 49 46 46...57 45 42 50`). Use the extension as a secondary hint only when the byte header is ambiguous or absent.

- [ ] SD-7: `BatchManifest` missing required fields from `PRISM-models.md`.
  - File: `jb/src/core/IO/BatchManifest.cs`.
  - `PRISM-models.md` lists the following required manifest fields that are currently absent:
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
