# PRISM — Transformation & Generation
*Abbreviations: `GLOSSARY.md`*

## ImagePreProcessor

`jb/src/core/Images/ImagePreProcessor.cs` — static class, single entry point `Preprocess(lambda, imagePath, config)`.

Steps in order:
1. **EXIF orient + flatten**: ImageSharp `AutoOrient()` + `BackgroundColor(White)` → encoded to flat JPEG (no alpha, sRGB).
2. **Salient bounding box**: OpenCVSharp Canny + local-contrast sigmoid mask, computed at ≤512 px analysis resolution. Result written to `lambda.Features["salient-bbox"]` as `"x1,y1,x2,y2"` (normalized 0–1 floats, invariant culture). Confidence fixed at 0.99.
3. **Upscale decision** (based on bbox pixel dimensions, not whole-image dimensions):
   - bbox largest dimension `< MinInputSizeInPixels` (570 px) → KO `PREPROCESS_TOO_SMALL`
   - `≥ MinOutputWidth` (800 px) → pass flat JPEG through unchanged
   - Between 570–800 → `ImageUpscaler.Upscale(bytes, scale)` (GPU if DirectML adapter present, else CPU Lanczos4 capped ×1.42)
   - Required scale `> MaxUpScaleFactor` (1.42) → KO `PREPROCESS_UPSCALE_EXCEEDED`
4. Returns upscaled flat-JPEG bytes or null on KO. Sets `lambda.IsKo`, `lambda.KoReasonCode`, `lambda.KoSafeMessage` on KO.

Called by `ImageTransformer` before routing; `lambda.Features["salient-bbox"]` is available to all Tx_ classes.

---

## Transformation Overview

Images transformed one by one, each based on image analysis enriched with match information. Salient object detection, bounding box calculation, and background identification feed the per-image transform decision. Useful tags from `ImageMatcher.cs` attenuate transformation parameters. Transform rules in `jb/src/core/Images/Transform`. Transformation parameters guided by per-image IFs and selected INGP phenotype.

**Current impl:** All Tx classes (`Tx_CropSquare`, `Tx_CenterAndStretch`, `Tx_DetailCropper`, `Tx_ProblemImageProcessor`) gated behind `ImageProcessorAvailable() = false`. Every image receives `TransformationStatus.Gated` — no pixel processing runs. Routing logic implemented and tested. Open work in `jb/src/core/Images/Transform/jbtodo.md`.

---

## Transform-Facing Classification Tags

Routing inputs read from `ImageRecord_LAMBDA.Features`:

| Feature | Role |
|---|---|
| `intersects-top/bottom/left/right` | Primary edge-intersect detection — drives Tx_CropSquare vs. Tx_DetailCropper routing |
| `salient-bbox` | Object bounds — required for Tx_CenterAndStretch and Tx_DetailCropper; UNKNOWN routes to Tx_ProblemImageProcessor |
| `low-contrast` | Triggers `Tx_LowContrastEnhancement` pre-step inside Tx_CenterAndStretch |
| `shadow-present` | Triggers shadow-band shrink of `salient-bbox` bottom edge inside Tx_CenterAndStretch |

Routing also reads `ImageRecord_LAMBDA.SelectedPhenotype` and `DetOrder` + `ProductTypeId` (see **Transform Routing Matrix** and **Det-Slot Exclusions** below).

All other IFs (human detection, head visibility, orientation, background, color, material) are available as secondary decision modifiers but do not currently gate the primary routing decision.

---

## Salient Object Bounds

Represented by `jb/src/core/Images/Transform/BoundingBox.cs`. Fields (all integers): `X`, `Y`, `Width`, `Height`, `Top`, `Left`, `Right`, `Bottom`.

`BoundingBox` does not emit confidence, detection method, or border-intersection flags. Border-intersection state tracked separately as transform/classification evidence.

---

## Background Identification

Emits: dominant background color + background type from `jb/src/core/Images/Transform/BackgroundType.cs`.

| Type | Meaning |
|---|---|
| `FLAT_PERFECT` | Single RGB value |
| `FLAT_NATURAL` | Visually flat with possible studio variance, dust, scratches, noise — no Hough lines |
| `TEXTURED` | Heavy luminance/chrominance variance or repeated patterns |
| `AMBIANCE` | Studio decorative objects, nature, urban, indoor location, similar |
| `UNKNOWN` | Cannot be determined safely |

---

## Light Object on Light Background

Object very light (white, light gray, creme) + background also very light → use different parameters to improve bounding box calculation.

---

## Border Intersection Rule (No-Reposition)

If salient object exits image frame at one or more edges → margin **cannot** be applied in that direction. Object "sticks" to intersecting edges. No repositioning in blocked direction(s).

When `Tx_DetailCropper` is selected but detects during pixel processing that the crop cannot be repositioned (border intersection blocks manipulation), it **internally delegates to `Tx_CropSquare`** — the image receives a centered square crop without background extension, is not KO'd, and is not exported unchanged. `ImageTransformationResult.TransformerType` records `Tx_CropSquare`; `Warnings` records the reason for the fallback. This is an internal fallback, not a routing decision in `ImageTransformer.cs`.

---

## Transform Routing Matrix

`ImageTransformer.SelectTransformer()` evaluates in order (first match wins):

