# NGP Classification — Architecture Recommendations

This document covers deliverables 6–8 of the NGP concept exercise (`jb-new-objectives.md`):
the **mapping model**, the **feature-detection architecture**, and the **visual workbench concept**.
It builds directly on the catalogs in this folder:

- [`ImageFeatures.md`](ImageFeatures.md) — 40 measurable features
- [`imagePhenotypes.md`](imagePhenotypes.md) — 30 compound phenotypes (10 confidently CPU-detectable)
- [`PRODUCTTYPES.md`](PRODUCTTYPES.md) — 18 product types, 8-slot DetOrder each
- [`ideas-on-NGP.md`](ideas-on-NGP.md) — plain-language framing

> **Scope reminder.** This is a *concept-development* exercise. Nothing here changes project code or
> config. All proposals (new features, new config files, new rule keywords) are recommendations to be
> ratified later through the normal `jbtodo.md` → `jb/docs` decision flow. The guiding constraints are
> unchanged: **deterministic, explainable, maintainable, CPU-only**, and limited to mapping images to a
> `DetOrder` for a given `ProductType`.

---

## Part 0 — The three-layer relationship, restated

```
ImageFeatures          (40 measured attributes, each with confidence)
      │   Layer A:  predicate / rule evaluation
      ▼
Phenotype              (named compound image type, scored 0..1 per phenotype)
      │   Layer B:  (ProductType, DetSlot) preference lookup + assignment
      ▼
(ProductType, DetOrder)   (det0..detN slot assignment for the FamilyID)
```

Two distinct mappings live here, and conflating them is the main design risk:

- **Layer A — Features → Phenotype.** Many-features-to-one-phenotype. Deterministic predicates ("all
  required features match"). This is the same shape as the existing **ImageRole qualification rule**
  documented in `jb/ticketboard/AGENTFEEDBACK.md` ("an image qualifies for an ImageRole only when all required
  ImageFeature states match").
- **Layer B — (ProductType, Phenotype) → DetSlot.** This is a *competition*: every image in a FamilyID
  competes for a fixed number of slots, and each slot has an ordered preference list of acceptable
  phenotypes. This is the same shape as the existing `DetOrderByNGP` preference lists.

Keeping them separate is what lets the same 40 features feed 18 product types without rewriting
detectors — the decoupling insight already captured in `ideas-on-NGP.md` Insight 6.

---

## Part 1 (Deliverable 6) — The Mapping Model

### 1.1 Evaluation of candidate representations

| Representation | Determinism | Explainability | Handles soft/ambiguous evidence | Maintainability | Verdict |
|---|---|---|---|---|---|
| **Flat lookup table** (feature-tuple → phenotype → slot) | High | High | Poor — needs an exact key; 40 features explode the key space | Poor at scale | Rejected as the whole model; kept as the *slot-preference* substructure |
| **Rule engine** (declarative predicates) | High | **Very high** — each firing rule is a human-readable reason | Medium — predicates are crisp, but you can attach confidence | High (edit rules, not code) | **Core of Layer A** |
| **Graph model** | Medium | High (visual) | Medium | Medium | Excellent for the *workbench* (Part 3), heavy as the runtime store |
| **Dense tensor** (ProductType × Slot × Phenotype × Feature) | High | Medium | High | Poor — mostly zeros, hard to hand-edit | Rejected (wasteful: ~18×8×30×40) |
| **Sparse tensor** (only non-zero preference/requirement entries) | High | Medium-High | High | High (few entries, diffable) | **Core of Layer B storage** |
| **Probabilistic model** (e.g. Bayesian net) | **Low** (sampling / learned weights) | Low | High | Low (needs training data, hard to audit) | Rejected — violates determinism + explainability |

### 1.2 Recommended architecture: **Sparse Rule-Tensor with deterministic assignment**

A hybrid that takes the best of three rows above. It has three concrete pieces:

**(a) Layer A — a declarative predicate table (`rule engine`).**
Each phenotype is a row of required + optional feature predicates (exactly the format already in
`imagePhenotypes.md`). Evaluation produces, per image, a **phenotype score vector** rather than a single
hard label:

```
phenotypeScore[p] = Π(required predicates satisfied ? feature.confidence : 0)
                    × bonus(optional predicates satisfied)
```

If any required predicate is `UNKNOWN` or unmet, the phenotype scores 0 for that image — deterministic,
and the reason is inspectable (which predicate failed). This directly resolves the
**hard-vs-soft open question**: *soft internally, deterministic externally*. The same features + same
config always yield the same vector; nothing is sampled.

**(b) Layer B — a sparse preference tensor `W[ProductType, DetSlot, Phenotype] → rank`.**
Only the allowed (phenotype, slot) pairs for a product type are stored, each with a preference rank.
This is literally the `PRODUCTTYPES.md` content in machine form, and it generalizes the existing
`DetOrderByNGP`. Storage is sparse because each slot lists 1–4 phenotypes, not all 30.

**(c) Assignment — a deterministic constrained solver.**
A FamilyID has N images and K det slots. Each (image, slot) pair gets a cost:

```
cost(image, slot) = f( slotPreferenceRank[productType, slot, phenotype],
                       phenotypeScore[image, phenotype],
                       universalOrderingTier(phenotype) )   // human→artificial, occlusion, background
```

Solve as a **minimum-cost assignment** (Hungarian algorithm, or a greedy slot-priority pass for V1).
This guarantees each slot is filled by a *distinct* image and removes the "same image claims two slots"
risk surfaced in the FMCG open question. Ties break by the existing rule already in `jb/ticketboard/AGENTFEEDBACK.md`:
role confidence → compatible `_det#` filename hints → stable import index. Leftover images go to fallback
slots after the configured ones (never dropped), matching current policy.

### 1.3 Why this wins on the three required axes

- **Determinism.** No learned weights, no sampling. Predicate evaluation and min-cost assignment are
  pure functions of (features, config). Re-running yields identical `_det` output.
- **Explainability.** Every decision has a trace: *which* features fed *which* phenotype score, *which*
  slot preference it matched, and *why* it won or lost the assignment (cost comparison). This trace is
  exactly what the Part 3 workbench renders.
- **Maintainability.** All three pieces are JSON edited by a domain expert, not code. Adding a product
  type = new sparse-tensor rows. Adjusting "back view should rank above side for jeans" = one rank edit.
  Detectors never change when ordering policy changes.

### 1.4 Storage model (proposed config files — not yet created)

| File | Holds | Shape |
|---|---|---|
| `ImageFeatures.json` | feature ids, datatypes, allowed states, threshold | flat list (already planned in `jb/ticketboard/AGENTFEEDBACK.md`) |
| `Phenotypes.json` | per-phenotype required/optional predicates + scoring weights | rule table (Layer A) |
| `DetOrderRules.json` *(extended)* | `ProductType → DetSlot → ordered phenotype/keyword list` | sparse preference tensor (Layer B) |
| `RoleKeywordPhenotypes.json` *(new, proposed)* | maps human slot keywords (`pack`, `label`, `material`) → allowed phenotype sets | thin lookup layer |

This keeps the **keyword abstraction** the team already uses (`front`, `pack`, `label`) while binding
keywords to concrete phenotypes through one indirection table — resolving that open question without
forcing phenotype ids into `DetOrderRules.json` and breaking the existing file.

### 1.5 Resolved open questions (carried from the catalogs)

| Question | Recommendation |
|---|---|
| Hard label vs soft phenotype vector | **Soft scores internally, deterministic assignment externally.** Store the full vector for explainability. |
| `ProductType` source | **Excel/metadata is authoritative.** `product-type-label` feature is a *fallback only* when metadata is absent, and may never override metadata. Keeps PRISM metadata-driven. |
| Keyword vs phenotype id in rules | **Keep keywords**, add `RoleKeywordPhenotypes.json` indirection. |
| `ghost-front` vs `front-packshot` ambiguity | **Add a `3d-structure-score` feature** (fold/collar/neckline depth cue) to make the split deterministic. Until then, both collapse to a single `front-clean-background` class for assignment. |
| `detail-stitching` vs `detail-material` | **Treat as one assignment class `detail-textile`** (they target the same slots). Keep both ids documented for future finer detection. |
| `exploded`/`composite` slots | **Add `exploded` and `composite` as new role keywords** mapped to `exploded-view` / `multi-angle-composite`. |
| `size-chart`, `multi-angle-composite` are product-independent | **Model as product-type-independent optional slots** appended after configured slots in every rule set. |
| `furniture` det0 prefers lifestyle (inverts human→artificial) | **Allow per-product-type preference overrides** in the sparse tensor — the rank is per (ProductType, Slot), so this is already expressible without a special case. |

---

## Part 2 (Deliverable 7) — Feature Detection Architecture

### 2.1 Design principle: cheap-first, gate-expensive

The catalog shows the cost gradient clearly: **6 edge/intersection + several geometric/background
features are `low` difficulty (classical OpenCV)**, while human/pose/OCR/classification are `medium`–
`high`. On CPU the winning strategy is to **run the cheap detectors unconditionally and use their output
to gate the expensive ones**. Example: don't run PAF pose estimation unless `skin-tone-area` exceeds
`MinimumSkinToneArea`; don't run OCR unless a cheap text-density pass fires.

### 2.2 Detection pipeline

```
image
 → preprocessing            (decode, EXIF-orient, downscale-for-analysis, alpha split)
 → salient-object + segmentation   (one foreground/background mask, reused everywhere)
 → low-level geometric pass (intersections, bbox, centering, aspect, occupancy, crop-tightness)
 → background pass          (solid/studio/lifestyle, white, shadow)
 → [gate] human pass        (skin histogram → PAF skeleton → head/face)   if skin-area gate passes
 → [gate] content pass      (OCR text, logo, packaging, color quantization) if cheap signals fire
 → [gate] classifier pass   (MobileNet/CLIP-class ONNX) for product-type-label, ambiguous orientation
 → phenotype scoring        (Layer A predicate evaluation → score vector)
 → DetOrder assignment      (Layer B sparse tensor + min-cost assignment, per FamilyID)
```

Everything before "phenotype scoring" runs **per image** in the `Classified` stage; assignment runs
**per FamilyID** in the `Ordered` stage. This respects the immutable pipeline order
(Imported → Classified → Matched → Ordered → …).

### 2.3 Detector catalog

| Detector | Inputs | Outputs (features) | Dependencies | Confidence generation | Performance (CPU) |
|---|---|---|---|---|---|
| **Preprocessor** | raw bytes | normalized BGR Mat, alpha mask, EXIF orientation | OpenCV | n/a (deterministic) | very cheap |
| **Salient/segmentation** | Mat | `salient-bbox`, foreground mask | OpenCV (saliency / GrabCut) or tiny U²-Net-lite ONNX | mask area stability, edge contrast | cheap–medium; **computed once, reused** |
| **Geometric** | bbox, mask | `intersects-*`, `intersection-count`, `fully-in-frame`, `aspect-ratio`, `product-aspect-ratio`, `*-centering`, `crop-tightness`, `image-occupancy`, `symmetry-score` | mask only | edge/line strength (Hough), bbox stability | **very cheap, very-high confidence** |
| **Background** | Mat, mask | `background-type`, `white-background`, `transparent-background`, `lifestyle-background`, `shadow-present`, `background-color` | color histogram, variance | color-cluster purity | cheap, high confidence |
| **Skin/human** | Mat | `skin-tone-area`, `has-human`, `hero-is-human` | multi-space skin histogram | skin-area fraction vs threshold | cheap (gate for pose) |
| **Pose (PAF)** | Mat, skin regions, intersections | `has-head` (partial), `body-visible`, `pose-type`, refined `hero-orientation` for humans | lightweight PAF ONNX | keypoint affinity strength | **medium–high cost; gated** |
| **Head/face** | top-half Mat, skeleton scale | `has-head`, `head-visible`, `has-face`, `face-visible` | Haar/LBP or tiny face ONNX; KGWRCM kernel | detector score + skeleton corroboration | medium; gated by skeleton |
| **OCR/text** | Mat, mask | `text-present`, contributes to `detail-label`, `size-chart`, `packaging-shot` | Tesseract (CPU) | mean word confidence | medium; **gated by text-density pre-pass** |
| **Logo/packaging** | Mat, contours | `logo-present`, `packaging-visible` | contour + template / tiny ONNX | template/match score | medium |
| **Color** | Mat (fg only) | `dominant-colors`, `product-color` | k-means in LAB space | cluster compactness | cheap |
| **Classifier (CLIP/MobileNet)** | Mat | `product-type-label`, orientation tie-break, `material-texture-visible` | `ImageClassifier.cs` ONNX boundary | softmax margin | **highest cost; gated / last resort** |

### 2.4 Confidence and UNKNOWN

Every detector emits a numeric confidence `double`; the boolean/enum value is derived by comparing to
`Classification.Confidence_Threshold` (currently `0.9`). Below threshold → `UNKNOWN`, never a guessed
default (Insight 5). `UNKNOWN` on a *required* phenotype predicate zeroes that phenotype and is surfaced
in the manifest. `UNKNOWN` on a *transform-critical* feature routes to `Tx_ProblemImageProcessor.cs`.

### 2.5 Performance posture

- One segmentation mask is computed once and **shared** by geometric, background, color, and crop
  detectors — the single most important CPU optimization.
- Heavy detectors (pose, OCR, classifier) are **gated and short-circuiting**: a clean white-background
  full-product image with no skin and no text never invokes any ONNX model and resolves to
  `front-packshot` / `top-packshot` from geometry alone — which is why **10/30 phenotypes are confidently
  CPU-detectable**.
- ONNX sessions are application-scoped, created at import time, owned by `ImageClassifier.cs` (never
  instantiated mid-pipeline).

---

## Part 3 (Deliverable 8) — Visual Workbench Concept

### 3.1 Goal

Let a human rapidly inspect and debug *why* every image in a single `FamilyID` got the phenotype and the
`_det` slot it did — across all images at once.

### 3.2 Representation evaluation

| Candidate | Fit for this goal |
|---|---|
| **Tensor visualization** (heatmap) | Good *secondary* view: image × phenotype score matrix as a heatmap. Shows scores, not *reasons*. |
| **Bipartite graph** | Natural for the *final* step: Images ↔ DetSlots assignment. But hides the feature→phenotype reasoning. |
| **Hypergraph** | A phenotype rule genuinely is a hyperedge over several features — expressive, but hard to read/lay out. |
| **Tanner graph** | Bipartite variable/check structure; close, but lacks an explicit "rule" semantics. |
| **Factor graph** | **Best fit.** Variable nodes (features, phenotypes, slots) connected through **factor nodes that *are* the rules**. Each factor renders the exact predicate that fired and with what confidence. |

### 3.3 Recommendation: a **layered factor graph**, with a bipartite assignment sub-view

The factor graph makes the deterministic rules first-class visual objects, which is precisely what makes
ordering decisions debuggable. Lay it out in four columns per FamilyID:

```
[ Images ]--(feature factors)-->[ Features ]--(phenotype rule factors)-->[ Phenotypes ]--(slot pref factors)-->[ DetSlots ]
```

**Node types**
- `ImageNode` — thumbnail + provenance + ProductType.
- `FeatureNode` — one measured feature value + confidence (color-coded; `UNKNOWN` flagged).
- `FactorNode (rule)` — a phenotype's required/optional predicate set; shows satisfied vs failed inputs.
- `PhenotypeNode` — phenotype id + computed score for that image.
- `SlotNode` — `det0..detN` with the winning image and the runner-up.

**Edge types**
- image→feature (measurement, weighted by confidence),
- feature→rule-factor (predicate input; green satisfied / red failed / grey unknown),
- rule-factor→phenotype (score contribution),
- phenotype→slot (preference rank), and the **bold assignment edge** = the image that won the slot.

**Interaction model**
- Click a `SlotNode` → highlight the winning path back to source image, plus the losing candidates and
  the cost delta that decided it (the assignment explanation).
- Click a `PhenotypeNode` that scored 0 → the failing required predicate lights up red ("orientation =
  UNKNOWN" / "intersection-count = 2").
- Hover a `FeatureNode` → the analyzer that produced it and the raw confidence.

**Filtering**
- by det slot, by phenotype, by `easy_to_detect`, by confidence band, by `UNKNOWN`-only (triage view),
  by "contested slots" (where the cost margin between candidates is small — the most error-prone cases).

**Debugging value**
- Instantly answers the three questions operators actually ask: *Why is this image in det3? Why did the
  obvious hero NOT win det0? Why does this image have no slot?* — each is a highlighted path or a red
  predicate.
- The **tensor heatmap** (image × phenotype score) is offered as a companion compact view for families
  with many images, where the full graph is dense.

This dovetails with the workbench rule (`PRISM-workbench.md`): the view is a **decorator over the same
`MatchEvidence` / `ImageRecord_LAMBDA` data** the pipeline already emits — it introduces no hidden
pipeline behavior, only visibility.

---

## Summary of recommendations

1. **Mapping model:** Sparse Rule-Tensor — declarative predicate table (Features→Phenotype, soft scores)
   + sparse preference tensor ((ProductType,Slot)→Phenotype rank) + deterministic min-cost assignment.
2. **Determinism preserved** by soft-score-then-solve; **explainability** by full per-decision trace;
   **maintainability** by JSON-only edits.
3. **Detection architecture:** cheap-first / gate-expensive CPU pipeline over one shared segmentation
   mask; ONNX gated and owned by `ImageClassifier.cs`.
4. **Workbench:** layered factor graph (with bipartite assignment sub-view and a tensor heatmap
   companion), rendered from existing evidence records.
5. **Proposed (not-yet-ratified) additions:** `3d-structure-score` feature; `RoleKeywordPhenotypes.json`
   indirection; `exploded`/`composite` slot keywords; product-type-independent `size-chart` /
   `multi-angle-composite` slots; per-(ProductType,Slot) preference overrides.

All of the above stay inside the original NGP goal: deterministic image classification and DetOrder
assignment. None expands NGP into general image understanding, tagging, or recommendation.
