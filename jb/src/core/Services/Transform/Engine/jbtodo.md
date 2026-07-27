# Image Transform Todo

- [ ] Tx_util_HeadCutter Algorithm A — anatomy-guided search space refinement: when `has-human == true`, use the lambda BoundingBox dimensional proportions combined with human anatomical ratios (e.g. head ≈ 1/8 of body height) to narrow the Haar face-detection search region before running DetectMultiScale. Requires a deepdive into apparel-image anatomical ratio distributions to determine reliable constants.
  - File: `jb/src/core/Images/Transform/Tx_util_HeadCutter.cs`
  - Blocked until: anatomical ratio constants are agreed upon.
  - Answer:
    - The ratio of head-to-body should lie between 1:4 (kids) and 1:8 (adults) anything outside that is weird
    - Implied search band (derived from that ratio + shipped Algorithm B Haar path, not a new constant): head occupies the top H/4 (kids, widest case) to H/8 (adults) of the lambda BoundingBox height H. So restrict `DetectMultiScale` to the top ~25% of the BoundingBox — covers the widest 1:4 case — instead of the full frame: ~75% fewer pixels scanned, and torso/hand false positives fall out of the region entirely. Bound the scale sweep too: face height ≈ head height, so `minSize` ≈ H/8, `maxSize` ≈ H/4 — the same ratio caps the cascade's window range. Still blocked on confirming the exact top-of-band offset (crown sits above the face box) before wiring in.

