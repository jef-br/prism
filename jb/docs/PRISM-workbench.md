# PRISM — Workbench (Web & WPF)
*Abbreviations: `GLOSSARY.md`*

## Allowed Differences Between Web and WPF

- **Web only:** sends uploads/URLs via API; receives progress via SSE; limited to jobs started by that client/session.
- **WPF only:** passes local file/folder/stream/Excel/zip descriptors directly to `Prism.Process`; subscribes directly to shared core PPE stream.

**Must NOT differ:** PJR meaning, PPP availability (one UI location, binary params grouped), validation semantics, definitive route order, KO grouping, BM interpretation, evidence display, output preview.

---

## Shared Behavior — Both Must Show

Per-image route (Imported → … → Exported), Excel model summary, image collection/import state, bounded matching/classification evidence, ordering/rename decisions, generation state, transformation summaries, KO records, output preview, PPP in one location (binary params grouped).

---

## No-Hidden-Behavior Rule

Display PRISM-owned route, evidence, status, score, and KO data from IRL, ME, ITR, and BM **without replacing facts with UI-only interpretations**.
- Label displayed values by source route stage.
- Render raw reason codes, scores, thresholds, statuses, and safe messages when available.
- Friendly UI text: additional display layer only.
- Manifest-backed diagnostics must be traceable to source stage or manifest row.
- Do NOT hide failed stages, KO reasons, rejected evidence summaries, or route states.

---

## Diagnostic Display

Route-based, using IRL and BM. `manifest.json` is the only retained diagnostic snapshot artifact. Workbenches label displayed values by source stage and link to manifest rows.

---

## Web — Section Data Shapes

| Section | Data expected |
|---|---|
| Uploader | Selected sources, local validation state, PPP |
| Excel model | Accepted Excel summary, FID counts, skipped worksheets/rows, safe KO details |
| Image collection | IRI import state + IRL route state |
| Match results | Bounded ME summaries + manifest-backed diagnostics |
| Classification/order/rename/generation/transform | Per-stage summaries from IRL |
| Output preview | IRO metadata, final filenames, previewable refs, manifest row links |
| KO groups | Safe KO/failure fields from BM projection |

---

## Web — Upload Component

- Enable `Start Prism Job` only after ≥1 valid Excel source + ≥1 valid image source.
- Keep disabled until both conditions met.
- When met, no currently allowed PPP combination is incompatible.
- Collect URL text separately from file drops.
- Keep job params in **one UI location**, binary params grouped.
- Leave authoritative validation to server.
- Server validation error → show safe reason from API error payload; treat source as invalid.
- Do not start job until upload submission complete and URL/zip inputs pushed to backend.

---

## Web — Drag-and-Drop Errors

- Unsupported items not submitted.
- Validation messages grouped by category, not repeated per item.
- Invalid URL/remote fetch shows safe per-URL detail.
- API error payload fields used for visible UI states.
- Excel rejection: "Excel file is corrupt, damaged, or password protected."
- Zip rejections: "Zip file is too big" / "Zip file is corrupt, damaged, or password protected."
- Unsupported media: "Only jpg/jpeg, png, tif/tiff, pdf, webp, bmp, and gif are supported."
- Oversized media: "Image(s) that are too big are ignored. Max size = \<value from config>."

---

## Web — API Client Behavior

- One typed API client layer for submission, progress, result retrieval, downloads, and pre-core API errors.
- Submit multipart to `POST /PRISM/process`: `request` JSON part, repeated `input` file parts, URL input entries, complete PPP payload.
- Job-start envelope = submission acknowledgment only (`JobID`, `progressUrl`, `resultUrl`, initial status — not completed BM data).
- Track progress via returned SSE `progressUrl`.
- Fetch output only via `resultUrl` after progress stream reports terminal state.
- `format="zip"` → binary zip download containing `manifest.json`.
- `format="json"` → JSON; read completed BM from `manifest` field.
- Map pre-core API error payloads to visible upload/job-start error states before any BM is available.

---

## Web — Progress Visualization

Live SSE from IRL. Shows: stage name, current item, counts, severity, safe message, per-image route state.
- SSE is live-only; no replay for late subscribers.
- Client subscribes only to its own jobs.
- `Queued`/`Running` = job-status events around the route, not route-stage names.
- After terminal state: stream ends; result/BM available until `Jobs.JobRetentionPeriodInHours` expires.
- After expiry: `JobID` stale; remove from client state.

---

## Web — Next.js Layout

- Route files thin; feature sections isolated.
- Reusable UI primitives in predictable shared locations.
- Design tokens: `PRISM-theme.css`. Reusable layout/state classes: one workbench CSS file. Component-specific styles near components. All colors/fonts in `PRISM-theme.css`.

---

## WPF Workbench

- Renders definitive route order from shared core PPE and IRL.
- Shows same progress fields and evidence groupings as web.
- Shows route-based BM diagnostics per stage.
- Does not keep unbounded image histories in memory; uses `manifest.json` as retained diagnostic record.

**Project layout:** Feature-oriented like `jb/src/workbench/web`. WPF-native folders: `Views`, `ViewModels`, `Controls`, `Services`, `Styles`. Core adapter isolated in `Services`.

**Local file selection:** local image files, local folders, local Excel, local zips, memory-backed streams.

**Direct invocation rules:**
- Passes local file/folder/stream/Excel/zip descriptors directly (not as API upload objects).
- Exposes all PPP in one job-request UI location, binary params grouped.
- Receives same PJRes as API callers.
- Subscribes to shared PPE stream (not WPF-only progress).
- Exposes only jobs started by that WPF session.
- `Queued`/`Running` = job-status events around definitive route.
- Must preserve parity: validation semantics, stage order, KO grouping, BM interpretation, diagnostics, output preview.
