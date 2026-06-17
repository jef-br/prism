# PRISM — Overview, Specs & Terminology
*Abbreviations: `GLOSSARY.md`*

## System Specs

- **What**: Renames/transforms product images using Excel FID data.
- **Users**: Junior non-technical admin staff; ~250 concurrent.
- **Runtime**: Local servers; GPU not guaranteed; CPU-only fully supported.

## Accepted Input

Images: `jpg jpeg png tif tiff pdf webp bmp gif`. PSD excluded unless added explicitly.
Excel: `.xlsx`, any human language, 2000+ suppliers.

## Batch & File Limits

- Normal cap: **2500 images**; hard ceiling: **5000**. Max **25 MB**/file.
- Heavy daily avg: ~10k images + 2 Excel → ~4 batches of 2500.
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
