# Image Phenotype Catalog

An **ImageNGP phenotype** is a named, compound image type derived by combining multiple `ImageFeature` values.
Phenotypes are the classification unit used to assign images to `DetOrder` slots.

All phenotype ids use `kebab-case`.
Feature references use the ids from `ImageFeatures.md`.
Detectability is evaluated for **CPU-only** execution: OpenCV, image segmentation, lightweight object detection, image classification (MobileNet-class ONNX), and geometric analysis — no GPU, no SaaS, no proprietary vision.

---

## Detection confidence key

| `easy_to_detect` | Meaning |
|---|---|
| `true` | Phenotype can be detected with very high confidence (≥ 0.90) using CPU-only methods; the required feature combination is largely unambiguous |
| `false` | Phenotype detection is unreliable, ambiguous between similar phenotypes, or requires a heavier model to reach usable confidence on CPU |

---

## Phenotype definitions

---

### `front-packshot`

| Field | Value |
|---|---|
| **Phenotype id** | `front-packshot` |
| **Description** | Hero product shot photographed from the front on a solid-color or transparent background; no human present. Classic e-commerce reference image. |
| **Required features** | `hero-is-human = FALSE`, `hero-orientation = FRONT`, `background-type = SOLIDCOLOR OR clipping-path = true`, `occlusion-level = full-product`, `intersection-count = 0` |
| **Optional features** | `white-background = true`, `shadow-present`, `lighting = EASY` |
| **easy_to_detect** | true |
| **Rationale** | Solid/white background + no human + front symmetry + full product in frame is a highly distinctive and common combination. Symmetry score + background detection + intersection check are all reliable on CPU. |

---

### `back-packshot`

| Field | Value |
|---|---|
| **Phenotype id** | `back-packshot` |
| **Description** | Hero product shot photographed from the rear on a solid-color or transparent background; no human present. |
| **Required features** | `hero-is-human = FALSE`, `hero-orientation = BACK`, `background-type = SOLIDCOLOR OR clipping-path = true`, `occlusion-level = full-product`, `intersection-count = 0` |
| **Optional features** | `white-background = true`, `shadow-present` |
| **easy_to_detect** | false |
| **Rationale** | Distinguishing BACK from FRONT without human presence requires asymmetric product features (labels, logos, closures). Symmetry score alone is insufficient; product-type-specific texture/logo detection needed. |

---

### `side-packshot`

| Field | Value |
|---|---|
| **Phenotype id** | `side-packshot` |
| **Description** | Hero product on a solid/transparent background viewed from the side (left or right profile). |
| **Required features** | `hero-is-human = FALSE`, `hero-orientation = SIDEON`, `background-type = SOLIDCOLOR OR clipping-path = true`, `occlusion-level = full-product` |
| **Optional features** | `white-background = true` |
| **easy_to_detect** | false |
| **Rationale** | Distinguishing SIDE from DIAGONAL is ambiguous for soft goods (clothing). Reliable for rigid products (bottles, shoes) where silhouette profile differs clearly. |

---

### `diagonal-packshot`

| Field | Value |
|---|---|
| **Phenotype id** | `diagonal-packshot` |
| **Description** | Product on a solid/transparent background at a 3/4 diagonal angle. Common for shoes and footwear. |
| **Required features** | `hero-is-human = FALSE`, `hero-orientation = DIAGONAL`, `background-type = SOLIDCOLOR OR clipping-path = true`, `occlusion-level = full-product` |
| **Optional features** | `white-background = true`, `shadow-present` |
| **easy_to_detect** | false |
| **Rationale** | Diagonal orientation detection requires edge/silhouette asymmetry analysis. Reliable for shoes (distinctive sole/upper silhouette), less reliable for symmetric soft goods. |

---

### `top-packshot`

| Field | Value |
|---|---|
| **Phenotype id** | `top-packshot` |
| **Description** | Product photographed from directly above on a solid/transparent background. Common for accessories, bags, homeware. |
| **Required features** | `hero-is-human = FALSE`, `hero-orientation = TOP`, `background-type = SOLIDCOLOR OR clipping-path = true`, `occlusion-level = full-product` |
| **Optional features** | `white-background = true` |
| **easy_to_detect** | true |
| **Rationale** | Overhead camera angle (flat-lay) produces distinctive compositional patterns detectable by aspect ratio of product bounding box, high symmetry, and absence of perspective distortion. |

