# PRISM — Transformation & Generation
*Abbreviations: `GLOSSARY.md`*

## Transformation Overview

Images transformed one by one, each based on image analysis enriched with match information. Salient object detection, bounding box calculation, and background identification feed the per-image transform decision. Useful tags from `ImageMatcher.cs` attenuate transformation parameters. Transform rules in `jb/src/core/Images/Transform`. Transformation parameters guided by per-image IFs and selected INGP phenotype.

**Current impl:** All Tx classes (`Tx_CropSquare`, `Tx_CenterAndStretch`, `Tx_DetailCropper`, `Tx_ProblemImageProcessor`) gated behind `ImageProcessorAvailable() = false`. Every image receives `TransformationStatus.Gated` — no pixel processing runs. Routing logic implemented and tested. Open work in `jb/src/core/Images/Transform/jbtodo.md`.

---

## Transform-Facing Classification Tags

Available to transform decisions (from IRL):
- Selected INGP phenotype (when available)
- Type-of-shot IF, hero orientation IF (including `UNKNOWN`)
- Human detection, head visibility, skin-tone, and related measured state
- Border-intersection flags: `TOP`, `RIGHT`, `BOTTOM`, `LEFT`
- Background labels (flat/solid evidence + background color)
- Product/category, color, material, pose, and orientation labels

These are optional decision modifiers. Core geometry (salient object bounds + border intersections) is the primary transform input.

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

---

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
- Apply seam feathering at the extension boundary after tiers 1 and 2. Tier 3 handles its own seam implicitly.
- Implemented by `Tx_util_BgStretch.cs` (sub-step helper, not an `IImageTransformation` implementor).

---

## UNKNOWN → Problem Processing

When `ImageTransformer.cs` finds any transform-critical IF set to `UNKNOWN` → route to `Tx_ProblemImageProcessor.cs` for conservative processing. Do not use normal transform assumptions.

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
