# PRISM — Overview, Specs & Terminology

## What is PRISM

PRISM is an image processing pipeline that renames and transforms product images by combining incoming media with data found in Excel files.

- **Target user:** Junior non-technical administrative support staff
- **Concurrency target:** Technically able to serve 250 concurrent users
- **Runtime:** Local servers. Hardware specs subject to change and will not always include a GPU.

## Accepted Input Media

Images and documents: `jpg`, `jpeg`, `png`, `tif`, `tiff`, `pdf`, `webp`, `bmp`, `gif`

PSD is not accepted unless added later as an explicit supported media type.

Excel: `.xlsx` files in any human language, from any of +2000 suppliers.

## Batch & File Limits

- Heavy daily average per user: ~10k images and 2 Excel files → ~4 batches of 2500 images each
- Normal configured cap: **2500 images per batch**
- Hard ceiling PRISM must handle with ease: **5000 images per batch**
- No single file larger than **25 MB** by default
- File and request limits configured in `jb/src/core/Prism_Config.json`

## External Resources

External resources are allowed **before** entering the pipeline (as input media only):
- Dropbox, WeTransfer, cloud platform links, direct HTTP links
- External image-like resources must be converted to flat JPG (raw byte array or memory-backed stream) before the pipeline receives them
- Zip resources must be unzipped; each valid image inside → flat JPG; each Excel inside → Excel collection

Once data is inside the pipeline, **no external resources are permitted**, except the upscaling API at `www.letsenhance.ai`.

Missing PRISM-owned configuration or model files must **fail fast and loud**.

## High-Level Pipeline Order

```
Imported > Classified > Matched > Ordered > Renamed > Generated > Transformed > Exported
```

This is the **definitive, final, and only valid pipeline order.**

## Desired Output

A collection of renamed/transformed images + `manifest.json`.

- Every image renamed by comparing filename with data from Excel file(s) to find `familyID`
- Images transformed according to the image typology defined in `core/images/classify`
- Product packshots: centered with consistent margin applied
- Non-repositionable images (detail/lifestyle): cropped as well as possible with background stretched respecting Intersection values
- `manifest.json` contains batch summary (counts OK/KO, per-image original/new filename, processed artifact reference — NOT the original image)

## Terminology / Vocabulary

| Term | Meaning |
|---|---|
| **Request** | Suffix for something a client asks PRISM |
| **Result** | Suffix for something a class sends back, or something PRISM sends back to a client |
| **Job** | The entire process including every single step start to finish |
| **Batch** | (1) The part of a job where images are processed (classified → exported); (2) The actual image collection — the complete set of all image files in a job, in any form (byte stream, memory-backed stream, artifact reference, or file on disk), including those found inside zips or remote locations |
| **IEM** | Internal Excel Model — all collated worksheets with deduplicated rows/columns |
| **FamilyID** | The primary product/family identifier; becomes the output filename stem |
| **_det** | Suffix indicating image order within a FamilyID family (zero-based: `_det0`, `_det1`, …) |
| **KO** | A failed/rejected item that is recorded in the manifest but does not stop the job when valid work remains |
| **Failed** | A PRISM-owned failure that stops the entire job |
| **Canonical image** | The highest-resolution representative of a visually duplicate group; proceeds through pipeline |