1. `salient-bbox` is UNKNOWN **or** `SelectedPhenotype` is null (when phenotype bypass is off) → **`Tx_ProblemImageProcessor`**
2. Any edge intersect is true (`intersects-top/bottom/left/right`):
   - Phenotype is `"closeup-image"` or `"model-detail-closeup"` **and** det-slot is not in the exclusion range for this product type → **`Tx_DetailCropper`**
   - Otherwise → **`Tx_CropSquare`** (fallback for intersecting images)
3. No edge intersects → **`Tx_CenterAndStretch`**
4. Default → **`Tx_CropSquare`**

Notes:
- `Tx_DetailCropper` may internally delegate to `Tx_CropSquare` when pixel-level border intersection blocks repositioning (see **Border Intersection Rule** above). This is not a routing decision.
- `Tx_ProblemImageProcessor` never calls `Tx_CropSquare` — it applies a safe proportional resize only.
- While `BypassPhenotypes = true` (temporary PoC gate in `ImageTransformer.cs`), rule 1 skips the phenotype-null check and rule 2 always falls through to `Tx_CropSquare` (not `Tx_DetailCropper`).

## Det-Slot Exclusions for Tx_DetailCropper

`Tx_DetailCropper` is excluded for images at certain det-slots, which fall back to `Tx_CropSquare`:

| Product type | Excluded det-slots |
|---|---|
| Default (non-clothing) | 0, 1, 2 |
| `clothing-*` (any product type starting with `clothing-`) | 0, 1 |

- The `clothing-*` rule is prefix-based — no hard-coded list; read from `DetOrderRules.json`.
- Det-slot is `lambda.DetOrder` (int, 0-based, from `ImageRecord_Base`).
- Product type is stored on `ImageRecord_LAMBDA.ProductTypeId` (string?), written by `ImageOrderer` during the Order stage. See ticket T-1800.

## Repositioning and Margin Application

Margin applied so there is whitespace between object and image edge. Method: crop original image using bounding box coordinates + desired margin value. If repositioning would require **new pixels** → fill to mimic existing background (background extension).

---

## Background Extension

For eligible images (not blocked by intersection): repositioning centers the object by cropping/expanding geometry so the configured margin exists between object and image edge. New pixels are filled to mimic existing background. Object geometry must be preserved. Intersecting-border images remain governed by the no-reposition rule in blocked directions.

### Fill Method — Tiered by Extension Ratio

Extension ratio = filled canvas area / source image area.

| Tier | Extension ratio | Method |
|---|---|---|
| 1 | ≤ 125% | Basic edge extension (mirror or clamp border pixels outward) |
| 2 | ≤ 142% | Content-aware edge extension (patch-based or frequency-aware border propagation) |
| 3 | > 142% | OpenCV inpainting — INPAINT_TELEA preferred; INPAINT_NS as alternative |
| 4 | > 250% | Solid white fill (#FFFFFF) |

- Never use Gaussian blur as a fill method.
- Apply seam feathering at the extension boundary after tiers 1 and 2. Tier 3 (inpainting) handles its own seam implicitly. No separate blur pass at any tier.
- Implemented by `Tx_util_BgStretch.cs` (sub-step helper, not an `IImageTransformation` implementor).

**Post-fill cleanup (center-and-stretch):** seam feathering and local smoothing applied at extension boundaries after tier 1 and 2 passes. Tier 3 inpainting handles its own seam implicitly. No blur pass at any tier.

---

## Transform Failure & KO Policy

**KO the image** (transform stage) when:
- Decoded input is invalid (corrupt, unsupported encoding after format conversion).
- Image shorter dimension < `Input.Images.MINIMUM_SIZE_IN_PIXELS` (570 px) AND the upscale required to reach `Output.Images.Processed.MINIMUM_SIZE_IN_PIXELS` (800 px) would exceed `Output.Images.Resize.MAXIMUM_UpScale` (1.42×).

All other failure modes — unknown features, low-confidence geometry, fill/stretch failures — are handled by `Tx_ProblemImageProcessor` (conservative proportional resize, export with warnings). No other transform failure triggers KO.

## UNKNOWN → Problem Processing

When `ImageTransformer.cs` finds `salient-bbox` UNKNOWN, or `SelectedPhenotype` is null (while phenotype bypass is off), route to `Tx_ProblemImageProcessor.cs` for conservative processing. Do not use normal transform assumptions.

---

## Generation Logic

### Trigger

If image collection for a FID has **x or fewer images** (configurable in CFG) → generate new images, provided originals are high enough quality.

**Current impl:** `GenerationBackendAvailable()` returns `false`. Decision shell (which families qualify) implemented in `ImageGenerator.cs`. Every qualified family receives `GenerationRouteState.Gated` — no inference runs. Open work in `jb/src/core/Images/Generate/jbtodo.md`.

### Local Generation (Recommended)

ComfyUI + Flux.1-schnell (≥12 GB VRAM, 4-step distillation). On same machine or LAN server. ONNX Runtime considered later only if model conversion and quality are proven.

### External SaaS — NOT Permitted

No external SaaS pipeline dependencies. Only permitted external exception: `www.letsenhance.ai`.

### Generation Cases

For families with low image count, copy hero image (front-facing product/model, fullest view) and generate an alternative:
- Crop to a detail
- Embed on a different background using GenAI
- Or both

### IRG

IRL records only whether generation was skipped, created child records, or failed. Generation-specific details live in IRG — see `PRISM-models.md`.

### Order Gaps and Generation

Order gaps allowed when `_det` slots can be filled by copying/transforming an existing image. After generation, remaining gaps in `_det` sequence are closed during renaming.
