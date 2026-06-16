# PRISM — Workbench (Web & WPF)

## Allowed Differences Between Web and WPF

Web and WPF may differ **only** at input selection and transport:
- Web: sends uploads and URLs through the API.
- WPF: may pass local file, folder, stream, Excel, and zip input descriptors directly to `Prism.Process`.
- Web: receives progress through API SSE transport.
- WPF: may subscribe directly to shared core progress event stream.
- Both: may only expose jobs started by that same client/session.
- This parity is explicit for the subscription design.

**Must NOT differ:**
- `PrismJobRequest` meaning
- `PrismProcessingParameters` availability (in one UI location, binary parameters grouped)
- Validation semantics
- Definitive route order
- KO grouping
- Manifest interpretation
- Evidence display semantics
- Output preview semantics


---

## Shared Behavior — Both Workbenches Must Show

- Per-image route from `ImageRecord_LAMBDA` in definitive order: Imported → Classified → Matched → Ordered → Renamed → Generated → Transformed → Exported
- Excel model summary
- Image collection and source/import state
- Bounded matching evidence
- Classification summaries
- Ordering and rename decisions
- Generation state
- Transformation summaries
- KO records and safe failure reasons
- Output preview
- The same `PrismProcessingParameters` controls in one job-parameter location, with binary parameters grouped

---

## No-Hidden-Behavior Rule

Workbench views must display PRISM-owned route, evidence, status, score, and KO data from `ImageRecord_LAMBDA`, `MatchEvidence`, `ImageTransformationResult`, and `BatchManifest` **without replacing those facts with UI-only interpretations**.

Rules:
- Label displayed values by source route stage.
- Render raw reason codes, scores, thresholds, statuses, and safe messages when available.
- Allow friendly UI text only as an **additional display layer**.
- Keep manifest-backed diagnostics traceable to the source stage or manifest row.
- Do **not** hide failed stages, KO reasons, rejected evidence summaries, or route states because they are inconvenient for presentation.

---

## Diagnostic Display

Route-based; uses `ImageRecord_LAMBDA` and `BatchManifest`.

Both workbenches show bounded per-image route diagnostics for: Imported, Classified, Matched, Ordered/Renamed, Generated, Transformed, and Exported output.

- `manifest.json` is the only retained diagnostic snapshot artifact.
- Normal summaries and retained diagnostics are organized per image inside the manifest.
- Workbenches must label displayed values by source stage and link back to manifest rows where applicable.
- There are no separate persisted diagnostic snapshot artifacts outside `manifest.json`.

---

## Web Workbench — Section Data Shapes

| Section | Data Expected |
|---|---|
| Uploader | Selected image/Excel/zip/URL sources, local validation state, `PrismProcessingParameters` |
| Excel model | Summary of accepted Excel inputs, FamilyID counts, skipped worksheets/rows, safe KO details |
| Image collection | `ImageRecord_INPUT` import state and `ImageRecord_LAMBDA` route state |
| Match results | Bounded `MatchEvidence` summaries and manifest-backed diagnostics when available |
| Classification/order/rename/generation/transform route | Per-stage summaries from `ImageRecord_LAMBDA` |
| Output preview | `ImageRecord_OUTPUT` metadata, final filenames, previewable output references, manifest row links |
| KO groups | Safe KO/failure fields from manifest projection |

---

## Web Workbench — Upload Component

- Enable `Start Prism Job` only after minimum accepted image source + Excel source criteria are met.
- Keep `Start Prism Job` disabled until at least one valid Excel source and one valid image source are present.
- When one valid Excel source and one valid image source are present, no currently allowed processing option combination is incompatible.
- Collect URL text separately from file drops.
- Collect images, Excel files, zip files, URL text, and all job parameters into the canonical request model.
- Keep job parameters in **one UI location**, binary parameters grouped.
- Leave authoritative validation to the server.
- Treat a server validation error as making the affected source invalid, and show the safe reason from the API error payload.
- Do not start the job until upload submission is complete and URL/zip inputs have been pushed to backend.

---

## Web Workbench — Drag-and-Drop Errors