-------
- [ ] Spec and implement Tx_util_HeadCutter: utility class that crops a human head at the nose-to-lips boundary, with family-aware fallback for covered or out-of-shot faces.
  - File: `jb/src/core/Images/Transform/processingtools/Tx_util_HeadCutter.cs` (to be created).
  - Crop target: the horizontal cut falls between the bottom of the nose and the top of the lips.
  - Two operating modes:
    1. Family-aware mode (preferred): detect face position from images in the group where the face is clearly visible, apply the derived cut line consistently to all images in the family including covered/out-of-shot faces.
    2. Per-image mode (fallback / webservice): detect and cut the head individually per image.
  - Open questions to answer before implementing:
    - Which facial landmark model / library? (e.g. MediaPipe Face Mesh, dlib 68-point, ONNX face landmark model)
    - Family-aware mode: minimum number of clear-face images required to derive the shared cut line? Fallback when threshold not met?
    - Straight horizontal crop or slight curve / soft mask?
    - How is the derived cut position passed to the Tx_ caller (pixel Y-coordinate or ratio of image height)?
  - Signature: `Process(byte[] arr, int stride, float upscale_factor)` for webservice (per-image mode); family-aware mode is PRISM-internal and receives the Lambda collection.
  - Answer: (not final — observed current-implementation state only, product decisions below still open)
    - File-path correction: the class already exists at `jb/src/core/Services/Transform/Engine/Tx_util_HeadCutter.cs` (not the `processingtools/` path in the todo header, and no longer under `Images/Transform/` — the 2026-07-08 Services/lib restructure moved it into `Services/Transform/Engine/`). It ships the per-image Algorithm B path as an internal `Analyze(ImageRecord_LAMBDA lambda, Mat colorMat)`, reusing the BGR Mat from ImagePreProcessor (no second decode). Verified 2026-07-13: `Analyze` signature, Haar `haarcascade_frontalface_default.xml` path, `SubMat` full-width crop, and in-place `ProcessedBytes`/`BoundingBox` mutation are all unchanged. **Changed since 2026-07-09:** the `0.75` cut constant is no longer a hardcoded literal — T-4200's config retrofit externalized it to `transform_Config.json` `HeadCutter.FaceHeightCutFactor` (bound via required, no-default `HeadCutterConfig`, validated to (0,1)), so the line now reads `cutY = bestFace.Y + (int)(bestFace.Height * cfg.FaceHeightCutFactor)`. Shipped value is still 0.75 — behaviour identical, but the heuristic is now a tunable, not a magic number.
    - Open questions the shipped code has *de facto* answered (read off the code, not a decision):
      - Landmark model: none. Pipeline is Haar face-box → fixed proportion, not landmarks. `CascadeClassifier` on `haarcascade_frontalface_default.xml` (`DetectMultiScale` over the full gray frame) yields a face rect; the nose-to-lips cut is approximated as `cutY = faceBox.Y + 0.75*faceBox.Height`. So "nose-to-lips" is an assumed 75%-of-face-box constant, not measured — accuracy rides entirely on how consistently Haar frames the face, and there is no landmark evidence to place the actual nose/lip line.
      - Crop shape: straight full-width horizontal cut (`SubMat(0, cutY, cols, rows-cutY)`), re-encoded to JPEG. No curve, no soft mask.
      - Cut delivery: not returned as a Y-coordinate or height ratio. The utility mutates `lambda.ProcessedBytes` and shifts `lambda.BoundingBox` up by `cutY` in place — PRISM-internal collection path only.
      - Multi-face pick: qualifies only faces whose centroid sits in the top half (`f.Y + f.Height/2 < imageHeight/2`), then picks the one furthest from the top edge (lowest centroid Y).
    - Still genuinely open (unchanged, needs your call — the code does NOT settle these):
      - Family-aware mode is not implemented. Only per-image detection exists; no shared cut line derived across a family, so the "minimum clear-face images / fallback threshold" question is untouched.
      - The webservice `Process(byte[], int, float)` per-image signature is not implemented — only the internal `Analyze` path exists.
      - Whether to replace the 0.75-of-face-box heuristic with a real landmark model for the true nose-to-lips line (ties into Algorithm A's crown-offset deepdive above).

-------

## Subject Isolation & Model-Aware Transformation (epic T-4800)

**Cross-cutting:** this design spans **Classify/preprocessing** (mask/box producer, seeding features,
ingress alpha capture) and **Transform** (consume mask/box, behavior toggles, evidence). Anchored here
because Transform is the driving feature; the upstream touch points are called out per ticket.

### Origin

Folds in the root-level `TRANSFORM-SUBJECT-ISOLATION-NOTE.md` idea note (now removed) plus a mature
classical-CV prototype vendored at `jb/docs/reference/process_images.py`. The note's premise is correct
and identifies a real gap: every Transform strategy acts on a single rectangular `BoundingBox` (from
`ImagePreProcessor.DetectSalientBoundingBox`) plus four `intersects-*` booleans. There is no subject
mask, no shadow separation, no product-color signal, and although the full Excel model is reachable in
Transform (`matched.Ingest.FamilyRecords`), nothing dereferences it — so transforms center and stretch a
rectangle that includes shadow and background, blind to what we already know about the product.

**Two anchors:**
1. **ONNX stays upstream.** All ONNX / image analysis runs before Transform; Transform is pure
   geometry + fill (OpenCvSharp). Rationale: keeps the transformations as deterministic as possible.
2. **Prototype = v1 reference.** `process_images.py` is a working, no-ONNX realization that resolves the
   note's own unsettled crux (its step-9 morphological fusion): it excludes shadow by keying on
   **chroma + texture, never lightness**, handles white-on-white via texture, discriminates hard-shadow
   edges by shape, fits the background as a plane over the border ring, uses Canny flood-fill as
   corroboration only, and protects the product band during fill.

### Corrections to the original note (do not let these drive work)

- Model is `yolo26s` (small), not "YOLO26n"; YOLO runs in Matching, emits boxes (no masks), is discarded.
- The "~1,300 product types → shadow indication" was a product-type→shadow-expectation *lookup table*
  (1,300 = 1300), not a per-pixel map. It does not exist and is **not needed for v1**.
  `ProductTypeMap.json` is 18 slugs for det-slot ordering.
- Center/stretch/margin/background already exist (`Tx_CenterAndStretch` + 4-tier `Tx_util_BgStretch`);
  CLAHE exists (`Tx_LowContrastEnhancement`); saliency + Canny already run upstream in `ImagePreProcessor`.

### Reconciliation with T-4700 (analyzer/taxonomy trim, landed 2026-07-27)

- `background-type` is settled at `SOLIDCOLOR / REALLIFE / UNKNOWN` (`STUDIO` removed) — no
  reconciliation needed; "flat" = `SOLIDCOLOR`.
- Seeding inputs all survived the trim: `product-color`, `background-type`, `background-color`,
  `product-type-label`, `intersects-*` are still declared. Seeding = threading, not building.
- `shadow-present`/`reflection-present` were removed with the shadow stub. The shadow toggle reads the
  detector's candidate-shadow evidence off the record directly; re-declaring a detector-measured
  `shadow-present` feature is optional (must follow `jb/docs/ImageNGP/HowToAddAPhenotype.md`).
- Expect more churn: a T-4700 follow-up collapses `DetOrderRules.json`/`ProductTypeMap.json` 19→5
  product types. Re-check config/code state when implementing.

### Finding — Transform vs Process entry-point divergence (top priority, latent not live)

`IImageTransformation` has two entries: pipeline `Transform(lambda)` (uses the detected salient bbox) and
per-image `Process(arr, stride, upscale, lambda = null)`. The interface contract says `Process` must
reuse the lambda's BoundingBox when provided, else derive from `arr`. But `Tx_CenterAndStretch.Process`
**ignores `lambda`** and always crops to `FullImageBounds(arr)`; `Tx_DetailCropper`'s comment says it
follows "the precedent Tx_CenterAndStretch.Process establishes" — the flaw is systemic.

- Cause: `Process` uses full-image bounds instead of the lambda's real bbox.
- Effect: per-image `Process` output ≠ pipeline `Transform` output for the same image.
- Consequence: **not live today** — the deployed transform microservice (`Prism.ServiceHost/Program.cs`)
  routes through `TransformService.TransformAsync` → `ImageTransformer.TransformImage` → `.Transform(lambda)`
  (canonical pipeline). Only a test exercises `Process`. But it is a trap: a future per-image webservice
  wired to `Process` would diverge and would ignore the persisted SubjectBox this design introduces.

**CLAHE corollary:** neither entry applies CLAHE today — `Tx_LowContrastEnhancement.Enhance` has no
caller, so the doc's "Tx_CenterAndStretch CLAHE pre-step" is not true in code. The intent (CLAHE improves
bbox accuracy) is honored by moving CLAHE into the upstream detector's throwaway preprocessing. The dead
`Enhance` is removed; the standalone `Tx_LowContrastEnhancement.Process` CLAHE-webservice stays a utility.