---

### `bottom-packshot`

| Field | Value |
|---|---|
| **Phenotype id** | `bottom-packshot` |
| **Description** | Product photographed from below (sole of shoe, base of appliance, underside of bag). |
| **Required features** | `hero-is-human = FALSE`, `hero-orientation = BOTTOM`, `background-type = SOLIDCOLOR OR clipping-path = true`, `occlusion-level = full-product` |
| **Optional features** | `white-background = true` |
| **easy_to_detect** | false |
| **Rationale** | BOTTOM orientation is rare and product-specific (shoe sole, bowl bottom). Detection relies on product-type knowledge. |

---

### `front-on-model-full-product`

| Field | Value |
|---|---|
| **Phenotype id** | `front-on-model-full-product` |
| **Description** | A real person wearing / holding the product, photographed from the front with the full body visible. The most human-facing hero image for clothing and accessories. |
| **Required features** | `hero-is-human = TRUE`, `hero-orientation = FRONT`, `head-visible = FULL OR PARTIAL`, `body-visible = full`, `intersection-count = 0` |
| **Optional features** | `background-type = STUDIO OR SOLIDCOLOR`, `lighting = EASY`, `pose-type = standing` |
| **easy_to_detect** | true |
| **Rationale** | Combination of human present + front symmetry + full body + head present + studio background is highly distinctive. Human detection and PAF pose estimation are both feasible on CPU for this common configuration. |

---

### `front-on-model-partial`

| Field | Value |
|---|---|
| **Phenotype id** | `front-on-model-partial` |
| **Description** | A real person photographed from the front but with body partially out of frame (e.g., cropped below the knee, or bust-only shot). |
| **Required features** | `hero-is-human = TRUE`, `hero-orientation = FRONT`, `body-visible = three-quarter OR half OR bust`, `intersects-bottom = true OR intersects-top = true` |
| **Optional features** | `head-visible = FULL OR PARTIAL`, `background-type = STUDIO` |
| **easy_to_detect** | true |
| **Rationale** | Border intersection clearly signals cropping; combined with human and front detection this is reliable. |

---

### `back-on-model-full-product`

| Field | Value |
|---|---|
| **Phenotype id** | `back-on-model-full-product` |
| **Description** | A real person wearing the product, photographed from the rear, full body visible. Shows product back details (labels, closures, back-print). |
| **Required features** | `hero-is-human = TRUE`, `hero-orientation = BACK`, `body-visible = full`, `head-visible = NONE OR PARTIAL`, `intersection-count = 0` |
| **Optional features** | `background-type = STUDIO`, `pose-type = standing` |
| **easy_to_detect** | false |
| **Rationale** | BACK orientation with a human requires detecting that the person is facing away — back-of-head vs. face, shoulder asymmetry. Possible with PAF pose estimation but confidence is lower. |

---

### `side-on-model`

| Field | Value |
|---|---|
| **Phenotype id** | `side-on-model` |
| **Description** | Person wearing the product photographed from the side. Common for showing silhouette, fit, and sleeve/leg length. |
| **Required features** | `hero-is-human = TRUE`, `hero-orientation = SIDEON`, `body-visible = full OR three-quarter` |
| **Optional features** | `background-type = STUDIO`, `head-visible = PARTIAL OR FULL` |
| **easy_to_detect** | false |
| **Rationale** | Side-on human silhouette is detectable but SIDEON vs. DIAGONAL requires confidence in the exact angle. PAF skeleton lateral pose is feasible but lower confidence than frontal. |

---

### `ghost-front`

| Field | Value |
|---|---|
| **Phenotype id** | `ghost-front` |
| **Description** | Product photographed on an invisible mannequin (ghost mannequin / hollow-man technique) from the front. Garment appears to be worn but no human is present. |
| **Required features** | `hero-is-human = FALSE`, `hero-orientation = FRONT`, `contains-mannequin = false`, `background-type = SOLIDCOLOR OR clipping-path = true`, `occlusion-level = full-product`, `intersection-count = 0` |
| **Optional features** | `white-background = true`, `lighting = EASY` |
| **easy_to_detect** | false |
| **Rationale** | Ghost shot looks like a packshot with a 3D structured garment. Distinguishing a ghost from a flat packshot requires detecting the 3D structure implied by fold patterns and collar/neckline shape — medium difficulty on CPU. |

---

