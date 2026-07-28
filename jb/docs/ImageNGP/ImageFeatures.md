# Image Feature Catalog

All features are measurable image attributes with a declared source and confidence value.
Features are computed during the **Classified** stage and stored in `ImageRecord_LAMBDA`.
Feature ids use `kebab-case`. Extraction difficulty and confidence assume **CPU-only execution** (no GPU, no SaaS, no proprietary vision systems).

Difficulty scale: `low` = reliable with classical CV / simple heuristics | `medium` = requires trained lightweight model or careful tuning | `high` = requires heavier model, still feasible on CPU but slower or less reliable.

Confidence scale: `very-high` ≥ 0.95 | `high` 0.85–0.94 | `medium` 0.70–0.84 | `low` < 0.70.

---

## 1. Human & Body Features

| Feature id | Description | Datatype | Possible values | Extraction difficulty | Expected confidence |
|---|---|---|---|---|---|
| `has-human` | Whether a human figure (partial or full) is detected in the image | Boolean | `true`, `false`, `unknown` | medium | high |
| `human-count` | Number of distinct human figures detected | Integer | 0, 1, 2, 3+ | medium | medium |
| `hero-is-human` | Whether the primary (hero) subject is a human wearing or holding the product | Enum | `TRUE`, `FALSE`, `UNKNOWN` | medium | high |
| `head-visible` | Degree to which the head/face is visible (full face, partial, occluded, absent) | Enum | `FULL`, `PARTIAL`, `NONE`, `UNKNOWN` | medium | medium |
| `body-visible` | Degree to which the full body is in frame | Enum | `full`, `three-quarter`, `half`, `bust`, `none`, `unknown` | medium | medium |
| `skin-tone-area` | Fraction of image area covered by detected skin-tone pixels (all skin tones) | Float [0.0–1.0] | Any float | low | high |

---

## 2. Orientation Features

| Feature id | Description | Datatype | Possible values | Extraction difficulty | Expected confidence |
|---|---|---|---|---|---|
| `hero-orientation` | Viewing angle / orientation of the hero product (or human wearing it) relative to camera | Enum | `FRONT`, `DIAGONAL`, `SIDEON`, `BACK`, `TOP`, `BOTTOM`, `UNKNOWN` | medium | medium |

---

## 3. Background Features

| Feature id | Description | Datatype | Possible values | Extraction difficulty | Expected confidence |
|---|---|---|---|---|---|
| `background-type` | Broad background category | Enum | `SOLIDCOLOR`, `REALLIFE`, `UNKNOWN` | low | high |
| `white-background` | Whether the background is predominantly white or near-white | Boolean | `true`, `false` | low | very-high |
| `clipping-path` | Whether the image has a clipping path / transparent/alpha background applied | Boolean | `true`, `false` | low | very-high |
| `transparent-background` | Whether the image background is fully transparent (alpha channel present and used) | Boolean | `true`, `false` | low | very-high |
| `lifestyle-background` | Whether the background contains real-world environmental or decorative content | Boolean | `true`, `false` | medium | high |
| `background-color` | Dominant background color when background is solid | String (CSS hex or color name) | Any color value or `unknown` | low | high |
| `shadow-present` | Whether the subject detector found a hard-edged cast shadow (thin, texture-only, chroma-unsupported lines stripped by shape). Published from `SubjectDetection.HasHardShadowEvidence` — not separately measured. Stays `unknown` when nothing was detected, and for alpha-derived detections, which carry no shadow information at all | Boolean | `true`, `false`, `unknown` | medium | medium |

---

## 4. Edge & Intersection Features

| Feature id | Description | Datatype | Possible values | Extraction difficulty | Expected confidence |
|---|---|---|---|---|---|
| `intersects-top` | Whether the salient object touches or crosses the top image border | Boolean | `true`, `false` | low | very-high |
| `intersects-bottom` | Whether the salient object touches or crosses the bottom image border | Boolean | `true`, `false` | low | very-high |
| `intersects-left` | Whether the salient object touches or crosses the left image border | Boolean | `true`, `false` | low | very-high |
| `intersects-right` | Whether the salient object touches or crosses the right image border | Boolean | `true`, `false` | low | very-high |
| `intersection-count` | Number of image borders the salient object intersects (0–4) | Integer | 0, 1, 2, 3, 4 | low | very-high |
| `fully-in-frame` | Salient object does not intersect any border | Boolean | `true`, `false` | low | very-high |

---

## 5. Shot & Composition Features

