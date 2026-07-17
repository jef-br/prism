# PRISM — Image Classification
*Abbreviations: `GLOSSARY.md`*

## When Classification Runs

Runs in the **`Classified`** stage for every canonical image after import normalization and visual-hash deduplication. Classification is unconditional — not a fallback.

Labels available before matching, ordering, generation, transformation, and export.

---

## IF and INGP

Classification and purpose-built analyzers produce **IFs**. Each IF is one measured image attribute (PT, orientation, background state, edge intersections, human presence, head visibility, skin-color evidence, object bounds, visual labels) with source and confidence.

INGP = phenotype derived from a combination of IFs. Not a single trait list like `TypeOfShot`. Examples: `PAP_FRONT`, `JEANS_GHOST_FRONT`, `JEANS_GHOST_DETAIL`.

`TypeOfShot` is one IF — not the INGP taxonomy.

INGP taxonomy: 26 phenotypes in `jb/docs/ImageNGP/imagePhenotypes.md` and `jb/docs/ImageNGP/PRODUCTTYPES.MD`.

**Current impl**: Most IFs set to `UNKNOWN` via `RecordUnknownFeatures()` in `ImageFeatureAnalyzer.cs`. CLIP runs for: `hero-is-human`, `hero-orientation`, `head-visible`, `body-visible` using natural-language prompts from `ClipPrompts.json`. Open work in `jb/src/core/Services/Matching/Classify/jbtodo.md`.

---

## Taxonomy & Prompt Configuration

INGP classification is config-driven — editable without recompiling the server:

- `ImageNGP.json` (`jb/src/core/ImageNGP/`) — canonical taxonomy: every IF id with its datatype and allowed values, plus the 26-phenotype catalogue.
- `ImageRoles.json` (same folder) — IF→phenotype rules, evaluated first-match by `PhenotypeRuleSet.cs`.
- `ClipPrompts.json` (`jb/src/core/config/`) — CLIP prompt → (IF, value) bindings, loaded by `ClipPromptCatalog.cs`.

At startup `ImageNgpValidator` cross-checks every IF id, value, and phenotype id used in `ImageRoles.json`, `DetOrderRules.json`, and `ClipPrompts.json` against `ImageNGP.json`. Any unknown id/value **fails fast and loud** — no silent UNKNOWN-on-typo.

---

## ONNX Model

- Temporary model: `sentence-transformers/clip-ViT-B-32`
- Sources: Hugging Face `sentence-transformers/clip-ViT-B-32` or Microsoft Foundry `sentence-transformers-clip-vit-b-32`
- Local path: `jb/src/core/Services/Matching/Classify/ONNX/clip-vit-b32-uint8/model_uint8.onnx`
- SHA-256: `4AC011172C8C022937BB83DAD2E8FC207F52F19972B36E14808CC3C8042C4E60` — verify before creating `InferenceSession`; mismatch → FFAIL
- Must not be stored in git. Not recreatable (temporary; will be replaced by PRISM-owned model).
- Tensor names, shapes, dtypes, tokenizer compatibility, normalization details: validated by `ImageClassifier.cs` at model load.

---

## ONNX Ownership

`ImageClassifier.cs` owns the ONNX boundary:
- Load and validate model files and tokenizer/model assets.
- Verify assets exist before inference.
- Validate model readiness (checksum + tensor contracts when known).
- Own communication between model layer and PRISM callers.
- Hide all lower-level ONNX provider/worker/session/tokenizer/buffer helpers.
- Sessions application-scoped. Per-batch buffers and per-worker state isolated behind `ImageClassifier.cs`.

---

## ONNX Diagnostics

Debug mode only. Writes model parameters, tensor metadata, bounded per-sample output to console. Normal batch output must not include unbounded tensor data or raw per-item scores.

---

## ONNX Runtime Provider Policy

- **CPU is the required baseline.** Must run on local servers and laptops without GPU.
- Only CPU-capable models permitted.
- GPU absence must not disable model-dependent stages or fail a job.
- GPU = bonus resource only.
- Missing/invalid/incompatible required model files → FFAIL. GPU absence alone is not.
- No GPU→CPU fallback path required — CPU-only is the supported configuration.

---

## Classification Confidence Thresholds

From CFG: `Classification.Confidence_Threshold` and `Classification.Cutoff_Threshold`.
Current effective: **`0.9`** (`Confidence_Threshold`).

| Score range | Stored in |
|---|---|
| ≥ `Confidence_Threshold` | `IRL.Tags.Influential` — accepted evidence; may drive decisions |
| ≥ `Cutoff_Threshold` AND < `Confidence_Threshold` | `IRL.Tags.Trivial` — weak evidence/diagnostics only; does not drive decisions |
| < `Cutoff_Threshold` | Discarded |

