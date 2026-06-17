# PRISM — Pipeline Core Architecture
*Abbreviations: `GLOSSARY.md`*

## Stage Order (definitive, immutable)

```
Imported → Classified → Matched → Ordered → Renamed → Generated → Transformed → Exported
```

1. **Imported** — Import media, parse Excel → IEM, unpack zips, normalize images → JPG.
2. **Classified** — Deduplicate by visual hash (highest res wins); apply classification to every canonical image.
3. **Matched** — Tokenize each image via all matchers; compare vs IEM; resolve to one FID above threshold.
4. **Ordered** — Per FID: order images using INGP qualification, DOR, filename hints, classification labels. Single pass.
5. **Renamed** — Collapse FID + order → final filename.
6. **Generated** — For low-image-count families: copy hero image, create alternative (when enabled + quality sufficient).
7. **Transformed** — Transform each image per INGP config and per-image IFs.
8. **Exported** — Return output images + `manifest.json`.

**Stage rules:**
- Stage emits: OK records, KO records, warnings, diagnostics, progress events.
- User-file KO → job continues when valid work remains.
- PRISM-owned failure → job stops as `Failed`.
- No cancellation stage. Accepted jobs run to natural completion.

---

## `Prism.cs` — Facade

Management-only code. Must read like a story — calls classes that do the real work.
- Receives input + PRISM-owned `JobID`, builds/completes PJR, passes to `Pipeline.cs`, returns PJRes.
- Cleanup: calls `JobCleaner.cs` / `JobErrorHandling.cs` only — no inline cleanup or error logic.

---

## `Pipeline.cs` — Processing Ownership

Receives structured input from `Prism.cs`; returns structured output through the exporter.

Uses: Excel → IEM via `ModelBuilder.cs`, `FilenameTokenizer.cs`, `ImageMatcher.cs` (strategy pattern), `ImageOrderer.cs`, image transformation, generation, export.

---

## PJR — `PrismJobRequest`

C# PODO. Structured job contract passed into `Prism.Process`. All inputs normalized into PRISM input records before construction.

**Required fields:**
- `Guid JobID` — PRISM-owned. Caller-provided IDs must not become PRISM's internal JobID.
- `string? ClientRequestToken` — optional, echoed only for correlation.
- `IReadOnlyList<IRI> ImageRecords`
- `IReadOnlyList<InputExcelFileRecord> ExcelRecords`
- `IReadOnlyList<InputZipFileRecord> ZipFileRecords`
- `PPP PrismProcessingParameters`

**Rules:**
- Must not expose raw frontend upload objects, API types, WPF objects, or platform link objects.
- `Prism.Process` rejects before pipeline if: no accepted image records, no accepted Excel records, missing PPP, or invalid input record structure.
- User-file failures during import remain attached to input records for inclusion in BM.
- No cancellation path. Every accepted request runs start to finish.

---

## PJRes — `PrismJobResult`

C# PODO. Client-facing result from `Prism.Process`.

**Required content:**
- `Guid JobID`, `string? ClientRequestToken` (echoed unchanged)
- Job status: completed / completed with KO / failed
- Output image records and/or exported artifacts per PPP
- BM, KO records (all stages), safe stage summaries, warnings, optional diagnostic refs
- Export metadata: format, filenames/artifact refs, content types, byte counts
- Original image data only when `PPP.ReturnOriginalImages = true`

**Rules:**
- Original bytes excluded by default; never in `manifest.json`.
- Manifest is the audit contract; byte-heavy payloads stay in result/export-specific fields.

---

## Failure Policies

**User-file KO (continue):** corrupt/unsupported/unreadable media, bad zip members, Excel rows with invalid PK, worksheets with no PK column, unmatched images, images that cannot be generated or transformed. KO records stay attached to the relevant record. Bad zip members keep archive provenance.

**PRISM-owned failure (stop as `Failed`):** missing/invalid CFG or any required folder-local `..._config.json`; missing/invalid/incompatible model files; invalid internal settings/schemas/thresholds; unavailable required storage; exporter failure. Not converted to per-image KO. FFAIL before expensive work whenever possible.

---

## Configuration Lifecycle

- Config built **on server startup**: loads CFG + all required folder-local `..._config.json` files.
- Validated before `Prism.Process` starts. V1 queue settings loaded before accepting jobs.
- Missing/invalid PRISM-owned config → FFAIL.
- No mutable config reads mid-stage. Each job uses the effective config at acceptance time.
- Effective config snapshot available for manifest and diagnostics.

---

## V1 Job Queue

- Single-server in-process bounded queue.
- `POST /PRISM/process` validates → creates PRISM-owned `JobID` → creates job record → enqueues.
- Queue = bounded .NET `Channel<T>` consumed by fixed background workers.
- Queue carries job references + metadata only (JobID, config snapshot ref, job folder ref, output format) — **not** image/Excel/zip bytes.
- Queue full → reject before job creation with pre-core API error. No `manifest.json` produced.
- Queued/running jobs are process-local in V1. Restart recovery not guaranteed.

---

## Source Tree Ownership

| Folder | Owns |
|---|---|
| `jb/src/core` | Pipeline behavior, model contracts, image processing, import/export, zip, runtime config |
| `jb/src/api` | HTTP contracts, request/response models, API validation, health/config endpoints, SSE |
| `jb/src/workbench/web` | Browser upload, API client, layout, progress, validation |
| `jb/src/workbench/wpf` | Desktop file selection, direct core invocation, WPF parity |
| `jb/docs` | Accepted project knowledge |
| `jbtodo.md` (folder-local) | Temporary working notes for unresolved/pending decisions |