| Feature id | Description | Datatype | Possible values | Extraction difficulty | Expected confidence |
|---|---|---|---|---|---|
| `occlusion-level` | How much of the product is visible in frame | Enum | `full-product`, `mostly-visible`, `partially-occluded`, `closeup`, `unknown` | medium | medium |
| `crop-tightness` | How tightly the image is cropped to the subject (ratio of subject bounding box to image area) | Float [0.0–1.0] | Any float | low | high |
| `product-coverage-ratio` | Fraction of the total image area covered by the primary product | Float [0.0–1.0] | Any float | medium | medium |
| `image-occupancy` | Overall "busyness" of the image — how much of the frame contains non-background content | Float [0.0–1.0] | Any float | low | high |
| `overlap-count` | Number of distinct product instances that visually overlap each other | Integer | 0, 1, 2, 3+ | high | low |
| `multiple-products` | Whether more than one distinct product is visible | Boolean | `true`, `false`, `unknown` | medium | medium |

---

## 6. Lighting Features

| Feature id | Description | Datatype | Possible values | Extraction difficulty | Expected confidence |
|---|---|---|---|---|---|
| `overexposed` | Whether the image has significant overexposed (blown-out) regions | Boolean | `true`, `false` | low | high |
| `underexposed` | Whether the image has significant underexposed (crushed) regions | Boolean | `true`, `false` | low | high |

---

## 7. Content / Object Features

| Feature id | Description | Datatype | Possible values | Extraction difficulty | Expected confidence |
|---|---|---|---|---|---|
| `dominant-colors` | Top 3–5 dominant colors in the image (excluding background when detectable) | Array of color values | Array of CSS hex strings or color names | low | high |
| `product-color` | Estimated primary product color | String | CSS hex or color name, `unknown` | low | high |
| `product-type-label` | Coarse product category detected via classification model | String | e.g., `shirt`, `jeans`, `shoe`, `bottle` | high | medium |

---

## 8. Geometric / Structural Features

| Feature id | Description | Datatype | Possible values | Extraction difficulty | Expected confidence |
|---|---|---|---|---|---|
| `aspect-ratio` | Width-to-height ratio of the image (not the product bounding box) | Float | Any positive float | low | very-high |
| `product-aspect-ratio` | Width-to-height ratio of the detected product bounding box | Float | Any positive float | medium | medium |
| `salient-bbox` | Bounding box of the salient / primary object | Rect {x, y, w, h} normalized [0.0–1.0] | Four floats | medium | medium |
| `vertical-centering` | How vertically centered the product bounding box is relative to the image | Float [0.0–1.0] (1.0 = perfectly centered) | Any float | low | high |
| `horizontal-centering` | How horizontally centered the product bounding box is relative to the image | Float [0.0–1.0] | Any float | low | high |

---

## Notes

- All features carry a numeric confidence `double` and a boolean flag derived from `Classification.Confidence_Threshold`.
- When confidence is below threshold, the feature value is set to `UNKNOWN` (enum) or `unknown` (string/boolean) — never defaulted to a false or arbitrary value.
- Boolean features with `unknown` as a possible value indicate the detector could not reach a reliable conclusion; they differ from `false` (confidently absent).
- CPU-only feasibility: features rated `low` difficulty are reliable with classical OpenCV operations; `medium` requires a lightweight ONNX model (MobileNet-class or smaller); `high` requires a larger model and may be slower (still feasible, not real-time on low-end hardware).

---

## Architecture decisions (deliverables 6–8)

**`salient-bbox` storage**
Use the existing `BoundingBox` struct (`jb/src/core/Models/BoundingBox.cs`). Add a method to return a flat `float[4]` `[x, y, w, h]` normalized [0.0–1.0] for serialization. The typed struct is the in-memory form; the flat array is the serialized form.

**`body-visible` detector**
Populated by the CLIP-backed classifier (`ClipPrompts.json`), not a dedicated skeleton/pose
detector — no such pass exists in this codebase. `pose-type` was removed (T-4700): its only
producer was a stub that never shipped, and no phenotype rule can consume a feature that's
always `UNKNOWN`.

**`product-type-label` vs Excel ProductType**
Excel `ProductType` is authoritative. The detected `product-type-label` provides error-checking and supporting evidence; it becomes the authority only when Excel does not supply a ProductType value. Multiple ProductTypes may share one ImageFeature grouping — for example, sweater, hoodie, pullover, jacket, short coat, vest, and cardigan all map to the same `topwear-short` ImageFeature value. The label modulates confidence:
- Match → high confidence corroboration.
- No match → possible multiple-product indicator (medium confidence).
- Extreme mismatch → **KO** for the image record.

**`dominant-colors` extraction**
Palette-cluster (LAB-space) approach. Search area determined before clustering:
1. Spatial weighting: top-wear ProductTypes weight the top image half more; bottom-wear types weight the bottom half more.
2. Background subtraction: a cluster touching all four edges is a background candidate. If it does not heavily intersect the salient mask (not the bbox), confirm it as background and subtract that region before the final palette-cluster run.
