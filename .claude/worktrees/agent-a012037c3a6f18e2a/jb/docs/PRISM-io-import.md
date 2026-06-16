# PRISM — IO & Import

## `Importer.cs` Responsibilities

`Importer.cs` owns:
- First checks: existence, size, and extension before opening any file.
- Normalizing accepted input paths (folder or file, local or resolved remote) into two collections: image collection and Excel collection.
- Format-specific work via media-specific import strategies.
- Conversion of supported external image formats, PDFs, and TIFF pages into flat JPG artifacts (stored in job temp folder before adding to image collection).
- EXIF orientation application during normalization.

Paths that fail validation are skipped and logged to `manifest.json`.

---

## Input Handling

### Local Path Input
Accepted before the pipeline starts. `Importer.cs` performs first checks. Only inputs that pass enter normalization. Import strategy classes handle content type and origin-specific parsing, including strategies for remote paths and platform links (WeTransfer, Dropbox).

### Stream Input
Memory-backed streams enter Importer as input descriptors with source metadata, stream reference, and explicit ownership. If the descriptor says Importer owns the stream, Importer disposes it after normalization or KO handling; otherwise caller remains responsible.

### Multipart File Input (API)
API upload parts are converted before pipeline entry into importer input descriptors containing: original filename, content type, byte length, source kind, and either a stream reference or a job-temp-file reference. API performs edge validation first, then passes descriptors to Importer.

### Directory Input
Local folders may be scanned recursively. Recursion stops for any folder whose **total byte size is below `Input.Images.filesize.min`**. Every discovered file is still validated individually against configured file size, extension, request size, and batch image count limits.

### Link Input (Remote URLs)
Remote URLs are fetched before pipeline entry, converted into temporary input descriptors, then handled like local files by `Importer.cs`. Use a generic direct-URL import strategy by default.

---

## Remote Fetcher Strategies

Implement from the start:
- `jb/src/core/IO/Fetchers/Fetch_HTTPS_DirectFile.cs` — generic direct HTTP/HTTPS file URLs
- `jb/src/core/IO/Fetchers/Fetch_DropBox.cs`
- `jb/src/core/IO/Fetchers/Fetch_WeTransfer.cs`

Add other platform-specific strategies only when their links require custom resolution.

URL policy loaded from `jb/src/core/IO/cfg/HostRules.json`:
```json
{
  "allowedSchemes": ["http", "https"],
  "blockedSchemes": ["ftp"],
  "blockedHostPatterns": ["reddit.com", "*.reddit.com"],
  "redirects": {
    "allowGenericDirectFileRedirects": false,
    "allowFetcherOwnedRedirects": true
  },
  "networkRanges": {
    "allowPrivate": true,
    "allowLinkLocal": true,
    "allowLoopback": false,
    "rejectAnyLoopbackDnsResult": true
  },
  "timeouts": {
    "connectSeconds": 10,
    "responseHeaderSeconds": 15,
    "idleReadSeconds": 15,
    "totalFetchSeconds": 120
  },
  "testing": {
    "allowLocalhost": false
  }
}
```

**Validation order for URLs:**
1. Parse as absolute URI
2. Validate scheme against `HostRules.json`
3. Normalize and validate host against `HostRules.json`
4. Resolve DNS for loopback/private-network classification
5. Reject literal loopback and any loopback DNS result (except explicit localhost test mode)
6. Allow private-network/link-local/internal ranges after scheme, host, and loopback checks pass
7. Select a fetcher route
8. Apply fetcher-specific redirect policy
9. Enforce `Content-Length` policy before reading when required
10. Enforce observed-byte caps while streaming
11. Enforce timeout caps while connecting and reading
12. Convert accepted downloads into temporary input descriptors for `Importer.cs`

**Note:** Private-network/link-local/internal IP ranges are **deliberately allowed** because PRISM input media may live on PRISM-owned local servers. This is an explicit SSRF exception. Loopback remains rejected.

---

## Flat JPG Conversion

- Transparent pixels → `#ffffff` when flattened to JPG.
- EXIF orientation is applied during import normalization (image is correct-side-up before downstream stages).
- If no EXIF orientation found, keep original orientation.
- Normalized JPG is written with default orientation semantics — no dedicated EXIF orientation status field recorded in `ImageRecord_INPUT`, `ImageRecord_LAMBDA`, manifest, or frontend journey payload.

---

## Zip Handling

- Each logical job gets a temporary folder (cleaned up after output is returned).
- Temp folder used for: spill-to-disk inputs, downloaded files, extracted zip members, normalized JPGs, diagnostic snapshots, output assembly.
- Non-image and non-Excel zip members are **omitted silently** — no record, no count, no manifest entry.
- Corrupt, encrypted/password-protected, oversized, or malformed processable zip members → KO in `manifest.json` only. Healthy extractable members continue processing.
- Zip layout folder names are always `OK` and `KO`. Manifest is always `manifest.json`. Not configurable via `ZipLayout.json`.
- **Zip output parity:** Zip and JSON output both project from one canonical `BatchManifest`. Summary counts, per-item manifest rows, OK/KO status, KO groups, source metadata, output filenames, config snapshot, and safe diagnostics must be identical between zip and JSON exports.
- Exporters use the reserved manifest-backed output paths from rename/export preparation. If a final filename, zip entry path, or JSON artifact path collision is detected, the affected FamilyID/family KO state and safe collision evidence must be identical in zip and JSON projections.

### Zip Member KO Reasons

- Unextractable processable zip members use source stage `zip-extract` and reason `corrupt-zip-member`.
- Extracted image/document members that fail decode or normalization use the existing corrupt image/conversion reasons, including `500` or `541` when applicable.
- Encrypted archives and encrypted entries use source stage `zip-extract` and reason `password-protected`. PRISM does not prompt for passwords in the core pipeline.
- Corrupt zip/image member KO records appear in the manifest KO group `corrupt images`.
- Encrypted archive/member KO records appear in the manifest KO group `password protected zip`.
- Each zip member KO entry includes archive name/path plus member path/original filename when available.

---

## Original Image Export Policy

- Original input bytes are **never included by default**.
- Original images included in `PrismJobResult` only when `PrismProcessingParameters.ReturnOriginalImages` is true.
- Even when `ReturnOriginalImages=true`: `manifest.json` must **not** contain original image bytes.
- `ReturnOriginalImages=true` affects the returned result payload only, not the manifest contract.

---

## Corrupt Image KO Reasons

| Reason Code | Condition |
|---|---|
| `500` | Damaged file that could not be opened or fully decoded |
| `500` | Corrupt file where part of the image is missing |
| `541` | Conversion failure |

Safe description added for the client; abusable internals not disclosed. Appears in console log and `manifest.json`.

---

## Media Kind Triage

Media kind is triaged from **bytes**, not only from filename or MIME type.
PDF and TIFF pages are rendered according to import rules.
Supported image/document media are normalized into PRISM's flat JPG input representation.
Accepted Excel files are added to the Excel collection.
