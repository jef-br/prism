# PRISM — Pipeline Core Architecture

## Definitive Stage Order

```
Imported > Classified > Matched > Ordered > Renamed > Generated > Transformed > Exported
```

1. **Imported** — Import media, parse Excel into IEM, unpack zips, normalize accepted image media into JPG representations.
2. **Classified** — Deduplicate images by visual hash (highest res wins), then apply image classification/labeling to every canonical image.
3. **Matched** — Tokenize each image using all matchers, compare against IEM, resolve to one FamilyID above threshold.
4. **Ordered** — Per FamilyID, order all images using filename tokens and classification labels. Matching and ordering may repeat until no unmatched candidates remain or no new matches are made in a pass.
5. **Renamed** — Collapse FamilyID + order probability into the final filename.
6. **Generated** — For families with low image count, copy hero image and create alternative generated version (when enabled and source quality sufficient).
7. **Transformed** — Transform each image using rules under `jb/src/core/Images/Transform`, guided by per-image `ImageNGP` config.
8. **Exported** — Return all output images with `manifest.json`.

**Stage rules:**
- Stage order is definitive and chronological — never reordered.
- A stage emits OK records, KO records, warnings, diagnostics, and progress events.
- User-file KO records do not stop the job when valid work remains.
- PRISM-owned failures stop the job as `Failed`.
- There is no cancellation stage. Accepted jobs run to natural completion.

---

## `Prism.cs` — Facade Rules

`Prism.cs` is the outward-facing facade. It must read like a story — chronological, management-only code that calls classes that do the real work.

- Receives caller input and the PRISM-owned internal `JobID`
- Builds or completes `PrismJobRequest`
- Passes structured job input to `Pipeline.cs`
- Receives structured output from the processing/exporter flow
- Returns the processed result to the requester
- Handles cleanup **only** by calling explicitly named helper classes (`JobCleaner.cs`, `JobErrorHandling.cs`, or equivalent)
- Contains **no** cleanup or error logic inline

Any frontend connects to `Prism.cs` via API or direct call.

---

## `Pipeline.cs` — Processing Ownership

`Pipeline.cs` owns processing and disposal of pipeline resources. It receives structured input from `Prism.cs` and returns structured output through the exporter flow.

It uses:
- Excel Modeling: collates all worksheets into the IEM, deduplicated and sorted by FamilyID
- `FilenameTokenizer.cs` (`jb/src/core/Pipeline/FilenameTokenizer.cs`)
- `ImageMatcher.cs` (`jb/src/core/Images/ImageMatcher.cs`) — loads matcher classes via strategy pattern
- `ImageOrderer.cs` — orders images per FamilyID
- Image transformation
- Generation logic
- Export

---

## `PrismJobRequest`

C# PODO. Structured job contract passed into `Prism.Process`. Represents one logical client-requested job after all inputs have been normalized into PRISM input records.

**Required fields:**
- `Guid JobID` — PRISM-owned internal job ID. External caller-provided IDs must not become PRISM's internal job ID.
- `string? ClientRequestToken` — optional caller-provided token, echoed back only for correlation. Never used as PRISM's job ID.
- `IReadOnlyList<ImageRecord_INPUT> ImageRecords`
- `IReadOnlyList<InputExcelFileRecord> ExcelRecords`
- `IReadOnlyList<InputZipFileRecord> ZipFileRecords`
- `PrismProcessingParameters PrismProcessingParameters` — output format, transform toggle, generation toggle, diagnostics settings, `ReturnOriginalImages`

**Rules:**
- Must not expose raw frontend upload objects, API-specific request types, WPF-specific objects, or platform-specific link objects.
- `Prism.Process` rejects a job before pipeline if: no accepted image records, no accepted Excel records, missing `PrismProcessingParameters`, or invalid input record structure.
- User-file failures discovered during import/stream/URL/zip handling remain attached to relevant input records for inclusion in the final manifest.
- Processing is never cancelled by request option. Every accepted request is handled start to finish.

---

## `PrismJobResult`

C# PODO. Client-facing result returned by `Prism.Process` for one requested PRISM job.

