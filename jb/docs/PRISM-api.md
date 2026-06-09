# PRISM — API Contracts

## `POST /PRISM/process`

Accepts `multipart/form-data`.

**Multipart parts:**
- `request` — JSON request model (see shape below)
- `input` — repeated for every uploaded file (image, `.xlsx`, or zip — client does not classify type)

**Request JSON shape:**
```json
{
  "ClientRequestToken": "abc-123",
  "rename": true,
  "transform": true,
  "generation": true,
  "format": "zip",
  "ReturnOriginalImages": false,
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

- Processing options are in the JSON request model, not query parameters.
- Accepted `format` values: `"zip"` and `"json"`.
- `request.Input` contains remote input strings (HTTP links, Dropbox, WeTransfer, cloud platform links).
- Clients do not classify inputs as image, Excel, or zip — PRISM/API ingress triages by accepted media type.

**Minimum requirements:** At least one accepted image representation and one accepted `.xlsx` Excel file must be present after import (from uploads, remote resources, or zip contents).

**Before calling `Prism.Process`:** API ingress and `Importer.cs` resolve/download/open all inputs, validate them, convert to importer descriptors, then build `PrismJobRequest`. `PrismJobRequest` must not expose raw multipart objects, API-specific types, WPF-specific objects, or platform-specific link objects.

---

## Submission Response (Job Start Envelope)

`POST /PRISM/process` returns quickly with:
- `JobID`
- `ClientRequestToken` (when supplied)
- `progressUrl`
- `resultUrl`
- Initial job status: `Queued`
- The client persists returned `JobID` values locally and expires them on the same retention window as the server.

---

## Progress — Server-Sent Events

`GET /PRISM/jobs/{JobID}/progress` — SSE stream for web clients.

SSE is the **primary** web progress transport. Polling is not primary. WebSockets not used.
Only the client that started the job may access this endpoint.

**Each SSE progress event includes:**
- `JobID`
- Route stage name from the definitive route: `Imported`, `Classified`, `Matched`, `Ordered`, `Renamed`, `Generated`, `Transformed`, `Exported`
- Current item when available
- Completed count and total count when known
- Severity
- Safe message
- Timestamp

Queue, running, completion, and failure job-status events may appear around route-stage progress events.
Events are **monotonic** for one job and never invent API-only progress stages.
SSE is live-only: late subscribers and reconnecting clients do not receive replayed historical events.
After terminal completion or failure, this endpoint no longer acts as a replay source.

**WPF:** Does not use API progress transport. Subscribes directly to the shared core progress event stream.

---

## Result Retrieval

`GET /PRISM/jobs/{JobID}/result`

Called after the job reaches a completed or failed final state. The progress stream sends completion/failure status but does not carry the full output.
Only the client that started the job may access this endpoint.
The result remains available until `Prism_Config.json -> Jobs.JobRetentionPeriodInHours` expires. After that, the `JobID` is stale and should be treated as unknown.

### Zip Output (`format="zip"`)

Returns raw `application/zip` stream with normal download headers only.
- No `X-Prism-JobID` or `X-Prism-ClientRequestToken` headers.

**Zip contents:**
- `manifest.json` at archive root
- `OK/` — all OK renamed, ordered, transformed output images
- `KO/` — normalized JPG artifacts for images that imported successfully but became KO later (images that cannot be decoded/imported appear in `manifest.json` KO entries only)
- The full first `.xlsx` file whose workbook contained the first accepted `familyID` column (keeps original filename)

### JSON Output (`format="json"`)

Returns `application/json`.

**Top-level fields:**
- `manifest` — canonical `BatchManifest` (summary, OK/KO images, KO groups, route summaries, safe diagnostics, export metadata)
- `images` — grouped per-image journey entries
- `originalImages` — optional, present only when `ReturnOriginalImages=true`

`images` contains:
- `ok[]` — images with exportable OK output
- `ko[]` — images that became KO, preserving bounded pipeline journey

Each journey item:
```json
{
  "sourceReference": "full/original/path/or/url/to/image.jpg",
  "lambda": { ... },
  "output": { ... }
}
```
- `output` is `null` for KO items.
- Default JSON export does not embed image bytes.

No separate top-level `summary`, `ko`, or `diagnostics` fields — those belong inside `manifest`.
`manifest.json` is the only retained diagnostic snapshot artifact.

---

## `GET /PRISM/health`

Returns generic "Prism Health OK" plus:
- Whether processing can currently accept jobs
- Number of jobs currently being processed
- Number of queued jobs
- Configured `MaxQueuedJobs`
- Configured `MaxConcurrentJobs`
- Supported runtime providers
- Config validity readiness fields
- Required model assets readiness
- Temp disk availability

---

## `GET /PRISM/config`

Exposes: accepted media types, max file size, max request size, max image count, output formats, visible feature flags, and any parameter safe to share from any `..._config.json` file in the repo.

Hides: local paths and private provider settings.

---

## Pre-Core Error Payload

```json
{
  "correlationId": "1234567890",
  "code": "INVALID_PAYLOAD",
  "message": "Message that describes what is invalid.",
  "details": [
    "request.Input[0]=https://example.com/file.zip",
    "maxRequestBytes=2684354560"
  ],
  "fieldErrors": [
    "request.Input[0]:CONTENT_LENGTH_REQUIRED"
  ],
  "retryable": false
}
```

`correlationId` is always a string (even if numeric-looking). `fieldErrors` entries shaped as `<fieldPath>:<VALIDATION_CODE>`.

**Field paths:** `request`, `request.Input`, `request.Input[0]`, `multipart.input[0]`, `format`, `rename`, `transform`, `generation`, `ReturnOriginalImages`

**Validation codes:**

| Code | Description |
|---|---|
| `INCOMPLETE_PAYLOAD` | Minimum image, zip, and xlsx requirements not met (per `Prism_Config.Input`) |
| `CONTENT_LENGTH_REQUIRED` | List only the first remote file for which `Content-Length` was required but not provided |
| `REQUEST_TOO_LARGE` | List total request size and max from `Prism_Config.json` |
| `REDIRECT_NOT_ALLOWED` | Safe message: "File a Fetcher support request by contacting Jef Bracke" |
| `UNSUPPORTED_URL` | Safe message: "File a URL support request by contacting Jef Bracke" |
| `FILE_TOO_LARGE` | One item exceeds its configured per-item limit |
| `FETCH_TIMEOUT` | Fetch exceeds configured connect, response-header, idle-read, or total-fetch timeout |
| `LOOPBACK_NOT_ALLOWED` | URL targets localhost, `127.0.0.0/8`, `::1`, or any DNS result containing a loopback address |

---

## Request Size Validation

**Per-item checks and aggregate checks are separate:**
- `*.filesize.min` / `*.filesize.max` → each uploaded, downloaded, image, zip, or Excel item
- `Input.MAXIMUM_REQUEST_SIZE` → summed submitted and downloaded binary bytes
- `*.amount.min` / `*.amount.max` → accepted media counts, not individual files

**Remote `Content-Length` policy:**
- Generic remote fetches require `Content-Length`.
- Dedicated `Fetch_` classes may have platform-specific behavior.
- Every fetcher still enforces observed-byte caps while reading.

**Zip rule:**
- Compressed zip bytes: may not exceed `Input.MAXIMUM_REQUEST_SIZE` or `Prism_Config.Input.ZIP.filesize.max`.
- Expanded zip bytes: do **not** count against `Input.MAXIMUM_REQUEST_SIZE`.
- Normalized image bytes: do **not** count against `Input.MAXIMUM_REQUEST_SIZE`.

**Failure behavior:**
- Request-level failure (cannot satisfy configured minimums) → stop job creation, return pre-core error payload.
- Item-level failures are ignored or handled when enough valid input remains.
- A second validation stage in `Importer.cs` happens after zip decompression and import normalization.
- When remaining valid input no longer satisfies configured minimums → stop the job.

---

## URL Validation (Pre-Pipeline)

See full URL validation rules and `HostRules.json` shape in `PRISM-io-import.md`.
