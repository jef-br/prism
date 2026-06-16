# Objective

Further develop the **NGP Classification** concept.

**Scope constraint:** Do **not** expand the concept beyond its current purpose:

> Map images to a `DetOrder` for a given `ProductType` using enrichment, feature extraction, and deterministic or near-deterministic filtering/classification.

The goal is image ordering and classification, not general image understanding, recommendation systems, tagging platforms, DAM systems, or broader AI initiatives.

# Starting Point

Read and analyze:

`C:\Users\JefB\Documents\JBGITROOT\prism\.claude\agents\domain-expert.chatmode.md`

Start at **line 24**.

# Documentation Tasks

## 1. Capture Simplified Insights

Whenever you discover something important that can be explained to a 10-year-old, append it to:

`jb/docs/ideas-on-NGP.md`

Use simple language and practical examples.

---

## 2. Create Product Type Catalog

Create:

`jb/docs/ImageNGP/PRODUCTTYPES.md`

List common e-commerce product types, including but not limited to clothing, fmcg, gardening stuff, diy stuff,
Use a structure suitable for later machine-readable classification.

---

## 3. Define Expected Image Ordering per Product Type

For each ProductType, document the expected image sequence as typically found on e-commerce websites.
Per product type, there are 8 images.
Each position can be filled with one or more ImagePhenotypes. For example the first picture is likely a frontal view but is equally likely to be  a ghost, on-model, or flat-lay image.

Preferences:
   - order from "human" to "artificial". Ie: model first > ghost model > flat-lay > clipping path > render
   - occlusion: full view > partial view > closeup
   - background: natural and flat (studio) > flat > ad/ambiance/lifestyle/marketing background > clipping path
   - ...


`jb/docs/ImageNGP/PRODUCTTYPES.md`

For every ProductType, describe expected image ordering using the existing image taxonomy and include:

### Orientation

Examples:

* front
* front-3-quarter
* side
* rear
* top
* bottom
* interior
* detail-view

### Occlusion

Examples:

* full-product
* mostly-visible
* partially-occluded

### Presentation Style

Examples:

* clipping-path
* on-ghost
* on-model
* flat-lay
* packshot
* detail
* ambiance
* lifestyle
* scale-reference
* exploded-view

Represent image types in kebab-case.

Example:

```text
t-shirt

1. front-on-ghost-full-product
2. back-on-ghost-full-product
3. front-on-model-full-product
4. side-on-model-full-product
5. detail-fabric-closeup
6. detail-print-closeup
```

Document realistic ordering conventions used by major e-commerce retailers.

---

## 4. Create Image Feature Catalog

Create:

`jb/docs/ImageNGP/ImageFeatures.md`

Build a comprehensive table of image features.

Include existing documented features plus additional useful features.

Examples:

* has-human
* human-count
* has-head
* has-face
* face-visible
* body-visible
* orientation
* front-view
* side-view
* rear-view
* top-view
* product-coverage-ratio
* image-occupancy
* background-type
* clipping-path
* white-background
* transparent-background
* lifestyle-background
* indoor
* outdoor
* shadow-present
* reflection-present
* crop-tightness
* symmetry-score
* occlusion-level
* overlap-count
* intersection-count
* dominant-colors
* text-present
* logo-present
* packaging-visible
* multiple-products
* scale-reference-present

For each feature include:

* description
* datatype
* possible values
* extraction difficulty
* expected confidence

---

## 5. Define Image Phenotypes

Create:

`jb/docs/ImageNGP/imagePhenotypes.md`

List all sensible image phenotypes that can occur in e-commerce product imagery.

Examples:

* front-packshot
* back-packshot
* front-on-model
* side-on-model
* ghost-front
* ghost-back
* flatlay-front
* detail-material
* detail-stitching
* lifestyle-hero
* packaging-shot
* scale-reference-shot

For each phenotype include:

* phenotype id
* description
* required feature combinations
* optional feature combinations

Also add:

| Field          | Description                         |
| -------------- | ----------------------------------- |
| easy_to_detect | Boolean                             |
| rationale      | Why detection is or is not reliable |

Determine whether the phenotype can be detected with very high confidence using:

* OpenCV
* image segmentation
* object detection
* image classification
* geometric analysis

Assume:

* no GPU
* no external SaaS
* no proprietary third-party vision systems

---

## 6. Design the Mapping Model

Propose how the following relationship should be modeled:

```text
ImageFeatures
        ↓
Phenotype
        ↓
(ProductType, DetOrder)
```

Evaluate alternative representations:

* lookup tables
* rule engine
* graph model
* tensor representation
* sparse tensor representation
* probabilistic model

Recommend one architecture.

Provide:

* rationale
* storage model
* query model
* maintainability considerations

Focus on determinism, explainability, and maintainability.

---

## 7. Design the Feature Detection Architecture

Design a software architecture for feature extraction.

Describe:

### Detection Pipeline

Examples:

```text
image
 → preprocessing
 → segmentation
 → low-level feature extraction
 → object detection
 → phenotype classification
 → DetOrder assignment
```

### Detector Types

Examples:

* geometric detectors
* segmentation detectors
* background detectors
* human detectors
* pose detectors
* orientation detectors
* OCR detectors

For each detector document:

* inputs
* outputs
* dependencies
* confidence generation
* performance considerations

Prioritize CPU-based execution.

---

## 8. Visual Workbench Concept

Design a visualization system for exploring all images belonging to a single `FamilyID`.

Investigate representing:

```text
FamilyID
  ├─ Image Tensor
  ├─ Image Tensor
  ├─ Image Tensor
  └─ Image Tensor
```

Each image should expose:

* extracted features
* phenotype
* confidence values
* assigned DetOrder
* ProductType

Explore whether a:

* Tanner graph
* bipartite graph
* factor graph
* tensor visualization
* hypergraph

is the most appropriate representation.

Provide:

* conceptual model
* node types
* edge types
* interaction model
* filtering possibilities
* debugging value

The objective is to allow rapid inspection and debugging of image ordering decisions across all images within a FamilyID.

# Deliverables

Produce or update:

* `jb/docs/ideas-on-NGP.md`
* `jb/docs/ImageNGP/PRODUCTTYPES.md`
* `jb/docs/ImageNGP/ImageFeatures.md`
* `jb/docs/ImageNGP/imagePhenotypes.md`

Additionally provide:

* architecture recommendations
* data model recommendations
* visualization/workbench design recommendations

Keep all proposals aligned with the original NGP goal of deterministic image classification and DetOrder assignment.