### Settled decisions

1. **Architecture — upstream, pluggable producer, two entry points → one contract.** Detection produces
   a persisted `SubjectMask` + `SubjectBox` (+ edge-intersect signals) on the record; Transform consumes
   them (geometry + fill only). One contract, swappable producer.
   - *Alpha path — at ingress, before jpg normalization.* Real alpha channel → build box/mask from
     transparency before composite-onto-white destroys it; persist; skip the heuristic path.
   - *Heuristic path — in Classify/preprocessing.* No usable alpha → run the ported classical-CV detector.
2. **v1 producer — port `process_images.py`** (chroma-plane + texture + shadow-strip-by-shape + Canny
   corroboration) to C#/OpenCvSharp4. SAM3 / yolo26s-seg deferred as future producers behind the contract.
3. **Excel + CLIP seeding — IN, ASAP.** product = Excel + CLIP; background = CLIP. Inputs already exist as
   features; work is threading to Transform + toggles. Three toggles: (a) product-color ≈ background-color
   → harder isolation; (b) background-type not `SOLIDCOLOR` → spend more on hero detection (skip when
   `SOLIDCOLOR`); (c) hard-shadow patterns → shadow-accounting.
4. **Shadow — image-driven toggle, table optional.** Detector stays shadow-agnostic; toggle fed by the
   detector's thin-line-texture (candidate hard-shadow) evidence, driving the existing
   `Tx_CenterAndStretch` shrink. Product-type→shadow-expectation table is optional later corroboration.