### `ghost-back`

| Field | Value |
|---|---|
| **Phenotype id** | `ghost-back` |
| **Description** | Product on invisible mannequin from the rear. |
| **Required features** | `hero-is-human = FALSE`, `hero-orientation = BACK`, `background-type = SOLIDCOLOR OR clipping-path = true`, `occlusion-level = full-product` |
| **Optional features** | `white-background = true` |
| **easy_to_detect** | false |
| **Rationale** | Same difficulty as `back-packshot` plus ghost-vs-flat ambiguity. |

---

### `ghost-side`

| Field | Value |
|---|---|
| **Phenotype id** | `ghost-side` |
| **Description** | Product on invisible mannequin from the side. |
| **Required features** | `hero-is-human = FALSE`, `hero-orientation = SIDEON`, `background-type = SOLIDCOLOR OR clipping-path = true`, `occlusion-level = full-product` |
| **Optional features** | `white-background = true` |
| **easy_to_detect** | false |
| **Rationale** | Side orientation detection is ambiguous for soft goods; ghost vs. packshot ambiguity adds another uncertainty. |

---

### `flatlay-front`

| Field | Value |
|---|---|
| **Phenotype id** | `flatlay-front` |
| **Description** | Product laid flat on a surface, photographed from directly above in a frontal / face-up orientation. Camera is overhead. No 3D structure visible. |
| **Required features** | `hero-is-human = FALSE`, `hero-orientation = TOP`, `top-view = true`, `intersection-count = 0` |
| **Optional features** | `background-type = SOLIDCOLOR`, `white-background = true`, `shadow-present` |
| **easy_to_detect** | true |
| **Rationale** | Overhead camera angle (top-view = true) combined with no human is highly reliable via geometric analysis (flat product projection, minimal perspective distortion, low product-aspect-ratio for most clothing). |

---

### `flatlay-styled`

| Field | Value |
|---|---|
| **Phenotype id** | `flatlay-styled` |
| **Description** | Product laid flat from overhead but surrounded by styled accessories, props, or additional items. Background and composition are decorative. |
| **Required features** | `hero-is-human = FALSE`, `top-view = true`, `multiple-products = true OR lifestyle-background = true` |
| **Optional features** | `intersection-count ≥ 1`, `text-present` |
| **easy_to_detect** | false |
| **Rationale** | Detecting styled vs. clean flatlay requires distinguishing multiple objects and decorative vs. plain backgrounds — reliable for the background dimension, less reliable for the styled-vs-clean distinction. |

---

### `closeup-image`

| Field | Value |
|---|---|
| **Phenotype id** | `closeup-image` |
| **Description** | Close-up of any product detail — fabric, texture, stitching, hardware, label, tag, or component — where the detail fills most or all of the frame and the image content intersects with at least one image border. No human present. Product-detail purpose only. Consolidates all close-up detail phenotypes (material, stitching, label, hardware). |
| **Required features** | `hero-is-human = FALSE`, `intersection-count ≥ 1`, `occlusion-level = closeup` |
| **Optional features** | `material-texture-visible`, `text-present`, `logo-present`, `crop-tightness ≥ 0.85`, `product-coverage-ratio ≥ 0.80` |
| **easy_to_detect** | true |
| **Rationale** | Border intersection (at least one edge touched) is a geometric invariant of all product detail close-ups and is detectable with very high confidence using salient-object bounds. Combined with `occlusion-level = closeup` and no human, this is a reliable and discriminating signal on CPU regardless of the specific detail type. |

---

### `packaging-shot`

| Field | Value |
|---|---|
| **Phenotype id** | `packaging-shot` |
| **Description** | Image primarily shows the product's packaging: box, bottle, blister pack, tube, bag. Product may be inside the packaging or shown alongside it. |
| **Required features** | `packaging-visible = true`, `hero-is-human = FALSE`, `occlusion-level = full-product OR mostly-visible` |
| **Optional features** | `text-present = true`, `logo-present = true`, `background-type = SOLIDCOLOR OR STUDIO` |
| **easy_to_detect** | true |
| **Rationale** | Packaging typically has visible text, logos, and rectangular/cylindrical hard-edge geometry. Text detection + rectangular contour analysis on CPU is reliable. |

---

### `lifestyle-hero`

