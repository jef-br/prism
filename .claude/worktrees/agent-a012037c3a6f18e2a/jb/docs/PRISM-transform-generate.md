# PRISM — Transformation & Generation

## Transformation Overview

Images are transformed **one by one**, each based on the result of image analysis enriched with information from image matching.

- `Preprocessor.cs` handles the actual image analysis: salient object detection, bounding box calculation, background identification.
- Useful tags and tokens from `ImageMatcher.cs` are used to **attenuate transformation parameters**.
- Transform rules located in `jb/src/core/Images/Transform`.
- Transformation parameters are guided by per-image ImageFeatures and the selected ImageNGP phenotype.

---

## Transform-Facing Classification Tags

Available to transform decisions (from `ImageRecord_LAMBDA`):
- Selected ImageNGP phenotype when available
- Type-of-shot feature when available
- Hero orientation feature, including `UNKNOWN`
- Human detection, head visibility, skin-tone, and related measured per-image state
- Border-intersection flags: `TOP`, `RIGHT`, `BOTTOM`, `LEFT`
- Background labels (flat/single-color evidence and background color)
- Product/category, color, material, pose, and orientation labels

These tags are optional decision modifiers. Core geometry (salient object bounds and border intersections) remains the primary transform input.

---

## Salient Object Bounds

The salient object bounding box is represented by `jb/src/core/Images/Transform/BoundingBox.cs`.

Bounding box fields are integers:
- `X`
- `Y`
- `Width`
- `Height`
- `Top`
- `Left`
- `Right`
- `Bottom`

`BoundingBox` does not emit confidence, detection method, or border-intersection flags. Border-intersection state is tracked separately as transform/classification evidence.

---

## Background Identification

Background identification emits:
- Dominant background color.
- Background type from `jb/src/core/Images/Transform/BackgroundType.cs`.

Allowed background types:
- `FLAT_PERFECT` — the background is made from one single RGB value.
- `FLAT_NATURAL` — visually flat background with possible studio lighting variance, dust, scratches, or image noise, but no heavy contrast lines that qualify as Hough lines.
- `TEXTURED` — heavy luminance or chrominance variance, repeated patterns, or both.
- `AMBIANCE` — studio setting with decorative objects, nature scene, urban photography, indoor location shoot, or similar context.
- `UNKNOWN` — background type cannot be determined safely.

---

## Light Object on Light Background

If the object is a very light color (white, light gray, creme, …) and the background is also a very light color → the object detection algorithm should use **different parameters** to improve bounding box calculation.

---

## Border Intersection Rule (No-Reposition)

If the salient object exits the image frame at one or more edges (content appears cropped), a margin **cannot** be applied in the direction of that edge.

- Object "sticks" to the intersecting edge(s).
- No repositioning is allowed in the blocked direction(s).

---

## Repositioning and Margin Application

When repositioning the object:
- A margin is applied so there is whitespace between the object and the image edge.
- Method: crop the original image using bounding box coordinates + desired margin value.
- If this crop-based repositioning would cause the original image to move such that **new pixels need to be added**, those new pixels must be filled to **mimic the already-existing pixels that make up the background** (background extension).

---

## Background Extension

For eligible images (not blocked by border intersection):
- Repositioning centers the object by cropping or expanding geometry so the configured margin exists between object and image edge.
- New pixels created by repositioning → filled to mimic the existing background.
- Object geometry must be preserved.
- Images whose content intersects a border remain governed by the no-reposition rule in the blocked direction.

---

## Unknown Classification → Problem Processing

When `ImageTransformer.cs` finds any transform-critical ImageFeature value set to `UNKNOWN`:
- Route to `Tx_ProblemImageProcessor.cs` for conservative processing.
- Do not use normal transform assumptions.

---

## Generation Logic

### Trigger Condition

If an image collection for a specific FamilyID has **x or fewer images** (configurable in `Prism_Config.json`), new images should be generated — provided existing original images are high enough quality.

### Local Generation (Recommended)

Run a small **Stable Diffusion** or **SDXL Turbo** workflow through **ComfyUI** on the same machine or LAN server. ONNX Runtime considered later only if model conversion and quality are proven.

### External SaaS — NOT Permitted

External SaaS generation services such as KREA.ai are examples of the kind of capability. They are **not permitted** as pipeline dependencies.

The only permitted external pipeline exception is the upscaling API at `www.letsenhance.ai`.

### Generation Cases

For families with a low image count, copy the hero image (front-facing product/model in fullest view) to generate an alternative version:
- Crop to a detail
- Embed the image on a different background using GenAI
- Or both

### `ImageRecord_GENERATED` — see `PRISM-models.md`

`ImageRecord_LAMBDA` only records whether generation was skipped, created child records, or failed. Generation-specific details live in `ImageRecord_GENERATED`.

### Order Gaps and Generation

Order gaps are allowed when images for a given `_det` slot can be filled by copying and transforming an existing image. This is part of the generation step.

### Det Suffix After Generation

After generation, any remaining gaps in the `_det` sequence are closed during renaming.