- Dropped items that are not supported input candidates are not submitted.
- Excel, zip, and media validation messages are grouped by category, not repeated per input item.
- Invalid URL and remote fetch validation shows safe per-URL detail.
- Authoritative server rejections use the documented pre-core API error payload fields for visible UI states.
- Excel rejection message: "Excel file is corrupt, damaged, or password protected."
- Zip rejection messages include "Zip file is too big" and "Zip file is corrupt, damaged, or password protected."
- Unsupported media message: "Only jpg/jpeg, png, tif/tiff, pdf, webp, bmp, and gif are supported."
- Oversized media message: "Image(s) that are too big are ignored. Max size = <value from config>."

---

## Web Workbench — API Client Behavior

- Use one typed API client layer for web submission, progress, result retrieval, downloads, and pre-core API errors.
- Submit canonical multipart requests to `POST /PRISM/process` with the `request` JSON part, repeated `input` file parts, URL input entries, and the complete `PrismProcessingParameters` payload.
- Treat the job-start envelope as submission acknowledgment only; it provides `JobID`, `progressUrl`, `resultUrl`, and initial status, not completed manifest data.
- Track progress through the returned SSE `progressUrl`.
- Fetch completed or failed job output only through the returned `resultUrl` after the progress stream reports a terminal state.
- For `format="zip"`, handle the response as a binary zip download that contains `manifest.json`.
- For `format="json"`, handle the response as JSON and read completed `BatchManifest` data from the `manifest` field.
- Map pre-core API error payloads to visible upload or job-start error states before any manifest is available.

---

## Web Workbench — Progress Visualization

Renders definitive route order from live SSE progress events and `ImageRecord_LAMBDA`.

Visible:
- Stage name
- Current item when available
- Completed count and total count when known
- Severity
- Safe message
- Per-image route state when available

Rules:
- SSE is live-only and does not replay missed events to late subscribers or reconnecting clients.
- The client may subscribe only to jobs it started.
- `Queued` and `Running` are job-status events around the definitive route and do not replace route-stage names.
- After terminal completion or failure, the live progress stream ends and the retained result/manifest remains available until `Prism_Config.json -> Jobs.JobRetentionPeriodInHours` expires.
- After retention expiry, the `JobID` is stale and should be removed from local client state.

---

## Web Workbench — Next.js Layout

- Keep route files thin
- Isolate feature sections
- Keep reusable UI primitives in predictable shared locations
- Keep styles in predictable folders
- Design tokens: `PRISM-theme.css`
- Reusable layout/state classes: one workbench CSS file; component-specific styles near their components; all colors/fonts in `PRISM-theme.css`

---

## WPF Workbench

- Renders definitive route order from shared core progress events and `ImageRecord_LAMBDA`.
- Shows same progress fields and evidence groupings as web workbench.
- Shows route-based manifest diagnostics for each stage.
- WPF does not keep unbounded image histories in memory and uses `manifest.json` as the retained diagnostic record.

## WPF Workbench — Project Layout

- Use the same feature-oriented organization as `jb/src/workbench/web` where the concepts match.
- Use WPF-native folder names where appropriate: `Views`, `ViewModels`, `Controls`, `Services`, and `Styles`.
- Keep the Prism core adapter isolated in `Services`.
- Keep route, evidence, diagnostics, and result display code aligned with shared workbench behavior instead of creating WPF-only interpretations.

**Local file selection supports:**
- Local image files
- Local folders
- Local Excel files
- Local zip files
- Memory-backed streams

WPF converts selected items into the same structured input meaning used by `PrismJobRequest`.

**WPF direct invocation rules:**
- Passes local file, folder, stream, Excel, and zip input descriptors directly instead of wrapping as API upload objects.
- Exposes all `PrismProcessingParameters` in one job-request UI location, binary parameters grouped.
- Receives the same `PrismJobResult` as API callers.
- Subscribes to the shared progress event stream (not WPF-only progress stages).
- May expose only jobs started by that WPF client/session.
- Treats `Queued` and `Running` as job-status events around the definitive route, not as route stages.
- Must preserve API/workbench parity for validation semantics, stage order, KO grouping, manifest interpretation, diagnostics, and output preview.
