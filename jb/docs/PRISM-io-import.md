# PRISM — IO & Import
*Abbreviations: `GLOSSARY.md`*

## `Importer.cs` Responsibilities

- First checks: existence, size, extension before opening any file.
- Normalizes accepted input paths → two collections: image collection + Excel collection.
- Format-specific work via media-specific import strategies.
- Converts supported external image formats, PDFs, and TIFF pages → flat JPG (job temp folder → image collection).
- Applies EXIF orientation during normalization.
- Failed validation → skipped and logged to `manifest.json`.

---

## Co-Deployment Contract (Core, decided 2026-07-15)

The job temp folder is the **artifact bus** between core stages. Ingress, Matching, and Export are **always co-deployed on one physical system** sharing one process filesystem — never split across machines (see `PRISM-overview.md` "Core vs. Features"). Consequences:

- `IngestResult` carries `NormalizedJpgPath` as an **absolute local path**, not bytes. This is deliberate: the Matching HTTP contract (`HttpMatchingService` → `Prism.ServiceHost PRISM_SERVICE=matching`) is only valid against a host that reads the same filesystem that Ingest wrote. There is no ship-bytes-over-the-wire variant and none is planned.
- A Matching host that cannot read the job temp folder fails loud: `MatchingService.MatchAsync` throws with an explicit co-deployment message instead of KO-ing every image with misleading per-image decode errors.
- The **public services** (Matching / Transform / Generate / Upscale) are the only ones that may run out-of-process via `Prism.ServiceHost` (`PRISM_SERVICE=matching|generate|transform|upscale`); a Matching host is additionally valid only on the same filesystem Ingest wrote (the guard above). **Ingest is never a service** — media enters PRISM exclusively through in-process ingress, which is also what attaches context (Excel/IEM) to media for the downstream services (T-3300, 2026-07-15).

### Import→Match Handoff: Disk Is the Contract (closed 2026-07-15, T-3500)

The proposed in-process fusion (carry normalized JPEG bytes or the decoded image from Import into Matching to skip the re-read at `MatchingService.PrepareLambda`) was **measured and rejected**. SPACINI29 (86 source JPEGs, ~486 MB, ~5.7 MB each), full pipeline, job wall 156.5 s:

- Re-reading the normalized files: **1.8 s summed** (~1.2% even counted serially; the reads run 8-wide in `Parallel.For`, so real wall impact is well under 0.5%) — this is all a bytes-carry could save.
- Decoding them: **21.3 s summed CPU** (~2–3 s wall under the same parallelism) — all a decoded-image carry could save, at ~16 MB per image of unbounded RAM (batch-sized spike, multiplied by concurrent jobs) plus pixel drift vs. the JPEG on disk.

Neither saving justifies an unbounded Import→Match memory spike. `NormalizedJpgPath` on the job temp folder **is** the Import→Match handoff — do not re-propose in-memory carry without new evidence that decode/read time has become a dominant cost.

---

## Input Handling

**Local Path:** `Importer.cs` performs first checks. Only inputs that pass enter normalization.

**Stream:** Memory-backed streams enter as input descriptors with source metadata, stream reference, and explicit ownership. If descriptor says Importer owns the stream, Importer disposes after normalization or KO; else caller remains responsible.

**Multipart (API):** API upload parts converted before pipeline entry into descriptors containing: original filename, content type, byte length, source kind, and either stream ref or job-temp-file ref. API performs edge validation first.

**Directory:** Local folders may be scanned recursively. Recursion stops for any folder whose total byte size is below `Input.Images.filesize.min`. Every file still validated individually against configured file size, extension, request size, and batch image count limits. Full member path preserved in the input descriptor as source metadata — path segments are available for deduplication and matching downstream. Implementation: `ScanDirectory` in `Importer.cs` with `SearchOption.AllDirectories`, filtered by accepted extensions and size limits from `ImportConfig`, with a recursion depth guard and total-file-count limit.

**Link (Remote URL):** Fetched before pipeline entry → temporary input descriptors → handled like local files by `Importer.cs`.

---

## Remote Fetcher Strategies

Implement from the start:
- `jb/src/core/IO/Fetchers/Fetch_HTTPS_DirectFile.cs` — generic direct HTTP/HTTPS file URLs
- `jb/src/core/IO/Fetchers/Fetch_DropBox.cs`
- `jb/src/core/IO/Fetchers/Fetch_WeTransfer.cs`

URL policy from HCFG:
```json
{
  "allowedSchemes": ["http", "https"],
  "blockedSchemes": ["ftp"],
  "blockedHostPatterns": ["reddit.com", "*.reddit.com"],
  "redirects": { "allowGenericDirectFileRedirects": false, "allowFetcherOwnedRedirects": true },
  "networkRanges": { "allowPrivate": true, "allowLinkLocal": true, "allowLoopback": false, "rejectAnyLoopbackDnsResult": true },
  "timeouts": { "connectSeconds": 10, "responseHeaderSeconds": 15, "idleReadSeconds": 15, "totalFetchSeconds": 120 },
  "testing": { "allowLocalhost": false }
}
```

