# PRISM — API Contracts
*Abbreviations: `GLOSSARY.md`*

## `POST /PRISM/process`

Accepts `multipart/form-data`.

**Parts:**
- `request` — JSON request model (see shape below)
- `input` — repeated per uploaded file (image, `.xlsx`, or zip — client does not classify type)

**Request JSON shape:**
```json
{
  "ClientRequestToken": "abc-123",
  "rename": true,
  "transform": true,
  "generation": true,
  "format": "zip",
  "ReturnOriginalImages": false,
  "allowEsrganUpscale": false,
  "Input": [
    "img1.jpg",
    "products.zip",
    "sheet.xlsx",
    "https://example.com/archive.zip",
    "https://dropbox.com/...",
    "https://wetransfer.com/..."
  ]
}
```

- Processing options in JSON, not query parameters.
- `format` values: `"zip"` and `"json"`.
- `allowEsrganUpscale` (T-4930) — **omitted means false**. False upscales undersized images with plain Lanczos
  (capped at 1.33×); true uses Real-ESRGAN (capped at 1.42×). Both aim at the same final-output size bar, so
  the flag trades processing time for detail recovery, not for output dimensions. See
  `PRISM-transform-generate.md` → "Unified upscale".
- `request.Input` contains remote input strings (HTTP links, Dropbox, WeTransfer, cloud links).
- Clients do not classify inputs as image/Excel/zip — PRISM triages by accepted media type.

**Minimum:** ≥1 accepted image representation + ≥1 accepted `.xlsx` after import.

**Before `Prism.Process`:** API ingress + `Importer.cs` resolve/download/open all inputs, validate, convert to descriptors, then build PJR. PJR must not expose raw multipart objects, API types, or platform link objects.

---

## Submission Response (Job Start Envelope)

`POST /PRISM/process` returns quickly with:
- `JobID`, `ClientRequestToken` (when supplied), `progressUrl`, `resultUrl`
- Initial status: `Queued`

Client persists returned `JobID` values locally and expires on same retention window as server.

---

## Progress — SSE

`GET /PRISM/jobs/{JobID}/progress` — SSE stream for web clients. Primary transport; no polling; no WebSockets.
Only the client that started the job may access this endpoint.

**Each PPE includes:** `JobID`, route stage name, current item (when available), completed/total counts (when known), severity, safe message, timestamp.

Queue/running/completion/failure job-status events may appear around route-stage events. Events are **monotonic** for one job. SSE is live-only — no replay for late subscribers or reconnects. After terminal state: endpoint no longer acts as replay source.

---

## Result Retrieval

`GET /PRISM/jobs/{JobID}/result`

Call after terminal state from progress stream. Only the job's originating client may access.
Result available until CFG `Jobs.JobRetentionPeriodInHours` expires. After: `JobID` stale.

### Zip Output (`format="zip"`)

Returns `application/zip`. No extra headers (`X-Prism-JobID` etc.).

**Contents:**
- `manifest.json` at archive root
- `OK/` — all OK renamed/ordered/transformed output images
- `KO/` — normalized JPGs for images that imported OK but became KO later (decode failures appear in manifest only); KO entry filename = original input filename; when that would conflict with another entry, use sanitized `InitialFullName`
- First `.xlsx` with first accepted FID column (original filename kept)

### JSON Output (`format="json"`)

Returns `application/json`.

**Top-level fields:**
- `manifest` — canonical BM (summary, OK/KO images, KO groups, route summaries, safe diagnostics, export metadata)
- `images` — `{ ok[], ko[] }` per-image journey entries
- `originalImages` — present only when `PPP.ReturnOriginalImages = true`

**Per journey item:**
```json
{ "sourceReference": "full/original/path.jpg", "lambda": { ... }, "output": { ... } }
```
`output` is `null` for KO items. Default JSON export does not embed image bytes.

