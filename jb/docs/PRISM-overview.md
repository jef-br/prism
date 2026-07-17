# PRISM — Overview, Specs & Terminology
*Abbreviations: `GLOSSARY.md`*

## System Specs

- **What**: Renames/transforms product images using Excel FID data.
- **Users**: Junior non-technical admin staff; ~250 concurrent.
- **Runtime**: Local servers; GPU not guaranteed; CPU-only fully supported.

## Core vs. Features

PRISM's **core** — the "prism" itself — is always deployed as one co-located unit: **friendly/greedy input aggregation → normalizing → matching → ordering → normalized output (export)**. Ingress, matching, and export are never split across machines; a full PRISM is always deployed internally even when only some routes are exposed publicly.

- **Ingress is a first-class front door.** Inputs arrive either as uploads or fetched from **URLs** (Dropbox / WeTransfer / direct HTTPS; `SourceKind = RemoteUrl`), materialized to the job's temp folder, then run straight through the in-process pipeline. Because ingress and matching share one process and one temp folder, the core needs **no shared filesystem between separate hosts** — there is no cross-machine image handoff to arrange.
- **Transform, Generate, and Upscale are features** layered on the core. They are the only parts that legitimately vary or run out-of-process per deployment (a given public instance may offer different transform/generation/upscale behavior via different API routes). The standalone `Prism.ServiceHost` hosts the four **public services** (`PRISM_SERVICE=matching|generate|transform|upscale`) — it takes pre-materialized inputs and does not itself fetch URLs. A Matching host is valid only co-located with the filesystem Ingest wrote (`PRISM-io-import.md` co-deployment contract). Ingest is never hosted as a service: media enters PRISM only through in-process ingress.

## Accepted Input

Images: `jpg jpeg png tif tiff pdf webp bmp gif`. PSD excluded unless added explicitly.
Excel: `.xlsx`, any human language, 2000+ suppliers.

## Batch & File Limits

- Cap: **10000 images** per batch (`Input.Images.amount.max` in CFG). Max **250 MB**/image file (`Input.Images.filesize.max`).
- Heavy daily avg: ~10k images + 2 Excel → fits in a single batch.
- Limits in CFG.

## External Resources

Pre-pipeline input only (Dropbox, WeTransfer, HTTP links). Each external image/zip converted before pipeline entry. Inside pipeline: `www.letsenhance.ai` only. Missing PRISM config/model → FFAIL.

## Pipeline Order (definitive, immutable)

```
Imported → Classified → Matched → Ordered → Renamed → Generated → Transformed → Exported
```

## Desired Output

Renamed/transformed images + `manifest.json`.
- All images renamed by matching filename vs Excel → FID
- Packshots: centered, consistent margin applied
- Non-repositionable (detail/lifestyle): cropped best possible; background stretched respecting intersection values
- `manifest.json`: counts (OK/KO), per-image original/new name, artifact ref — never original bytes

## Vocabulary

| Term | Meaning |
|---|---|
| FID | Primary product identifier; output filename stem |
| `_det` | Zero-based order suffix within FID (`_det0`, `_det1`, …) |
| IEM | Collated, deduplicated Excel worksheets |
| KO | Failed/rejected; recorded in manifest; does not stop job when valid work remains |
| Failed | PRISM-owned failure; stops the entire job |
| Canonical image | Highest-res representative of a visual-duplicate group |
| Batch | (1) Part of job where images are processed; (2) Complete image collection for one job |
| Request | Suffix for what a client asks PRISM |
| Result | Suffix for what a class or PRISM sends back |
| Job | Entire process, start to finish |
