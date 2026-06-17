# PRISM — Data Models & Record Definitions
*Abbreviations: `GLOSSARY.md`*

## IRI — `ImageRecord_INPUT.cs`

State after import, before classification. No EXIF orientation state (applied at import). Original bytes excluded from manifest; returned only when `PPP.ReturnOriginalImages = true`.

| Field | Notes |
|---|---|
| Original filename | safe source provenance |
| Source kind | local path, folder member, stream, multipart upload, URL, zip member |
| Original content type | when known |
| Byte length | when known |
| Accepted media classification | |
| Normalized JPG artifact reference | created by `Importer.cs` |
| Normalized dimensions | when available |
| Optional hash / visual-hash metadata | when available |
| Import status + safe diagnostics | |

---

## IRL — `ImageRecord_LAMBDA.cs`

Lifecycle hub: Imported → Classified → Matched → Ordered → Renamed → Generated → Transformed → Exported. Exposes ordered per-image route list for web visualization (stage name, status, safe message, bounded evidence, optional diagnostic ref).

| Field | Notes |
|---|---|
| Link to IRI | |
| Optional matched FR ID | once matching succeeds |
| Bounded ME | once matching has run |
| Measured IFs + candidate/selected INGP | |
| Border intersections, human detection, head visibility, skin-tone | |
| Per-trait confidence scores + derived booleans | |
| UNKNOWN/unavailable reasons | |
| Ordering result + final rename data | FID, `_det` order, final filename |
| Generation route state | skip / created child records / failed |
| Optional IRG child references | |
| Bounded ITR | once transformation has run |
| Optional IRO link | once exportable output exists |
| Current lifecycle status + KO/failure state | |

---

## IRO — `ImageRecord_OUTPUT.cs`

Exportable processed image artifact.

| Field | Notes |
|---|---|
| Final filename | |
| Extension | `.jpg` default |
| MIME type | `image/jpeg` default |
| Artifact or byte source reference | used by exporters |
| Width and height | |
| Byte length | when known |
| Checksum | when available |
| Export status | |
| Safe export metadata | for zip, JSON, BM, API, workbench consumers |

---

## IRG — `ImageRecord_GENERATED.cs`

Generation-specific details for generated child images. IRL records only skip/created/failed; details live here.

| Field | Notes |
|---|---|
| Source FID | |
| Source hero image / source image references | |
| Generation method | detail crop, GenAI background variation, or both |
| Generation parameters / safe config snapshot | |
| Quality decision | |
| Generated output image reference | when accepted |
| KO/failure reason | when rejected |
| Safe diagnostics | |
| Optional transient diagnostic references | non-persisted debug-only |

---

## FR — `FamilyRecord.cs`

Canonical catalog entity from IEM. Duplicate FIDs cannot exist; conflicting rows/columns preserve unique values as tokenized evidence.

| Field | Notes |
|---|---|
| FID | product/family identifier |
| Canonical properties | from dynamic Excel columns |
| Column classifications | PK, categorical, descriptive, mixed |
| Normalized tokens | used by matchers |
| Original source cell values | evidence |
| Conflict evidence | from merged duplicate rows or columns |

---

## BM — `BatchManifest.cs`

Canonical audit and export contract for a completed job. Both zip and JSON output project from this one manifest. Original bytes never placed in `manifest.json`.

| Field | Notes |
|---|---|
| Batch/job identifier | |
| Optional client request token | safe to echo |
| Summary | BMS record (see below) |
| `ImageRows` | list of MIR — one per image (see below) |
| KO groups and safe reason details | |
| Effective config snapshot / safe summary | |
| Stage/route summaries, warnings, diagnostics | |
| Optional transient diagnostic references | non-persisted debug-only |
| Output format metadata | filenames/artifact refs, content types, byte counts |

---

## BMS — `BatchManifestSummary.cs`

Split from BM in T-1100. Carries all batch-level counts.

| Field | Notes |
|---|---|
| `ImageCount` | total canonical images in batch |
| `ExcelCount` | accepted Excel files |
| `ZipCount` | ZIP archives |
| `OkRenamed` | images that received FID-based output filename |
| `KoRecords` | images that became KO for any reason |
| `OkTransformed` | images that completed transformation |
| `KoTransformed` | images where transformation produced KO |
| `GeneratedCount` | IRG child records created |

---

## MIR — `ManifestImageRow.cs`

Per-image row projected into `BM.ImageRows`. One row per canonical image.