No separate top-level `summary`, `ko`, or `diagnostics` fields — those belong inside `manifest`.

---

## `GET /PRISM/health`

Returns: processing acceptance status, jobs currently processing, jobs queued, configured `MaxQueuedJobs` + `MaxConcurrentJobs`, supported runtime providers, config/model/disk readiness fields.

---

## `GET /PRISM/config`

Returns: accepted media types, max file size, max request size, max image count, output formats, visible feature flags, safe params from any `..._config.json`. Hides: local paths and private provider settings.

---

## Pre-Core Error Payload

```json
{
  "correlationId": "1234567890",
  "code": "INVALID_PAYLOAD",
  "message": "Message that describes what is invalid.",
  "details": ["request.Input[0]=https://example.com/file.zip", "maxRequestBytes=2684354560"],
  "fieldErrors": ["request.Input[0]:CONTENT_LENGTH_REQUIRED"],
  "retryable": false
}
```

`correlationId` is always a string. `fieldErrors` shaped as `<fieldPath>:<VALIDATION_CODE>`.

**Field paths:** `request`, `request.Input`, `request.Input[0]`, `multipart.input[0]`, `format`, `rename`, `transform`, `generation`, `ReturnOriginalImages`

**Validation codes:**

| Code | Description |
|---|---|
| `INCOMPLETE_PAYLOAD` | Minimum image/zip/xlsx requirements not met (per CFG `Input`) |
| `CONTENT_LENGTH_REQUIRED` | First remote file requiring but lacking `Content-Length` |
| `REQUEST_TOO_LARGE` | Total request size + max from CFG |
| `REDIRECT_NOT_ALLOWED` | Safe message: "File a Fetcher support request by contacting Jef Bracke" |
| `UNSUPPORTED_URL` | Safe message: "File a URL support request by contacting Jef Bracke" |
| `FILE_TOO_LARGE` | One item exceeds its configured per-item limit |
| `FETCH_TIMEOUT` | Fetch exceeds connect, response-header, idle-read, or total-fetch timeout |
| `LOOPBACK_NOT_ALLOWED` | URL targets localhost, `127.0.0.0/8`, `::1`, or any DNS result containing loopback |

---

## Request Size Validation

**Per-item vs aggregate (separate checks):**
- `*.filesize.min` / `*.filesize.max` → each uploaded/downloaded item
- `Input.MAXIMUM_REQUEST_SIZE` → summed submitted + downloaded binary bytes
- `*.amount.min` / `*.amount.max` → accepted media counts

**Remote `Content-Length` policy:**
- Generic fetches require `Content-Length`.
- Dedicated `Fetch_` classes may have platform-specific behavior.
- All fetchers still enforce observed-byte caps while reading.

**Zip rule:**
- Compressed zip bytes: count against `Input.MAXIMUM_REQUEST_SIZE` and `CFG.Input.ZIP.filesize.max`.
- Expanded zip bytes: do **not** count against `Input.MAXIMUM_REQUEST_SIZE`.
- Normalized image bytes: do **not** count.

**Failure behavior:**
- Request-level failure (cannot satisfy configured minimums) → stop job creation, return pre-core error. No `manifest.json`.
- Item-level failures handled when enough valid input remains.
- Second validation in `Importer.cs` after zip decompression + import normalization.
- If remaining valid input no longer satisfies minimums → stop the job.

---

## URL Validation (Pre-Pipeline)

See full rules and HCFG shape in `PRISM-io-import.md`.

---

## Decision Log

**SD-13 (resolved):** `images.ok[]/ko[]` now use `ImageJourneyItem { sourceReference, lambda (bounded stage journey), output }` instead of the flat `ManifestImageRow` projection. Implemented via `ToImageJourneyItem()` in `Exporter.cs`; journey items are carried through `ExportArtifacts.JourneyItems` to `PrismJobResult.OkImages`/`KoImages` and surfaced in `PrismJsonImagesEnvelope`.
