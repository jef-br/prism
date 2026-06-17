# Source Information

All content migrated to `jb/docs/`. Entry point: `jb/docs/PRISM-index.md`. Abbreviations: `jb/docs/GLOSSARY.md`.

## Core spec

- **What**: Renames and transforms product images using Excel FID data.
- **Users**: Junior non-technical admin staff; ~250 concurrent.
- **Input**: `jpg jpeg png tif tiff pdf webp bmp gif` + `.xlsx` (2000+ suppliers). PSD excluded unless added explicitly.
- **Batch cap**: 2500 (normal); 5000 (ceiling). Max 25 MB/file. Limits in CFG.
- **Runtime**: Local servers; GPU not guaranteed. CPU-only fully supported; GPU is bonus only.
- **External resources**: Pre-pipeline input only (Dropbox, WeTransfer, HTTP). Inside pipeline: `www.letsenhance.ai` only. Missing config/model → FFAIL.
- **Output**: Renamed/transformed images + `manifest.json` (counts, per-image original/new name, artifact ref — never original bytes).

## Pipeline order (definitive, immutable)

```
Imported → Classified → Matched → Ordered → Renamed → Generated → Transformed → Exported
```

## Vocabulary

| Term | Meaning |
|---|---|
| FID | Primary product identifier; output filename stem |
| `_det` | Zero-based order suffix within FID (`_det0`, `_det1`, …) |
| IEM | Collated, deduplicated Excel worksheets |
| KO | Failed/rejected; recorded in manifest; does not stop job |
| Failed | PRISM-owned failure; stops the job |
| Canonical image | Highest-res representative of a visual-duplicate group |
| Batch | Complete image collection for one job |

## Unique values

**CLIP SHA-256** (`jb/src/core/Images/Classify/ONNX/clip-vit-b32-uint8/model_uint8.onnx`):
`4AC011172C8C022937BB83DAD2E8FC207F52F19972B36E14808CC3C8042C4E60`
Mismatch → FFAIL. Also in `jb/docs/PRISM-classify.md`.