| Field | Notes |
|---|---|
| `SourceReference` | original source filename or URL — safe provenance |
| `FinalFileName` | output filename when OK output exists |
| `Status` | pipeline status for this image |
| `KoReasonCode` | stable KO reason code when KO |
| `KoSafeMessage` | human-readable safe KO explanation |
| `FamilyId` | matched FID when accepted |
| `DetOrder` | zero-based det-slot index when ordered |
| `TransformerType` | which Tx class handled this image |
| `TransformationStatus` | outcome of the transformation decision |

---

## Manifest Row Projection

From IRI/IRL, ME, ITR, IRO: original filename, final filename (OK), status, KO reason, matched FID, route-stage summaries, bounded match/classification/transform evidence, output metadata (extension, MIME, dimensions, byte length, checksum, export status), safe diagnostics.

---

## ME — `MatchEvidence.cs`

Bounded matching decision embedded by IRL. Replaces retired `MatcherResult.cs`.

| Field | Notes |
|---|---|
| Original image identifier / source filename ref | |
| Final candidate FID | when accepted |
| Final score | |
| Threshold status | |
| Tie status | |
| Runner scores / bounded candidate summaries | when useful |
| Top candidate evidence | |
| Rejected near-tie evidence | when bounded |
| Numeric token evidence | |
| String token evidence | |
| Classification-label evidence | |
| Relevant IF and INGP summary | used by matching, ordering, transformation, diagnostics |
| Matcher names, scores, confidences, weights | |
| Safe explanation text | |
| Optional transient diagnostic references | for heavy/verbose evidence during processing |

ME stores paired evidence between image-origin tokens and FR/IEM cell evidence. Each token evidence item: token ID, token type, original token text, normalized token text, parser confidence (when available), matched family-side cell value.

---

## ITR — `ImageTransformationResult.cs`

Bounded transformation summary embedded by IRL.

| Field | Notes |
|---|---|
| Transformation status | |
| Input + output dimensions | |
| Crop rectangle / crop decision summary | when crop occurs |
| Resize mode, scale factor, target size | when resize occurs |
| Background fill method / no-fill/no-reposition state | |
| Warnings | |
| Failure reason | when transformation becomes KO |
| Safe summary text | for workbench/manifest display |
| Optional transient diagnostic references | masks, intermediate images, debug output during processing only |

Manifest rows project selected safe fields from the embedded bounded summary.

---

## PPE — `PipelineProgressEvent.cs`

Shared progress contract consumed by API SSE, WPF direct invocation, and workbench route visualization.

| Field | Notes |
|---|---|
| `JobID` | |
| Stage name | `Imported`, `Classified`, `Matched`, `Ordered`, `Renamed`, `Generated`, `Transformed`, `Exported` |
| Current item ID / safe current item name | when available |
| Completed count | |
| Total count | when known |
| Severity | |
| Safe message | |
| Timestamp | |

`Queued`, `Running`, `completed`, `failed` = job-status events that may appear around route-stage progress; they do not replace the definitive route-stage vocabulary.

---

## KO and Failure Record Fields

Used in BM, API errors, and workbench diagnostics.

| Field | Notes |
|---|---|
| Stable reason code | |
| Safe human-readable message | |
| Source stage | definitive route for item-level; owning boundary for API/import/export failures |
| Source file/zip member/worksheet/row/image record/artifact ref | when available |
| Item ID | when available |
| Retryable flag | |
| Batch-continues flag | |
| Safe details | no abusable internals |

---

## IF, INGP, and IRL Storage

**IF:** measured per-image attribute from CLIP or purpose-built analyzers. Retains source, confidence, UNKNOWN/unavailable state.

**IF storage decisions:**
- `salient-bbox` → `BoundingBox` in memory; serialized as flat `float[4]`.
- `pose-type` / `body-visible` → share one PAF detector pass, gated by `skin-tone-area` threshold; `body-visible` evaluated first.
- `product-type-label` → match corroborates, no match flags possible duplicates, extreme mismatch → KO.
- `dominant-colors` → spatially-weighted LAB palette-cluster with salient-mask background subtraction.

**INGP:** selected phenotype derived from a combination of IFs. Not the feature list. Examples: `PAP_FRONT`, `JEANS_GHOST_FRONT`, `JEANS_GHOST_DETAIL`.

**IRL owns:** IFs (source + confidence + UNKNOWN reasons); candidate/selected INGP; DO assignment evidence (which INGP/slot combinations qualified and why one won); ordering result and final rename data.

---

## Classification Tag Storage

- `IRL.Tags.Influential` — tags ≥ `Classification.Confidence_Threshold`
- `IRL.Tags.Trivial` — tags ≥ `Classification.Cutoff_Threshold` AND < `Confidence_Threshold`
- Tags below `Cutoff_Threshold` — discarded
