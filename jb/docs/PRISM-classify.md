# PRISM — Image Classification

## When Classification Runs

Classification runs during the **`Classified`** stage for **every canonical image** after import normalization and visual-hash deduplication.

Labels are part of the definitive pipeline route and are available before matching, ordering, generation, transformation, and export. Classification is not a fallback — it runs unconditionally.

---

## ImageFeature and ImageNGP

Classification and purpose-built analyzers produce **ImageFeatures**. An ImageFeature is one measured image attribute with source and confidence, such as product type, orientation, background state, edge intersections, human presence, head visibility, skin-color evidence, object bounds, or visual labels.

`ImageNGP` is the image phenotype derived from a combination of ImageFeatures. It is not a single trait list such as `TypeOfShot`; examples of the intended phenotype shape are `PAP_FRONT`, `JEANS_GHOST_FRONT`, `JEANS_BUTT`, and `JEANS_GHOST_DETAIL`.

`TypeOfShot` is not the canonical ImageNGP list. It may remain one ImageFeature or be replaced by more precise ImageFeatures when the ImageNGP taxonomy is defined.

The ImageNGP phenotype taxonomy is being finalized in `jb/docs/ImageNGP/PRODUCTTYPES.md` and `jb/docs/ImageNGP/imagePhenotypes.md`. T-500 (Classified Stage) is blocked until those documents are complete.

---

## ONNX Model

- Current temporary external model: `sentence-transformers/clip-ViT-B-32`.
- Retrieval sources: Hugging Face model `sentence-transformers/clip-ViT-B-32` or Microsoft Foundry model `sentence-transformers-clip-vit-b-32`.
- Local ONNX runtime artifact, when used: `jb/src/core/Images/Classify/ONNX/clip-vit-b32-uint8/model_uint8.onnx`.
- The local ONNX file is a machine-local artifact and must not be stored in git.
- The ONNX file does not need to be recreatable because this model is temporary and will be replaced by a PRISM-owned model later.
- Exact tensor names, tensor shapes, dtypes, tokenizer compatibility, and normalization details are implementation contracts validated by `ImageClassifier.cs` at model load, not open project decisions for this temporary model.
- Prompt sets, thresholds, and expected label outputs belong to classification configuration and evaluation when PRISM's own model/taxonomy is finalized, not to this temporary external model folder.

---

## ONNX Ownership and Lifetime

`jb/src/core/Images/ImageClassifier.cs` owns the ONNX model boundary for PRISM.

Responsibilities:
- Load and validate required model files and tokenizer/model assets.
- Verify required assets exist before inference is attempted.
- Validate model readiness, including checksum and tensor contract checks when those contracts are known.
- Own communication between the model layer and the rest of PRISM.
- Hide any lower-level ONNX provider, worker, session, tokenizer, or buffer helper from PRISM callers.

ONNX sessions are application-scoped and reusable. Per-batch input/output buffers and any per-worker state remain isolated behind `ImageClassifier.cs`.

---

## ONNX Diagnostics

ONNX diagnostics run only in debug mode.

Debug diagnostics write model parameters, tensor metadata, and bounded per-sample output to the console. Normal batch output must not be bloated with unbounded tensor data or raw per-item scores.

---

## ONNX Runtime Provider Policy

- **CPU is the required baseline.** PRISM must run on local servers and laptops without a GPU.
- Only models that can run on CPU-only are permitted.
- Absence of a GPU must not disable model-dependent stages or fail a job.
- A GPU is a bonus resource only — it may enhance productivity of what could also be done on CPU.
- Missing, invalid, or incompatible required model files still fail fast and loud as PRISM-owned failures; GPU absence alone is not such a failure.
- No GPU → CPU fallback path is required. CPU-only is a supported configuration, not a fallback.

---

## Classification Confidence Thresholds

Configured in `jb/src/core/Prism_Config.json` at `Classification.Confidence_Threshold` and `Classification.Cutoff_Threshold`.

