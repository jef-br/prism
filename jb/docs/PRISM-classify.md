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

- `ImageNGP.json` (`jb/src/core/config/`) — canonical taxonomy: every IF id with its datatype and allowed values, plus the 26-phenotype catalogue.
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

Repo-wide policy (CLIP, YOLO, Upscale, and every future model-running component), not CLIP-specific — see `PRISM-model-runtime.md` for the full mandate, the single pinned package version, the shared `OnnxSessionFactory` construction path, and enforcement. Summary: CPU is the mandatory baseline; DirectML is used automatically when a hardware adapter is present; GPU absence never fails a job.

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

## Subject Isolation (`SubjectDetector`)

Produces the persisted `SubjectDetection` — subject box, binary mask (PNG), per-edge intersect flags, and
candidate-hard-shadow evidence — that the Transformed stage consumes as pure geometry. Classical CV, no
ONNX, so the transforms stay deterministic. Ported from `jb/docs/reference/process_images.py`.

**The defining invariant: lightness is never a detection criterion.** A cast shadow is a near-pure
lightness change, so keying on brightness would pull the shadow into the product box. Detection keys only
on **chroma** (distance from a least-squares background *plane* fitted over the border ring — a plane, not
a mean, so a backdrop curving into a floor is modelled rather than mistaken for subject) and on **texture**
(local standard deviation after a high-pass, which strips slow shadow penumbra). White-on-white is caught
by texture alone. Thin, texture-only, chroma-unsupported lines are stripped by shape (morphological open);
that stripped fraction is the hard-shadow evidence. A Canny border flood-fill corroborates only — it can
never introduce a region on its own, so an isolated shadow silhouette cannot sneak in.

### Where it runs, and why it has to run there

Inside `ImageFeatureAnalyzer.Refine` **wave 3**, immediately before `FinalizePhenotype`. This is the only
point in the pipeline where four conditions hold simultaneously:

| Condition | Why it holds here |
|---|---|
| Excel seed available | `Refine` runs post-match; the FamilyIDRecord is resolved from `MatchEvidence` |
| CLIP colour seed available | `Analyzer_ProductColor`/`Analyzer_BackgroundColor` ran moments earlier in the same wave |
| Phenotype not yet assigned | `FinalizePhenotype` is the next step, so `shadow-present` is readable by the rules |
| No second decode | wave 3 already holds the image decoded and shared across the analyzer chain |

Running it later (at the Transform stage, where it first landed) makes a detector-measured feature always
read `UNKNOWN` when `ImageRoles.json` evaluates — the UNKNOWN trap in `ImageNGP/HowToAddAPhenotype.md`.
The detector is OpenCvSharp and the Classify project is ImageSharp-only, so `Refine` takes the detection
as a callback that `FeatureAnalysisService` (in `Prism.Core`) supplies. Note the concurrency consequence:
the refinement loop is serial, so detection is serial too.

### Seeded steering

The seed (`SubjectSeedHint`) lets detection decide how hard to work *before* it runs:

- **Product colour ≈ background colour → CLAHE runs.** CLAHE exists to lift a white-on-white weave clear
  of the noise floor. When the colours are measured as clearly different, chroma already separates product
  from background and CLAHE is superfluous, so it is skipped along with its cost. Unknown colours keep it
  on — an unmeasured signal is not evidence of contrast.
- **Background not `SOLIDCOLOR` → a second step decides the treatment.** "Non-flat" is two conditions, not
  one. **B1**, a studio sweep (soft gradient, dust specks, sensor noise), fits its background plane closely
  and gets speckle tolerance. **B2**, a real-life scene, does not fit, and gets `HeroDetectionOnSteroids` —
  a higher analysis resolution and a stricter significant-blob bar. The discriminator is the **mean
  absolute residual of the border-ring plane fit**, which detection already computes; no second
  measurement and no extra CLIP call. A known `REALLIFE` background skips straight to B2. A `SOLIDCOLOR`
  background skips the whole step, which is the speed win.

`HeroDetectionOnSteroids` is the designated seam for heavier evidence (prior per-family evidence, yolo26n
boxes, saliency). Extend it there rather than scattering real-life special cases through detection.

### Producers behind one contract

`ISubjectDetector` is a swappable seam. `"classical-cv"` (the heuristic above) is the only producer today;
`FeatureAnalysisService.DetectSubject` also emits an `"edge-bleed"` shortcut when the subject already
touches all four edges (no background ring left to fit a box against). A segmentation model is a future
producer, and Transform needs no change to accept one — it consumes `SubjectDetection` generically.

T-5030 (2026-08-04) removed the `"alpha"` producer and the separate ingress alpha-capture path it rode
on: every accepted input format is now composited onto white and re-encoded as JPEG before any analyzer
runs, so no image downstream of Import ever carries a real alpha channel. `AlphaSubjectCapture.cs` is
deleted; `ImageRecord_INPUT.Subject` (the alpha→lambda seed) is gone.

### Hard-shadow evidence

The thin, texture-only, chroma-unsupported lines stripped during detection are the hard-shadow signature.
`SubjectDetection.HardShadowStrippedFraction` carries the raw measurement (fraction of frame stripped) and
`HasHardShadowEvidence` is that measurement against `HardShadowEvidenceFraction`. `Analyzer_ShadowPresence`
publishes the boolean as the `shadow-present` feature; the Transformed stage's shadow toggle reads it to
trim the box bottom so a cast shadow is not centred as product.

Keep the raw fraction and the verdict separate. The threshold shipped at 0.01 and fired on 86 of 86
SPACINI29 images — a signal that is always true carries no information, and it was trimming every centred
image for nothing. Calibrated to 0.05 on 2026-07-28 (23/86). Because the measurement is persisted, the
threshold can be re-tuned against labelled data without re-instrumenting the detector — see [[T-4945]].

Config: `ClassifyConfig.json` → `SubjectDetector`. All values `required`, no in-code defaults.

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

interior-shot is now reachable via the `interior-detected` ImageFeature, set by `Analyzer_Interior.cs`. The analyzer detects enclosed regions that are smoother than their surrounding texture and bounded by strong edges. Product-type gating (wallet/bag/suitcase only) is applied at the Order stage (T-1800).

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