| Field | Value |
|---|---|
| **Phenotype id** | `lifestyle-hero` |
| **Description** | Full product or model shot in a real-world or styled environment (not studio). The background tells a story or evokes a mood. Product is clearly visible and prominent. |
| **Required features** | `lifestyle-background = true`, `occlusion-level = full-product OR mostly-visible` |
| **Optional features** | `hero-is-human = TRUE OR FALSE`, `indoor = true OR outdoor = true`, `intersection-count ≤ 1` |
| **easy_to_detect** | true |
| **Rationale** | Lifestyle background detection (non-solid, scene-type background) is reliable with background segmentation + color diversity analysis on CPU. |

---

### `lifestyle-context`

| Field | Value |
|---|---|
| **Phenotype id** | `lifestyle-context` |
| **Description** | Catch-all for non-packshot images: any image showing the product in a real-world, marketing, or ambient context that does not qualify as a packshot phenotype. Generic marketing photographs qualify. The primary PRISM distinction is packshot-family (on-model, ghost, floating, packshot) vs. non-packshot — `lifestyle-context` is the residual class for all images with a lifestyle background that do not fit a more specific packshot phenotype. Product coverage may range from prominent to incidental. |
| **Required features** | `lifestyle-background = true` |
| **Optional features** | `hero-is-human = TRUE OR FALSE`, `occlusion-level` (any), `indoor = true OR outdoor = true`, `intersection-count ≥ 1` |
| **easy_to_detect** | false |
| **Rationale** | As a catch-all residual class, `lifestyle-context` is assigned when no other phenotype can be confidently asserted for an image with a lifestyle background. Lifestyle background detection is reliable; the assignment decision is driven by elimination rather than direct detection. |

---

### `scale-reference-shot`

| Field | Value |
|---|---|
| **Phenotype id** | `scale-reference-shot` |
| **Description** | Image where a known-size reference object (hand, ruler, coin, common household item) is shown alongside the product to indicate scale. |
| **Required features** | `scale-reference-present = true`, `occlusion-level = full-product OR mostly-visible` |
| **Optional features** | `hero-is-human = FALSE`, `background-type = SOLIDCOLOR OR STUDIO` |
| **easy_to_detect** | false |
| **Rationale** | Detecting a scale reference requires recognizing specific reference objects (hands, rulers). Hand detection is feasible on CPU but `scale-reference-present` has low expected confidence (see ImageFeatures.md). |

---

### `illustration-technical-drawing`

| Field | Value |
|---|---|
| **Phenotype id** | `illustration-technical-drawing` |
| **Description** | An image that is primarily a graphic, schematic, or synthetic composition rather than a photograph. Covers: technical drawings (assembly instructions, furniture flat-pack diagrams), EU energy labels, exploded-view compositions, multi-angle composites, vector drawings, icons, and badges. Always assigned the last configured DetOrder slot. |
| **Required features** | `hero-is-human = FALSE` |
| **Optional features** | `text-present = true`, `multiple-products = true`, `overlap-count = 0`, `white-background = true`, `logo-present = true` |
| **easy_to_detect** | false |
| **Rationale** | The boundary between a photograph and a graphic or schematic requires semantic understanding (vector lines, synthetic rendering, scale-drawing cues). On CPU without a specialized model, reliable detection requires classification labels from the CLIP model. |

---

### `model-detail-closeup`

| Field | Value |
|---|---|
| **Phenotype id** | `model-detail-closeup` |
| **Description** | Close-up on a human model showing a specific product area — e.g., shoe on foot, collar of shirt, cuff of jacket — with body partially in frame. |
| **Required features** | `hero-is-human = TRUE`, `occlusion-level = closeup OR partially-occluded`, `body-visible = bust OR none` |
| **Optional features** | `hero-orientation = FRONT OR SIDEON`, `intersects-bottom = true`, `face-visible = false` |
| **easy_to_detect** | false |
| **Rationale** | Partial human + product close-up is ambiguous between a detail shot and a partial on-model shot. Requires both human detection and occlusion level, both of which have medium confidence. |

---

### `sitting-on-model`

| Field | Value |
|---|---|
| **Phenotype id** | `sitting-on-model` |
| **Description** | Person wearing the product in a seated position. Common for jeans/trousers, casual wear, and home accessories. |
| **Required features** | `hero-is-human = TRUE`, `pose-type = sitting`, `occlusion-level = full-product OR mostly-visible` |
| **Optional features** | `hero-orientation = FRONT OR SIDEON`, `background-type = STUDIO OR REALLIFE` |
| **easy_to_detect** | false |
| **Rationale** | Sitting pose detection requires PAF pose estimation with confident hip/knee keypoint inference. Feasible on CPU but confidence is medium, especially when lower body is partially occluded. |

---

### `on-model-with-accessories`

| Field | Value |
|---|---|
| **Phenotype id** | `on-model-with-accessories` |
| **Description** | Person wearing the hero product styled with additional accessories (belt, bag, hat, jewelry). Hero product remains identifiable. |
| **Required features** | `hero-is-human = TRUE`, `multiple-products = true`, `occlusion-level = full-product OR mostly-visible` |
| **Optional features** | `background-type = STUDIO OR REALLIFE`, `body-visible = full OR three-quarter` |
| **easy_to_detect** | false |
| **Rationale** | Multi-product detection on a human model requires isolating the hero product from accessories — this requires object detection and product-type labeling. |

---

### `interior-shot`

| Field | Value |
|---|---|
| **Phenotype id** | `interior-shot` |
| **Description** | Inside view of a product — interior of a bag, inside of a shoe, inside of a box or case. |
| **Required features** | `hero-is-human = FALSE`, `hero-orientation = FRONT OR TOP`, `packaging-visible = false` |
| **Optional features** | `background-type = SOLIDCOLOR`, `occlusion-level = full-product` |
| **easy_to_detect** | false |
| **Rationale** | Interior shots require product-type-specific knowledge to identify as interior vs. exterior. No geometry-only signal is reliable. |

---

### `size-chart`

| Field | Value |
|---|---|
| **Phenotype id** | `size-chart` |
| **Description** | Image primarily containing a sizing guide, measurement chart, or fit guide table. |
| **Required features** | `text-present = true`, `hero-is-human = FALSE OR TRUE`, `product-coverage-ratio ≤ 0.30`, `image-occupancy ≥ 0.60` |
| **Optional features** | `logo-present = true`, `packaging-visible = false` |
| **easy_to_detect** | true |
| **Rationale** | High text density + tabular layout is reliably detected by OCR + grid-line detection on CPU. |

> **DetOrder note:** `size-chart` has no configured DetOrder slot. Images of this type are assigned after all configured det slots via deterministic fallback.

---

## Summary table

| Phenotype id | easy_to_detect |
|---|---|
| `front-packshot` | true |
| `back-packshot` | false |
| `side-packshot` | false |
| `diagonal-packshot` | false |
| `top-packshot` | true |
| `bottom-packshot` | false |
| `front-on-model-full-product` | true |
| `front-on-model-partial` | true |
| `back-on-model-full-product` | false |
| `side-on-model` | false |
| `ghost-front` | false |
| `ghost-back` | false |
| `ghost-side` | false |
| `flatlay-front` | true |
| `flatlay-styled` | false |
| `closeup-image` | true |
| `packaging-shot` | true |
| `lifestyle-hero` | true |
| `lifestyle-context` | false |
| `scale-reference-shot` | false |
| `illustration-technical-drawing` | false |
| `model-detail-closeup` | false |
| `sitting-on-model` | false |
| `on-model-with-accessories` | false |
| `interior-shot` | false |
| `size-chart` | true |

**Total phenotypes: 26**
Easily detectable on CPU: 9 / 26 (35%)

---

## Architecture decisions

- **`ghost-front` vs. `front-packshot`**: Phenotype disambiguation is determined by product type, not by a dedicated 3D-structure ImageFeature. No additional feature needed.
- **`detail-*` consolidation**: All close-up product-detail phenotypes (material, stitching, label, hardware) are consolidated into `closeup-image`. `model-detail-closeup` is retained separately because it requires `has-human` or skin-tone evidence. The distinguishing invariant for `closeup-image` is border intersection (`intersection-count ≥ 1`).
- **Phenotype assignment**: Always a hard assignment. No soft probability vectors or confidence-weighted phenotype scoring.
- **`lifestyle-context`**: Catch-all for non-packshot images. Generic marketing photographs qualify. Assigned by elimination when `lifestyle-background = true` and no packshot phenotype fits.
- **`size-chart`**: No configured DetOrder slot. Assigned to deterministic fallback after all configured det slots.
- **`illustration-technical-drawing`**: Always assigned the last configured DetOrder slot, regardless of product type.