5. **Fill — unchanged in v1.** Keep `Tx_util_BgStretch` and its expansion-ratio tiers as-is, fed the
   better box/mask. Seam-carving deferred (later follow-up would replace the Tier-3 Inpaint method with
   product-band-protected seam-insertion).
6. **Evidence — fold into Export Todo 4** (`transform-manifest.json`, `jb/src/core/lib/Export/jbtodo.md`).
   Don't spawn a parallel store.

### v1 detector (ported reference algorithm)

Runs upstream (extends/replaces `ImagePreProcessor.DetectSalientBoundingBox`). Classical CV, no ONNX.
Produces `SubjectMask`, `SubjectBox`, per-edge intersect signals, candidate-shadow-edge evidence.

**Code style — one named helper per step (mandatory), K&R braces, recipe-readable:**

```csharp
var detection = this.Detect(image, cfg);
if (this.IsWhiteOnWhite(detection)) { this.WhiteOnWhiteAdjustments(detection); }
this.StripHardShadowEdges(detection);
this.CorroborateWithCanny(detection);
var box = this.SignificantComponentsBox(detection);
```

Steps (each = its own helper): alpha shortcut at ingress · detection on a throwaway preprocessed copy
(bilateral denoise → LAB → CLAHE on L → high-pass → local std-dev = texture; chroma = distance from a
least-squares background **plane** fitted over the border ring) · **lightness deliberately not a
criterion** (excludes shadow) · white-on-white via texture · hard-shadow edges stripped by shape (morph
OPEN on texture-only chroma-unsupported thin lines, also emitted as candidate-shadow evidence) · Canny
border flood-fill as corroboration only · robust MAD thresholds with config floors · edge-intersect /
bleed-off-canvas via `count_canvas_contacts` (cleaner than current `intersects-*`).

### Tickets & waves (board: epic T-4800)

- **Wave 0 (P4, blocks all):** T-4805 (unify Transform/Process — top priority), T-4810 (persisted
  SubjectMask/SubjectBox contract), T-4820 (seeding access + FamilyIDRecord link in Transform).
- **Wave 1:** T-4830 (port the v1 detector + ingress alpha path; P1 + P4 review). T-4840 (vendor the
  reference script) — **done**, script is at `jb/docs/reference/process_images.py`.
- **Wave 2:** T-4850 (consume mask/box; routing; P1 + P4 review), T-4860 (three toggles + shadow wiring).
- **Wave 3:** T-4870 (extend transform-manifest evidence; coordinate with Export Todo 4).
- **Board action:** thaw `[[T-2600]]` (user-directed) for a future segmentation producer behind T-4810.
- **Deferred/optional:** seam-carving Tier-3 replacement; product-type→shadow-expectation table;
  SAM3 / yolo26s-seg producers behind the T-4810 contract.

### Open sub-decisions

- jbtodo home confirmed here (Transform Engine); Classify touch points noted per ticket.
- Transform doc sync: `PRISM-transform-generate.md` may still list the stale 5-value `background-type`
  taxonomy — reconcile to `SOLIDCOLOR`/`REALLIFE`/`UNKNOWN` when T-48x0 lands.
- Shadow-present re-declaration: detector-measured ImageNGP feature (via `HowToAddAPhenotype.md`) vs
  record-only detector evidence + toggle input (T-4860).

### Verification

Unit tests per producer + per toggle (white-on-white, cast-shadow, gradient/backdrop-into-floor,
bleed-off detail shots). Debug overlay (port `save_debug_overlay`) to eyeball mask/box. In-process
evidence harness (`prism-evidence-report`) on a shadow/background-heavy set. A/B vs current salient box.
Perf: classify-stage cost delta on SPACINI29 vs the 156.5s baseline; confirm the non-flat toggle saves
time on flat sweeps.
