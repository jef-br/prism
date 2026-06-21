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