**Required content:**
- `Guid JobID` — PRISM's internal job ID
- `string? ClientRequestToken` — echoed back unchanged when supplied
- Job status: completed / completed with KO records / failed
- Output image records and/or exported artifacts per `PrismProcessingParameters`
- `BatchManifest`
- KO records (import, Excel, classification, matching, ordering, renaming, generation, transformation, export, cleanup)
- Diagnostics: safe stage summaries, warnings, optional diagnostic artifact references
- Export metadata: output format, filenames/artifact references, content types, byte counts
- Original image data only when `PrismProcessingParameters.ReturnOriginalImages` is true

**Rules:**
- Original input bytes are excluded by default and never placed in `manifest.json`.
- The manifest is the audit contract; byte-heavy payloads and exported artifacts stay in result/export-specific fields.

---

## Failure Policies

### User-File KO Policy (continue the job)

Continue and record KO for:
- Unsupported standalone media files
- Corrupt, unreadable, damaged, partially decoded, or conversion-failed images/documents
- Bad zip members (unextractable, undecoded, unnormalized, unclassified)
- Excel rows with missing/malformed/non-config-compliant primary key values
- Excel worksheets with no usable primary key column
- Images that cannot be matched to an acceptable FamilyID
- Images that cannot be generated or transformed into acceptable output

KO records stay attached to the relevant file/zip member/worksheet/row/image record. If no valid work remains after KO handling, the job ends naturally with KO records.
Bad zip member KO records keep archive/member provenance and do not collapse healthy archive contents.

### PRISM-Owned Failure Policy (stop the job as `Failed`)

Stop for:
- Missing, unreadable, or invalid `Prism_Config.json`
- Missing, unreadable, or invalid required folder-local `..._config.json` files
- Missing, unreadable, invalid, or incompatible required model files
- Invalid internal settings, schemas, thresholds, tensor names, export settings, or configured limits
- Unavailable required job/temp/output storage or cleanup-critical infrastructure
- Exporter failure that prevents returning the requested output format

These failures are not converted into per-image KO records. PRISM-owned failures should be detected before expensive work whenever possible.

---

## Configuration Lifecycle

- `Prism.cs` builds the PRISM configuration object **on server startup**.
- Configuration loads `Prism_Config.json` and all required folder-local `..._config.json` files.
- Configuration is validated before `Prism.Process` starts pipeline execution.
- V1 queue settings (`MaxQueuedJobs`, `MaxConcurrentJobs`) are loaded before PRISM accepts jobs.
- Missing or invalid PRISM-owned configuration fails fast and marks the job as failed.
- PRISM does not read mutable configuration mid-stage.
- Each job uses the effective configuration that was valid when the job was accepted.
- The effective configuration snapshot (or safe summary) is available for manifest and diagnostics.

---

## V1 Job Queue

- Single-server in-process bounded job queue.
- `POST /PRISM/process` validates, creates PRISM-owned `JobID`, creates job record, and enqueues.
- Queue is conceptually a bounded .NET `Channel<T>` consumed by a fixed number of background workers.
- Queue carries job references and metadata only (`JobID`, config snapshot reference, job folder reference, requested output format) — **not** image/Excel/zip bytes.
- When queue is full: reject before job creation with a pre-core API error. No `manifest.json` produced for queue-full rejection.
- Queued/running jobs are process-local in V1. Restart recovery not guaranteed.
- RabbitMQ not used in V1 (future option only if durable queue recovery or distributed workers are needed).

---

## Source Tree Ownership

| Folder | Owns |
|---|---|
| `jb/src/core` | Pipeline behavior, model contracts, image processing, import/export, zip, runtime config |
| `jb/src/api` | HTTP contracts, request/response models, API validation, health/config endpoints, progress transport |
| `jb/src/workbench` | Shared UI/workbench behavior across web and WPF |
| `jb/src/workbench/web` | Browser-specific upload, API client, layout, progress, validation |
| `jb/src/workbench/wpf` | Desktop-specific file selection, direct core invocation, WPF parity |
| `jb\docs` | Established accepted project knowledge |
| `jbtodo.md` (folder-local) | Temporary working notes for unresolved or pending decisions |
