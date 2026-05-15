# API Todo

- [ ] Define request model for `POST /PRISM/process`: write the exact query parameters and multipart body parts the API accepts before it calls Prism core.
  - Impact:
    - Project progress: High - The request model is the API boundary that every client and importer path depends on.
    - Effect on other TODOs: Blocks - It gates multipart field names, validation, URL handling, progress correlation, and workbench upload behavior.
  - Industry standard:
    Large media APIs define a stable ingestion contract before implementation, including file parts, metadata, options, limits, and correlation identifiers, so clients can retry idempotently and operators can reproduce failed jobs.
  - Recommended solution:
    Define one explicit request shape with multipart parts for images, Excel files, zip files, URL lists, and one structured `PrismProcessingParameters` options part for all job parameters, including `ReturnOriginalImages`.
  - Answer:

- [ ] Define multipart field names for `POST /PRISM/process`: name the form fields for images, Excel files, zip files, URLs, and processing options so every frontend sends the same shape.
  - Impact:
    - Project progress: High - Stable field names prevent web, WPF, and external clients from sending incompatible uploads.
    - Effect on other TODOs: Unblocks - It feeds importer descriptors, upload component behavior, request size validation, and API client behavior.
  - Industry standard:
    Batch upload endpoints use predictable plural field names and avoid client-specific variants, because ingestion workers need to validate counts, sizes, and media classes before starting expensive processing.
  - Recommended solution:
    Use `images`, `excelFiles`, `zipFiles`, `urls`, and `prismProcessingParameters` as the canonical multipart field names, with all clients sending the same names even if some are empty.
  - Answer:

- [ ] Define API progress streaming behavior: choose how clients receive stage progress while a long batch is processing.
  - Impact:
    - Project progress: High - Progress streaming is central for long-running image batches and workbench observability.
    - Effect on other TODOs: Unblocks - It connects pipeline progress event shape to web/WPF progress displays and diagnostic snapshots.
  - Industry standard:
    Long-running background jobs expose progress through polling, server-sent events, web sockets, or reply queues, with correlation IDs and monotonic stage updates rather than tying user interfaces to synchronous request lifetimes.
  - Recommended solution:
    Use a job ID plus progress endpoint or SSE stream for web clients, and let WPF subscribe directly to core events when running in-process.
  - Answer:

- [ ] Define response model for zip output: say whether the HTTP response is a raw zip stream, what headers it uses, and where `manifest.json` is located.
  - Impact:
    - Project progress: High - Zip output is the primary artifact contract for processed batches.
    - Effect on other TODOs: Blocks - It affects zip layout, manifest parity, output filename rules, and workbench download handling.
  - Industry standard:
    Batch media processors return archive artifacts with deterministic layout, explicit content headers, and an embedded manifest so results can be consumed without relying on side channels.
  - Recommended solution:
    Return a raw `application/zip` stream with a deterministic filename and include `manifest.json` at the archive root beside configured OK/KO folders.
  - Answer:

- [ ] Define response model for JSON output: list the top-level JSON fields returned when `format=json` is requested.
  - Impact:
    - Project progress: High - JSON output defines the machine-readable alternative to zip export.
    - Effect on other TODOs: Blocks - It must align with output image records, manifest projection, MIME metadata, and JSON export property names.
  - Industry standard:
    JSON batch exports separate summary, item rows, binary payload metadata, and errors so clients can stream, page, or inspect results without losing provenance.
  - Recommended solution:
    Define top-level fields for `batchId`, `summary`, `images`, `ko`, `manifest`, and `diagnostics`, with image bytes encoded only for processed outputs when requested.
  - Answer:

- [ ] Define error payload model: choose the JSON fields used when the API rejects a request before Prism core runs.
  - Impact:
    - Project progress: High - Pre-core rejection must be explicit so invalid requests never enter expensive processing silently.
    - Effect on other TODOs: Unblocks - It aligns request size validation, URL validation, workbench error states, and client retry behavior.
  - Industry standard:
    Public APIs return structured error envelopes with stable codes, messages, invalid fields, correlation IDs, and safe details that avoid exposing internal paths or infrastructure.
  - Recommended solution:
    Use an error payload with `code`, `message`, `details`, `fieldErrors`, `correlationId`, and `retryable`.
  - Answer:

- [ ] Define pre-pipeline external URL validation: say which URL schemes and hosts are allowed before imported media enters Prism core.
  - Impact:
    - Project progress: High - URL policy controls security, import reliability, and whether external resources can enter the pipeline safely.
    - Effect on other TODOs: Unblocks - It feeds link import handling, remote strategies, drag-and-drop errors, and user-file failure policy.
  - Industry standard:
    Media aggregators validate URL schemes, redirects, host allow/deny rules, content length, content type, and timeout behavior before downloading data into job storage.
  - Recommended solution:
    Allow only `https` by default, reject private-network and unsupported hosts, enforce configured size/time limits, and convert accepted URLs into temporary input descriptors before core processing.
  - Answer:

- [ ] Define configured request size validation: say how the API calculates total request size and compares it to `Prism_Config.json`.
  - Impact:
    - Project progress: High - Size validation protects memory, disk, and worker capacity before the job is accepted.
    - Effect on other TODOs: Unblocks - It supports multipart handling, upload validation states, Importer limits, and batch KO policy.
  - Industry standard:
    Large batch systems enforce limits at the edge using declared and observed byte counts, then repeat per-item validation inside ingestion so oversized or deceptive payloads are caught early.
  - Recommended solution:
    Compare the sum of multipart content lengths, downloaded URL content lengths, and expanded zip limits against `Input.MAXIMUM_REQUEST_SIZE`, then validate each item again in Importer.
  - Answer:

- [ ] Define health response model: list what `GET /PRISM/health` reports about config, model files, disk space, and pipeline availability.
  - Impact:
    - Project progress: Medium - Health reporting improves operations but depends on core lifecycle and resource ownership decisions.
    - Effect on other TODOs: Influences - It reflects config loading, ONNX model ownership, disk spill policy, and failure policy.
  - Industry standard:
    Production batch services expose readiness separately from liveness and include dependency checks for configuration, model assets, disk capacity, and downstream services.
  - Recommended solution:
    Return readiness fields for config validity, required model assets, temp disk availability, supported runtime providers, and whether processing can currently accept jobs.
  - Answer:

- [ ] Define config response model: list which runtime config values `GET /PRISM/config` exposes to workbench and other clients.
  - Impact:
    - Project progress: Medium - The config endpoint helps clients validate locally, but it should follow core config ownership.
    - Effect on other TODOs: Influences - It informs upload validation states, workbench UI limits, and API request validation.
  - Industry standard:
    Client-visible config endpoints expose only safe operational limits and display metadata, not secrets, internal paths, model implementation details, or security policy internals.
  - Recommended solution:
    Expose accepted media types, max file size, max request size, max image count, output formats, and visible feature flags while hiding local paths and private provider settings.
  - Answer:
