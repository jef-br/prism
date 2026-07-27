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
| **Optional features** | `white-background = true` |
| **easy_to_detect** | true |
| **Rationale** | Solid/white background + no human + front symmetry + full product in frame is a highly distinctive and common combination. Symmetry score + background detection + intersection check are all reliable on CPU. |

---

### `back-packshot`

| Field | Value |
|---|---|
| **Phenotype id** | `back-packshot` |
| **Description** | Hero product shot photographed from the rear on a solid-color or transparent background; no human present. |
| **Required features** | `hero-is-human = FALSE`, `hero-orientation = BACK`, `background-type = SOLIDCOLOR OR clipping-path = true`, `occlusion-level = full-product`, `intersection-count = 0` |
| **Optional features** | `white-background = true` |
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
| **Optional features** | `white-background = true` |
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
| **Optional features** | `background-type = SOLIDCOLOR` |
| **easy_to_detect** | true |
| **Rationale** | Combination of human present + front symmetry + full body + head present is highly distinctive. Human and edge-intersection detection are both feasible on CPU for this common configuration. |

---

### `front-on-model-partial`

| Field | Value |
|---|---|
| **Phenotype id** | `front-on-model-partial` |
| **Description** | A real person photographed from the front but with body partially out of frame (e.g., cropped below the knee, or bust-only shot). |
| **Required features** | `hero-is-human = TRUE`, `hero-orientation = FRONT`, `body-visible = three-quarter OR half OR bust`, `intersects-bottom = true OR intersects-top = true` |
| **Optional features** | `head-visible = FULL OR PARTIAL`, `background-type = SOLIDCOLOR` |
| **easy_to_detect** | true |
| **Rationale** | Border intersection clearly signals cropping; combined with human and front detection this is reliable. |

---

### `back-on-model-full-product`

| Field | Value |
|---|---|
| **Phenotype id** | `back-on-model-full-product` |
| **Description** | A real person wearing the product, photographed from the rear, full body visible. Shows product back details (labels, closures, back-print). |
| **Required features** | `hero-is-human = TRUE`, `hero-orientation = BACK`, `body-visible = full`, `head-visible = NONE OR PARTIAL`, `intersection-count = 0` |
| **Optional features** | `background-type = SOLIDCOLOR` |
| **easy_to_detect** | false |
| **Rationale** | BACK orientation with a human requires detecting that the person is facing away — back-of-head vs. face, shoulder asymmetry. |

---

### `side-on-model`

| Field | Value |
|---|---|
| **Phenotype id** | `side-on-model` |
| **Description** | Person wearing the product photographed from the side. Common for showing silhouette, fit, and sleeve/leg length. |
| **Required features** | `hero-is-human = TRUE`, `hero-orientation = SIDEON`, `body-visible = full OR three-quarter` |
| **Optional features** | `background-type = SOLIDCOLOR`, `head-visible = PARTIAL OR FULL` |
| **easy_to_detect** | false |
| **Rationale** | Side-on human silhouette is detectable but SIDEON vs. DIAGONAL requires confidence in the exact angle. |

---

### `ghost-front`

| Field | Value |
|---|---|
| **Phenotype id** | `ghost-front` |
| **Description** | Product photographed on an invisible mannequin (ghost mannequin / hollow-man technique) from the front. Garment appears to be worn but no human is present. |
| **Required features** | `hero-is-human = FALSE`, `hero-orientation = FRONT`, `background-type = SOLIDCOLOR OR clipping-path = true`, `occlusion-level = full-product`, `intersection-count = 0` |
| **Optional features** | `white-background = true` |
| **easy_to_detect** | false |
| **Rationale** | Ghost shot looks like a packshot with a 3D structured garment. No feature currently distinguishes a ghost shot from a flat packshot when `background-type = SOLIDCOLOR` (the `contains-mannequin` feature that would have disambiguated it was removed in T-4700 — its sole producer was a stub); `ghost-front` remains reachable only via its other branch (`clipping-path = true` with a non-solid background). See the architecture note below — this ambiguity is accepted by design, not a defect. |

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

### `closeup-image`

| Field | Value |
|---|---|
| **Phenotype id** | `closeup-image` |
| **Description** | Close-up of any product detail — fabric, texture, stitching, hardware, label, tag, or component — where the detail fills most or all of the frame and the image content intersects with at least one image border. No human present. Product-detail purpose only. Consolidates all close-up detail phenotypes (material, stitching, label, hardware). |
| **Required features** | `hero-is-human = FALSE`, `intersection-count ≥ 1`, `occlusion-level = closeup` |
| **Optional features** | `crop-tightness ≥ 0.85`, `product-coverage-ratio ≥ 0.80` |
| **easy_to_detect** | true |
| **Rationale** | Border intersection (at least one edge touched) is a geometric invariant of all product detail close-ups and is detectable with very high confidence using salient-object bounds. Combined with `occlusion-level = closeup` and no human, this is a reliable and discriminating signal on CPU regardless of the specific detail type. |

---

### `lifestyle-hero`

| Field | Value |
|---|---|
| **Phenotype id** | `lifestyle-hero` |
| **Description** | Full product or model shot in a real-world or styled environment (not studio). The background tells a story or evokes a mood. Product is clearly visible and prominent. |
| **Required features** | `lifestyle-background = true`, `occlusion-level = full-product OR mostly-visible` |
| **Optional features** | `hero-is-human = TRUE OR FALSE`, `intersection-count ≤ 1` |
| **easy_to_detect** | true |
| **Rationale** | Lifestyle background detection (non-solid, scene-type background) is reliable with background segmentation + color diversity analysis on CPU. |

---

### `lifestyle-context`

| Field | Value |
|---|---|
| **Phenotype id** | `lifestyle-context` |
| **Description** | Catch-all for non-packshot images: any image showing the product in a real-world, marketing, or ambient context that does not qualify as a packshot phenotype. Generic marketing photographs qualify. The primary PRISM distinction is packshot-family (on-model, ghost, floating, packshot) vs. non-packshot — `lifestyle-context` is the residual class for all images with a lifestyle background that do not fit a more specific packshot phenotype. Product coverage may range from prominent to incidental. |
| **Required features** | `lifestyle-background = true` |
| **Optional features** | `hero-is-human = TRUE OR FALSE`, `occlusion-level` (any), `intersection-count ≥ 1` |
| **easy_to_detect** | false |
| **Rationale** | As a catch-all residual class, `lifestyle-context` is assigned when no other phenotype can be confidently asserted for an image with a lifestyle background. Lifestyle background detection is reliable; the assignment decision is driven by elimination rather than direct detection. |

---

### `illustration-technical-drawing`

| Field | Value |
|---|---|
| **Phenotype id** | `illustration-technical-drawing` |
| **Description** | An image that is primarily a graphic, schematic, or synthetic composition rather than a photograph. Covers: technical drawings (assembly instructions, furniture flat-pack diagrams), EU energy labels, exploded-view compositions, multi-angle composites, vector drawings, icons, and badges. Always assigned the last configured DetOrder slot. |
| **Required features** | `hero-is-human = FALSE` |
| **Optional features** | `multiple-products = true`, `overlap-count = 0`, `white-background = true` |
| **easy_to_detect** | false |
| **Rationale** | The boundary between a photograph and a graphic or schematic requires semantic understanding (vector lines, synthetic rendering, scale-drawing cues). On CPU without a specialized model, reliable detection requires classification labels from the CLIP model. |

---

### `model-detail-closeup`

| Field | Value |
|---|---|
| **Phenotype id** | `model-detail-closeup` |
| **Description** | Close-up on a human model showing a specific product area — e.g., shoe on foot, collar of shirt, cuff of jacket — with body partially in frame. |
| **Required features** | `hero-is-human = TRUE`, `occlusion-level = closeup OR partially-occluded`, `body-visible = bust OR none` |
| **Optional features** | `hero-orientation = FRONT OR SIDEON`, `intersects-bottom = true` |
| **easy_to_detect** | false |
| **Rationale** | Partial human + product close-up is ambiguous between a detail shot and a partial on-model shot. Requires both human detection and occlusion level, both of which have medium confidence. |

---

### `on-model-with-accessories`

| Field | Value |
|---|---|
| **Phenotype id** | `on-model-with-accessories` |
| **Description** | Person wearing the hero product styled with additional accessories (belt, bag, hat, jewelry). Hero product remains identifiable. |
| **Required features** | `hero-is-human = TRUE`, `multiple-products = true`, `occlusion-level = full-product OR mostly-visible` |
| **Optional features** | `background-type = REALLIFE`, `body-visible = full OR three-quarter` |
| **easy_to_detect** | false |
| **Rationale** | Multi-product detection on a human model requires isolating the hero product from accessories — this requires object detection and product-type labeling. |

---

### `interior-shot`

| Field | Value |
|---|---|
| **Phenotype id** | `interior-shot` |
| **Description** | Inside view of a product — interior of a bag, inside of a shoe, inside of a box or case. |
| **Required features** | `hero-is-human = FALSE`, `interior-detected = true` |
| **Optional features** | `background-type = SOLIDCOLOR`, `occlusion-level = full-product` |
| **easy_to_detect** | false |
| **Rationale** | Detected by `Analyzer_Interior.cs` via Sobel-edge boundary analysis (enclosed region smoother than surrounding texture, bounded by strong edges). Product-type gating (wallet/bag/suitcase) applied at the Order stage. |

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
| `closeup-image` | true |
| `lifestyle-hero` | true |
| `lifestyle-context` | false |
| `illustration-technical-drawing` | false |
| `model-detail-closeup` | false |
| `on-model-with-accessories` | false |
| `interior-shot` | false |

**Total phenotypes: 20**
Easily detectable on CPU: 6 / 20 (30%)

---

## Architecture decisions

- **`ghost-front` vs. `front-packshot`**: Phenotype disambiguation is determined by product type, not by a dedicated 3D-structure ImageFeature. No additional feature needed.
- **`detail-*` consolidation**: All close-up product-detail phenotypes (material, stitching, label, hardware) are consolidated into `closeup-image`. `model-detail-closeup` is retained separately because it requires `has-human` or skin-tone evidence. The distinguishing invariant for `closeup-image` is border intersection (`intersection-count ≥ 1`).
- **Phenotype assignment**: Always a hard assignment. No soft probability vectors or confidence-weighted phenotype scoring.
- **`lifestyle-context`**: Catch-all for non-packshot images. Generic marketing photographs qualify. Assigned by elimination when `lifestyle-background = true` and no packshot phenotype fits.
- **`illustration-technical-drawing`**: Always assigned the last configured DetOrder slot, regardless of product type.
- **T-4700 (2026-07-27)**: removed `sitting-on-model`, `packaging-shot`, `size-chart`, `scale-reference-shot`, `flatlay-front`, `flatlay-styled` — every hard-required condition on each depended solely on a feature whose only producer was an empty-body analyzer stub, making them mathematically unreachable (`PhenotypeRuleSet` never treats `UNKNOWN` as satisfying a `required` condition). Removed alongside them: `pose-type`, `camera-angle`, `top-view`, `text-present`, `logo-present`, `packaging-visible`, `scale-reference-present`, `contains-mannequin`, and 15 other stub-only or structurally-dead features/enum values (see `ImageFeatures.md`). Re-introduction is gated on a reliable DetOrderRules catch-all proving out first — see `jb/src/core/Services/Matching/Analyzers/jbtodo.md`'s "Removed" section for what each stub would have produced.
