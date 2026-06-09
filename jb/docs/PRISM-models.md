# PRISM — Data Models & Record Definitions

## `ImageRecord_INPUT.cs`

Handoff between import normalization and the image-processing route. State after import, before classification/matching.

| Field | Notes |
|---|---|
| Original filename | Safe source provenance |
| Source kind | local path, folder member, stream, multipart upload, URL, zip member |
| Original content type | when known |
| Byte length | when known |
| Accepted media classification | |
| Normalized JPG artifact reference | created by `Importer.cs` |
| Normalized dimensions | when available |
| Optional hash / visual-hash input metadata | when available |
| Import status | |
| Safe import diagnostics | |

- No EXIF orientation diagnostic state (normalized before pipeline entry).
- Original image bytes not included in manifest data; returned only when `ReturnOriginalImages=true`.
- Stores image-origin filename/content tokens and original filename provenance. Filename offset/start-end metadata is not required because the token value and original filename are both retained.

---

## `ImageRecord_LAMBDA.cs`

Lifecycle hub for one canonical image through the definitive route: imported > classified > matched > ordered > renamed > generated > transformed > exported.

| Field | Notes |
|---|---|
| Stable link to `ImageRecord_INPUT` | |
| Optional matched `FamilyRecord` ID | once matching succeeds |
| Bounded `MatchEvidence` summary | once matching has run |
| Classification state (measured ImageFeatures + candidate/selected ImageNGP phenotype) | |
| Border intersections | |
| Human detection output | |
| Head visibility output | |
| Skin-tone area / related measured signals | |
| Per-trait confidence scores and derived booleans | |
| Unknown/unavailable reasons | when defined |
| Candidate and selected `ImageNGP` classification summary | |
| Ordering result and final rename data | FamilyID, `_det` order, final filename |
| Generation route state | skip / created child records / failed |
| Optional references to `ImageRecord_GENERATED` child records | |
| Bounded `ImageTransformationResult` summary | once transformation has run |
| Optional `ImageRecord_OUTPUT` link | once exportable output exists |
| Current lifecycle status | |
| KO/failure state | when image cannot continue |

Exposes an ordered per-image route list for web visualization. Each route entry has: stage name, sequence, status, safe message, optional KO reason, optional bounded evidence summary, and optional manifest-backed diagnostic details.

Normal match, classification, transform, route, and naming summaries: embedded when bounded. Retained diagnostics are projected into `manifest.json`; no separate persisted diagnostic snapshot artifacts are required.

---

## `ImageRecord_OUTPUT.cs`

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
| Safe export metadata | needed by zip, JSON, manifest, API, or workbench consumers |

---

## `ImageRecord_GENERATED.cs`

Generation-specific details for generated child images. `ImageRecord_LAMBDA` only records whether generation was skipped/created/failed; details live here.

| Field | Notes |
|---|---|
| Source FamilyID | |
| Source hero image / source image references | |
| Generation method | detail crop, GenAI background variation, or both |
| Generation parameters / safe config snapshot | |
| Quality decision | |
| Generated output image reference | when accepted |
| KO/failure reason | when rejected |
| Safe diagnostics | |
| Optional transient diagnostic references | non-persisted debug-only references when available |

---

## `FamilyRecord.cs`

Canonical catalog entity produced from the IEM.

| Field | Notes |
|---|---|
| FamilyID | product/family identifier |
| Canonical properties | derived from dynamic Excel columns |
| Column classifications | primary key, categorical, descriptive, mixed |
| Normalized tokens | used by numeric and string matchers |
| Original source cell values | safe and useful for evidence |
| Conflict evidence | from merged duplicate rows or duplicate columns |

Rules:
- Duplicate FamilyID records cannot exist in IEM.
- Conflicting rows/columns: preserve unique values, retain conflicting source values as tokenized evidence.

---

## `BatchManifest.cs`

Canonical audit and export contract for a completed job. Both zip and JSON output project from this one manifest.

| Field | Notes |
|---|---|
| Batch/job identifier | |
| Optional client request token | safe to echo |
| Summary counts | images, Excel files, OK renamed, KO renamed, OK transformed, KO transformed, dropped/KO images, generated records |
| Per-image manifest rows | projected from `ImageRecord_LAMBDA` |
| KO groups and safe reason details | |
| Effective configuration snapshot / safe summary | |
| Stage/route summaries, warnings, diagnostics | |
| Optional transient diagnostic references | non-persisted debug-only references when available |
| Output format metadata | filenames/artifact references, content types, byte counts, export metadata |

Original image bytes are **never** placed in `manifest.json`.

---

## Manifest Row Projection