Current effective threshold: **`0.9`** (`Classification.Confidence_Threshold`).

| Score range | Storage |
|---|---|
| ≥ `Confidence_Threshold` | `ImageRecord_LAMBDA.Tags.Influential` — accepted evidence, may drive decisions |
| ≥ `Cutoff_Threshold` and < `Confidence_Threshold` | `ImageRecord_LAMBDA.Tags.Trivial` — weak evidence and diagnostics only, do not drive decisions |
| < `Cutoff_Threshold` | Discarded from matching, ordering, and transform evidence |

Traits use **both** a numeric confidence `double` and a boolean derived from it (boolean = score ≥ configured threshold).

---

## Unknown Classification States

- Every bounded ImageFeature enum has an `UNKNOWN` value.
- When classification or an analyzer is not confident enough to choose a concrete feature value → set that feature to `UNKNOWN`, never default to a false or arbitrary value.
- Unknown ImageFeatures can prevent confident ImageNGP derivation and must remain visible in the classification summary.
- Unknown transform-critical ImageFeatures route image handling to conservative processing in `Tx_ProblemImageProcessor.cs`.
- Valid imported/canonical images stay in the image collection regardless of classification confidence issues.

---

## Orientation ImageFeature

Hero orientation is one ImageFeature. Current allowed values are:
- `FRONT`
- `DIAGONAL`
- `BACK`
- `SIDEON`
- `TOP`
- `BOTTOM`
- `UNKNOWN`

---

## Border Intersection Detection

**Stage 1:** Use salient object bounds as a first stage.

**Stage 2:** Edge detection on a subsample of the image:
- Subsample covers entire width for horizontal edges (top/bottom); full height for vertical edges (left/right).
- Other dimension: `SubSampleWidth` = 10% of the smallest initial image dimension.
- Perform Canny edge detection to detect Hough Line presence.
- If Hough lines detected **and** those lines leave the image frame → salient object is considered intersecting.

Intersections can occur at zero, one, several, or all edges. Intersection at an edge means the salient object **cannot be repositioned** in that direction.

---

## Human Detection

**Step 1:** Scan histogram for "human skin color" percentage.
- Must account for all skin colors under all common lighting circumstances.
- Configurable parameter: `MinimumSkinToneArea`
- Result stored as a property of `ImageRecord_LAMBDA`.

**Step 2:** Part Affinity Field-based pose estimation (without detecting keypoints) to find a human skeleton (partial or full).
- Uses border intersection information (from prior stage) to predict partial/full skeleton.
  - Bottom intersection → legs likely cut off
  - Left/right intersection → one arm might be cut off

---

## Head Visibility Detection

- Attempt to detect facial features using the image as a matrix with kernels such as the Kernel Gabor-based Weighted Region Covariance Matrix (KGWRCM) optimized for facial feature detection.
- Limit detection area to the **top half** of the image.
- Scale the kernel using the previously discovered skeleton to match the size of a human head given anatomical proportions vs. image size vs. the single biggest blob of skin color in the top third of the full original image.
- Correlate result with image classification/labeling from the temporary CLIP model owned by `ImageClassifier.cs`.

---

## Expected Classification Label Categories (`ImageLabelingMatcher`)

- Human/silhouette presence (whether human appears fully within frame when confidence supports it)
- Clothing or product categories (e.g., `jeans`, `shirt`)
- Product colors (e.g., `blue`, `red`)
- Background labels: flat/single-color evidence and color when available
- Pose/orientation labels (e.g., `front`) — used as ImageFeature evidence

Labels are retained as classification evidence with confidence and summarized in `MatchEvidence` and `ImageRecord_LAMBDA`.

---

## Visual Deduplication

Runs in the `Classified` stage after import normalization.
- Key comparer: visual hash.
- Highest-resolution image → canonical image that continues through pipeline.
- Non-canonical duplicates: do not produce separate OK output images.
- Reported with safe source provenance in manifest/workbench diagnostics.