**URL validation order:**
1. Parse as absolute URI
2. Validate scheme vs HCFG
3. Normalize/validate host vs HCFG
4. Resolve DNS for loopback/private-network classification
5. Reject literal loopback + any loopback DNS result (except explicit localhost test mode)
6. Allow private-network/link-local/internal after scheme/host/loopback checks pass
7. Select fetcher route
8. Apply fetcher-specific redirect policy
9. Enforce `Content-Length` policy before reading when required
10. Enforce observed-byte caps while streaming
11. Enforce timeout caps
12. Convert accepted downloads → temp input descriptors for `Importer.cs`

> Private/link-local/internal IP ranges **deliberately allowed** — PRISM input media may live on PRISM-owned local servers. Explicit SSRF exception. Loopback remains rejected.

---

## Flat JPG Conversion

- Transparent pixels → `#ffffff` when flattened.
- EXIF orientation applied during import (image correct-side-up before downstream stages).
- No EXIF → keep original orientation.
- Normalized JPG written with default orientation — no EXIF orientation field in IRI, IRL, manifest, or journey payload.

### Parallel Normalization

- Both image loops (direct records and zip-member images) normalize via `Parallel.ForEach`, capped at `Environment.ProcessorCount`, so peak concurrent decodes never exceed the machine's core count regardless of batch size.
- Result accumulation (`normalizedImages`, `imageKoRecords`) uses `ConcurrentBag<T>`, since order carries no meaning downstream — `Exporter` and matching correlate records by `InitialFullName`, never by list position.
- The normalized filename's uniqueness index comes from a job-scoped `Interlocked` counter, not list length, so filenames stay collision-free under concurrent completion.
- Excel/IEM construction (`BuildFamilyRecords`) stays sequential; it runs after both image loops complete and `ModelBuilder` is not thread-safe.

### Fast-Path Already-Conforming JPEGs

- Before decoding, `Importer` checks the source via `Image.Identify` (metadata-only, no full pixel decode): if the format is already JPEG and the EXIF orientation tag is absent or `1`/`TopLeft` (i.e. `AutoOrient` would be a no-op), the source file is copied unchanged into the job's `normalized/` folder instead of being decoded and re-encoded.
- Baseline JPEG has no alpha channel by definition, so "no alpha channel" is automatically satisfied whenever the fast path's format check passes — no separate check needed.
- `NormalizedJpgPath` always resolves to a file inside `jobTempFolder/normalized/` either way (decoded-and-encoded, or fast-path-copied) — the job-owned lifetime contract is unchanged.
- Any exception or non-conforming result during the fast-path check falls through to the existing full decode/composite/encode path; real corruption is still classified by the existing KO paths.

---

## Zip Handling

- Each job gets a temporary folder (cleaned up after output returned).
- Temp folder: spill-to-disk inputs, downloads, extracted zip members, normalized JPGs, diagnostic snapshots, output assembly.
- Non-image/non-Excel zip members → omitted silently (no record, no count, no manifest entry).
- Zip output parity: both zip and JSON output project from one canonical BM. Counts, per-item rows, OK/KO status, KO groups, source metadata, output filenames, config snapshot, safe diagnostics must be identical.
- Exporters use reserved manifest-backed output paths. Collision → KO state and safe collision evidence must be identical in zip and JSON projections.

### Zip Member KO Reasons

| Condition | Stage | Reason | Manifest group |
|---|---|---|---|
| Unextractable processable member | `zip-extract` | `corrupt-zip-member` | `corrupt images` |
| Member fails decode/normalization | import stage | `500` / `541` | `corrupt images` |
| Encrypted archive or entry | `zip-extract` | `password-protected` | `password protected zip` |

Each KO entry includes archive name/path + member path/original filename when available. PRISM does not prompt for passwords.

**Zip layout:** OK and KO folder names are `OK` and `KO`. Manifest is always `manifest.json`. Not configurable via `ZipLayout.json`.

---

## Original Image Export Policy

- Original input bytes **never included by default**.
- Included in PJRes only when `PPP.ReturnOriginalImages = true`.
- `ReturnOriginalImages=true` affects result payload only, not `manifest.json`.

---

## Corrupt Image KO Reasons

| Code | Condition |
|---|---|
| `500` | Damaged file; could not be opened or fully decoded |
| `500` | Corrupt file where part of image is missing |
| `541` | Conversion failure |

Safe description added for client; internals not disclosed.

---

## Media Kind Triage

Media kind triaged from **bytes**, not only filename or MIME type. PDF and TIFF pages rendered per import rules. Supported image/document → flat JPG. Accepted Excel → Excel collection.

Implementation: read the first 16 bytes of the file and match against known magic-byte signatures (JPEG: `FF D8 FF`; PNG: `89 50 4E 47`; WebP: `52 49 46 46 ... 57 45 42 50`). Extension used as secondary hint only when byte header is ambiguous or absent.