Fields projected from `ImageRecord_INPUT`, `ImageRecord_LAMBDA`, `MatchEvidence`, `ImageTransformationResult`, `ImageRecord_OUTPUT`:
- Original filename + safe source provenance
- Final filename when OK output exists
- Current/final status
- KO reason when applicable
- Matched FamilyID when accepted
- Route-stage summaries (imported → exported)
- Bounded matching evidence summary and scores
- Bounded classification summary and confidence state
- Bounded transformation summary
- Output metadata (extension, MIME type, dimensions, byte length, checksum, export status) when available
- Safe diagnostics and manifest-backed retained diagnostic details

---

## `MatchEvidence.cs`

Bounded matching decision and explanation embedded by `ImageRecord_LAMBDA`. Replaces the retired `MatcherResult.cs`.

| Field | Notes |
|---|---|
| Original image identifier / source filename reference | |
| Final candidate FamilyID | when accepted |
| Final score | |
| Threshold status | |
| Tie status | |
| Runner scores / bounded candidate summaries | when useful |
| Top candidate evidence | |
| Rejected near-tie evidence | when bounded |
| Numeric token evidence | |
| String token evidence | |
| Classification-label evidence | |
| Relevant ImageFeature and `ImageNGP` summary | used by matching, ordering, transformation, diagnostics |
| Matcher names, scores, confidences, weights | |
| Safe explanation text | |
| Optional transient diagnostic references | for heavy/verbose evidence when available during processing |

`MatchEvidence` stores paired evidence between image-origin tokens and `FamilyRecord` / Internal Excel Model cell evidence. Each retained token evidence item includes token ID, token type, original token text, normalized token text, parser confidence when available, and the matched family-side cell value that produced the evidence.

---

## `ImageTransformationResult.cs`

Bounded transformation summary embedded by `ImageRecord_LAMBDA`.

| Field | Notes |
|---|---|
| Transformation status | |
| Input dimensions and output dimensions | |
| Crop rectangle / crop decision summary | when crop occurs |
| Resize mode, scale factor, target size | when resize occurs |
| Background fill method / no-fill/no-reposition state | |
| Warnings | |
| Failure reason | when transformation becomes KO |
| Safe summary text | for workbench/manifest display |
| Optional transient diagnostic references | masks, intermediate images, preprocessing debug output during processing only |

Manifest rows project selected safe fields from the embedded bounded summary.

---

## `PipelineProgressEvent.cs`

Shared progress contract consumed by API SSE transport, WPF direct invocation, and workbench route visualization.

| Field | Notes |
|---|---|
| `JobID` | |
| Stage name | from definitive route: `Imported`, `Classified`, `Matched`, `Ordered`, `Renamed`, `Generated`, `Transformed`, `Exported` |
| Current item ID / safe current item name | when available |
| Completed count | |
| Total count | when known |
| Severity | |
| Safe message | |
| Timestamp | |

`Queued`, `Running`, `completed`, and `failed` are job-status events that may appear around route-stage progress, but they do not replace the definitive route-stage vocabulary.

---

## KO and Failure Record Fields

Used in `BatchManifest`, API errors, and workbench diagnostics.

| Field | Notes |
|---|---|
| Stable reason code | |
| Safe human-readable message | |
| Source stage | definitive route for item-level; owning boundary for API/import/export failures |
| Source file/zip member/worksheet/row/image record/artifact reference | when available |
| Item ID | when available |
| Retryable flag | |
| Batch-continues flag | |
| Safe details | no abusable internals |

---

## ImageFeature, ImageNGP, and ImageRecord_LAMBDA Storage

**ImageFeature** values are measured per-image attributes. They can come from CLIP classification or purpose-built analyzers and must retain source, confidence, and unknown/unavailable state when that information is available. Examples include:
- Product type or product label evidence
- Lighting and background state
- Hero orientation
- Hero head visibility
- Human presence
- Border intersections
- Skin-tone evidence
- Object bounds and background state

**`ImageNGP`** is the selected image phenotype derived from a combination of ImageFeatures. It is not the list of individual features. Examples of the intended phenotype shape are `PAP_FRONT`, `JEANS_GHOST_FRONT`, `JEANS_BUTT`, and `JEANS_GHOST_DETAIL`.

**`ImageRecord_LAMBDA.cs`** owns measured per-image state and derived classification/order summaries:
- ImageFeatures with source, confidence, and unknown/unavailable reasons
- Candidate ImageNGPs and selected ImageNGP for that image
- DetOrder assignment evidence, including which ImageNGP/DetOrder combinations qualified and why one won
- Ordering result and final rename data

---

## Classification Tag Storage

- `ImageRecord_LAMBDA.Tags.Influential` — tags ≥ `Classification.Confidence_Threshold`
- `ImageRecord_LAMBDA.Tags.Trivial` — tags ≥ `Classification.Cutoff_Threshold` and < `Confidence_Threshold`
- Tags below `Cutoff_Threshold` — discarded