Each trait stores both numeric `double` confidence and a derived boolean (score ≥ threshold).

---

## UNKNOWN States

- Every bounded IF enum has an `UNKNOWN` value.
- Confidence below threshold → set to `UNKNOWN`, never default to false or arbitrary value.
- UNKNOWN IFs can prevent INGP derivation — must remain visible in classification summary.
- UNKNOWN transform-critical IFs → route to `Tx_ProblemImageProcessor.cs`.
- Valid canonical images stay in the image collection regardless of classification confidence issues.

---

## Orientation IF

`hero-orientation` allowed values: `FRONT`, `DIAGONAL`, `BACK`, `SIDEON`, `TOP`, `BOTTOM`, `UNKNOWN`

---

## Border Intersection Detection

**Stage 1:** Salient object bounds as first stage.
**Stage 2:** Edge detection on subsampled strips:
- Covers full width for horizontal edges; full height for vertical.
- `SubSampleWidth` = 10% of smallest image dimension.
- Canny edge detection → Hough Line detection.
- Hough lines detected AND leaving image frame → salient object is intersecting.

Intersection at an edge means the object **cannot be repositioned** in that direction. Each edge is independent: 0–4 intersections possible.

---

## Human Detection

**Step 1 — Skin histogram scan:**
- Detects skin-color percentage (all skin colors, all common lighting).
- Configurable: `MinimumSkinToneArea`
- Result stored in IRL.

**Step 2 — PAF-based pose estimation (partial or full skeleton):**
- Uses border intersection information from prior stage to predict partial/full skeleton.
- Bottom intersection → legs likely cut off; left/right intersection → arm might be cut off.

---

## Head Visibility Detection

- Facial feature detection using image matrix + kernels (KGWRCM) optimized for facial features.
- Detection area limited to **top half** of image.
- Kernel scaled using previously discovered skeleton (anatomical proportions vs. image size vs. largest skin-color blob in top third).
- Correlated with CLIP classification labels from `ImageClassifier.cs`.

---

## Expected Classification Label Categories (`ImageLabelingMatcher`)

- Human/silhouette presence
- Clothing/product categories (`jeans`, `shirt`, …)
- Product colors (`blue`, `red`, …)
- Background labels (flat/solid evidence + color)
- Pose/orientation labels (`front`, …)

Labels retained as evidence with confidence; summarized in ME and IRL.

---

## Visual Deduplication

Runs in `Classified` stage after import normalization.
- Key comparer: visual hash.
- Highest-resolution → canonical; continues through pipeline.
- Non-canonical duplicates: no separate OK output.
- Reported with safe source provenance in manifest/workbench diagnostics.

---

## ONNX InferenceSession Scope

`InferenceSession` is **application-scoped singleton** held by `MatchingService`. The 146 MB model loads once at startup (in the `MatchingService` constructor) and is reused across all jobs for the lifetime of the API process. `ClassificationService` is still created per-job but borrows the shared `ImageClassifier` — it does not own or dispose it.

`MatchingService` owns a `_clipLock` object; all `InferenceSession.Run()` calls are serialized through it (required for DML execution provider which is not thread-safe for concurrent calls).

## interior-shot Detection

interior-shot is now reachable via the `interior-detected` ImageFeature, set by `InteriorAnalyzer.cs`. The analyzer detects enclosed regions that are smoother than their surrounding texture and bounded by strong edges. Product-type gating (wallet/bag/suitcase only) is applied at the Order stage (T-1800).

## illustration-technical-drawing Detection

`illustration-technical-drawing` is no longer a catch-all. It requires a positive signal from `Analyzer_IsIllustration.cs` via the `is-illustration` feature (in addition to `hero-is-human = FALSE`).

`Analyzer_IsIllustration` applies three-signal topological analysis — all three must pass:

| Signal | What is measured | Threshold |
|---|---|---|
| HF Edge Density | Fraction of pixels where Sobel gradient ≥ 60/255 | ≥ 12% (`MinEdgeDensity`) |
| Background Flatness | Fraction of border-strip pixels (5% depth each side) where all RGB channels ≥ 230/255 | ≥ 80% (`BackgroundFlatnessMin`) |
| Color Cluster Count | Count of quantized RGB buckets (8 bins/channel = 512 total) with > 1% population | ≤ 4 (`MaxColorClusters`) |

The first signal catches the high line frequency typical of technical drawings. The second targets the near-white flat backgrounds illustrations are always printed on. The third distinguishes drawings (1–2 colors: black + white) from actual colored illustrations (few color clusters) vs. product photos (many clusters).

Transparent pixels count as white for the background signal. Every-other-pixel sampling used in the color signal for performance.

File: `jb/src/core/Services/Matching/Analyzers/Analyzer_IsIllustration.cs`
