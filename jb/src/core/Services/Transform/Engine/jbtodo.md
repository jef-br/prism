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

### Settled sub-decisions (user, 2026-07-28)

- jbtodo home confirmed here (Transform Engine); Classify touch points noted per ticket.
- Transform doc sync: `PRISM-transform-generate.md` may still list the stale 5-value `background-type`
  taxonomy — reconcile to `SOLIDCOLOR`/`REALLIFE`/`UNKNOWN` when T-48x0 lands.
- **Shadow-present: re-declare it as a real ImageNGP feature**, measured by the detector, via
  `HowToAddAPhenotype.md`. Not record-only. This is only viable because of the stage move below.

### Detector stage — settled (user, 2026-07-28)

`SubjectDetector` runs in **`ImageFeatureAnalyzer.Refine` wave 3**, immediately before
`FinalizePhenotype` — not in `ImagePreProcessor.PreprocessAsync` where it first landed.

Why this exact seam is the only one that works: three requirements have to hold *simultaneously*, and
they hold together nowhere else in the pipeline.

| Requirement | Why Refine wave 3 satisfies it |
|---|---|
| Excel seed available | `Refine` runs post-match; the `FamilyIDRecord` is resolved from `MatchEvidence.FinalFamilyId` |
| CLIP colour seed available | `Analyzer_ProductColor` + `Analyzer_BackgroundColor` run two lines earlier in the same wave |
| Phenotype not yet assigned | `FinalizePhenotype` is the very next step, so a detector-measured feature is readable by the rules |
| No extra decode | wave 3 already holds a shared decoded image across the analyzer chain |

Had detection stayed at the Transform stage, a detector-measured `shadow-present` would be written
*after* phenotype assignment and would therefore always read `UNKNOWN` when `ImageRoles.json` rules
evaluate — the exact "UNKNOWN trap" `HowToAddAPhenotype.md` warns about, and the shape of stub that
T-4700 deleted ten of. Transform now reads `lambda.Subject` and performs no detection of its own.

### Seed steering — what the toggles actually do (user, 2026-07-28)

The design doc said "harder isolation" and "more effort" without naming parameters. Settled:

**(a) product-color ≈ background-color → this is where CLAHE belongs.** CLAHE exists to lift a
white-on-white weave clear of the noise floor. When the product colour is clearly distinct from the
background, chroma already carries the signal and **CLAHE is superfluous** — so it is skipped, and its
cost with it. It earns its place only when product and background nearly match. (Colours unknown → keep
CLAHE on, the conservative choice, preserving today's behaviour rather than silently weakening detection.)

**(b) background not flat → a second discrimination step, then one of two treatments.** "Non-flat" is not
one condition, it is two:
- **B1 — soft gradients, mild noise, a speck of dust: a photo-studio sweep.** The existing least-squares
  background *plane* already models a smooth ramp; B1 adds speckle tolerance. Cheap.
- **B2 — a real-life background.** This gets `HeroDetectionOnSteroids`: the everything-we-have escalation
  for an **accurate** hero detection — prior evidence, yolo26n, saliency, anything that helps. Explicitly
  **not** built out fully now (user: "do not go bananas"); the method exists and is named so the
  escalation path is a visible seam rather than an implied one, and so the next person extending it knows
  exactly where that work goes.

The discriminator between B1 and B2 is the **residual of the background plane fit over the border ring** —
already computed by `FitBackgroundPlane`. A smooth studio sweep fits the plane closely (low residual); a
real-life scene does not (high residual). No new measurement is needed to tell them apart.

**(c)** Shadow accounting — settled above: re-declared as an ImageNGP feature.

### Verification

Unit tests per producer + per toggle (white-on-white, cast-shadow, gradient/backdrop-into-floor,
bleed-off detail shots). Debug overlay (port `save_debug_overlay`) to eyeball mask/box. In-process
evidence harness (`prism-evidence-report`) on a shadow/background-heavy set. A/B vs current salient box.
Perf: classify-stage cost delta on SPACINI29 vs the 156.5s baseline; confirm the non-flat toggle saves
time on flat sweeps.

### Review + completion pass (2026-07-28, second session)

All seven children reviewed. T-4805 / T-4810 / T-4820 approved as landed. Three came back with blocking
findings, now closed:

- **T-4850** — `PreferSubjectGeometry` claimed a confidence gate it never implemented (it read only the
  whole-frame flag), so a 0.1-confidence sparse blob overrode the legacy bbox unconditionally — including
  the null-bbox case that previously routed safely to `Tx_ProblemImageProcessor`. Now gated on
  `Crop.SubjectPromotionMinConfidence` (0.35). Promotion also destroyed the legacy box, which made this
  ticket's own A/B acceptance bar unverifiable; the pre-promotion box is retained on `LegacySalientBox`
  and emitted as evidence.
- **T-4860** — `background-type = UNKNOWN` normalised to null and therefore read as *flat*, identical to a
  known `SOLIDCOLOR`, inverting the spec. And the shadow shrink ran before routing, so it perturbed
  `Tx_CropSquare`/`Tx_DetailCropper`/`Tx_ProblemImageProcessor` inputs it was never scoped for. Both fixed.
- **T-4830** — three of four mandated test scenarios were missing, including the algorithm's defining
  invariant. Added: white-on-white (texture-only), gradient background, and a cast-shadow case that now
  asserts the box **excludes** the shadow strip, not merely that the evidence flag fired.

**Separately found, and worse than any of the above: the epic did not build under CI.** `ci.yml` builds
Release with `-warnaserror:SA1402,SA1649,S109,SA1101`; the detector port introduced 21 unnamed magic
numbers (the MAD scale factor, the plane-fit sample cutoff, bilateral-filter tuning, histogram bins), so
S109 failed the build. A plain local `dotnet build` hides this because S109 is only a warning outside CI —
which is how it passed both the original implementation and the first review. Fixed as named `private
const` per the `jb/ticketboard/AGENTFEEDBACK.md` T-4400 policy, zero value changes.

### Real-data verification (SPACINI29, 2026-07-28)

Green tests were not enough — two things only the evidence run could find.

**1. Detection was dead in production while 466 tests passed.** The first evidence run reported
`SubjectProducer` empty, `promoted=False` and `shadow-present=UNKNOWN` on all 86 images. Cause: the
ImageSharp→OpenCvSharp conversion used `Mat.SetArray(byte[])`, which OpenCvSharp rejects against a
`CV_8UC3` Mat ("Mat data type is not compatible"). Effect: `Refine` threw on every image, and
`MatchingService`'s deliberate non-fatal `catch { refinementFailed++; }` swallowed it — so detection
produced nothing *and* phenotype assignment silently dropped to 0, surfacing only as a warning counter
nobody reads. Consequence: every unit test passed because each one builds its Mat with OpenCvSharp
directly and never crosses the conversion boundary. Fixed with `Marshal.Copy` into the Mat's own buffer,
plus `SubjectDetectionWiringTests` which drives the real conversion through `FeatureAnalysisService.Refine`
and fails if that path ever throws again.

**2. The hard-shadow signal was degenerate.** At the shipped `HardShadowEvidenceFraction` of 0.01 it fired
on **86/86** images, so it discriminated nothing while trimming 6% off the bottom of every centred image
and publishing `shadow-present=true` for all of them. `SubjectDetection.HardShadowStrippedFraction` now
carries the raw measurement, which made calibration possible instead of guesswork: min 0.0113, median
0.0371, p90 0.0702, max 0.1243. User set the threshold to **0.05** (config only — 23/86 fire).

**Measured outcome after both fixes** (86 images, all OK):

| Signal | Result |
|---|---|
| Producer | `classical-cv` on 86/86 |
| Real detections vs whole-frame fallback | 83 / 3 |
| Promoted into routing geometry | 71; the other 15 are exactly those below the 0.35 confidence floor |
| `shadow-present` | 23 true / 63 false |
| Toggle (a) product≈background | fires on 19/86 |
| Toggle (b) non-flat background | 0/86 — SPACINI29 is entirely `SOLIDCOLOR`, so the B2 `HeroDetectionOnSteroids` path is **not exercised by this dataset** |
| Refinement failures | 0 (was 86) |
| Classify-stage cost | 174.8s vs the 156.5s baseline — **+18.3s (+11.7%)** for detection |

**A/B against the legacy salient box** (71 promoted images): centre shift median 15.5px on ~3500px
images, and 51/71 agree within 50px. Area ratio subject/legacy median 1.027. The five largest
disagreements all sit at mid confidence (0.48–0.61). So the two agree closely on the bulk and the
confidence gate withholds the weakest detections — but "equal-or-better **centering**" cannot be claimed
from geometry alone without labelled data or visual inspection. That remains the one open piece of
T-4850's acceptance.

### Implementation status (2026-07-28)

Epic T-4800 implemented across Wave 0–3. Landed:
- **T-4805** — `Tx_CenterAndStretch.Process` now honours the lambda bbox (was `FullImageBounds`); dead
  `Tx_LowContrastEnhancement.Enhance` removed. `Tx_DetailCropper`/`Tx_CropSquare`/`Tx_ProblemImageProcessor`
  audited: already compliant (DetailCropper honoured the lambda; the other two are bbox-independent).
- **T-4810** — `SubjectDetection` contract (`Models/`) + `Subject` on `ImageRecord_LAMBDA` +
  `ISubjectDetector` seam + round-trip test.
- **T-4820** — `TransformSeed` read-model (Excel+CLIP signals) threaded via `TransformService` →
  `ImageTransformer`; FamilyIDRecord lookup by id.
- **T-4830** — `SubjectDetector` (classical-CV port of `process_images.py`) + `SubjectDetectorConfig` +
  ClassifyConfig.json `SubjectDetector` section; wired into `ImagePreProcessor.PreprocessAsync`
  (populates `lambda.Subject`, additive). `MaxAnalysisSize` 1024 (Python used 2400).
- **T-4850** — `ImageTransformer.PreferSubjectGeometry` promotes a confident (non-whole-frame) Subject
  into the legacy bbox + intersect features, so routing and every Tx run on the detector geometry.
- **T-4860** — `TransformToggles` (product≈background, non-flat-background, shadow) computed from
  seed+subject; shadow toggle trims the box bottom (`Crop.ShadowBottomShrinkFraction`).
- **T-4870** — detection + toggle evidence appended to `OutputRecord.SafeSummaryText` (Todo-4 carrier).

Tests: Transform suite 67 green; Core unit 127 green; SubjectDetector unit tests green.

**Previously deferred — both pulled into scope by the user on 2026-07-28, no longer deferred:**
- **T-4830 ingress-alpha path** — being built. Alpha is flattened onto white inside
  `Importer.LoadImageWithExifOrientation`; the capture goes in after `AutoOrient()` and before the white
  composite, carried on a new `ImageRecord_INPUT.Subject` field and preferred over the heuristic producer.
- **Toggles (a)/(b) behavioural effect** — being built. The recorded reason for deferring ("seed is
  resolved at the Transform stage, after preprocessing") did not survive inspection: `TransformSeed.Resolve`
  sits seven lines *below* the `PreprocessAsync` call inside the same method, so nothing structural
  prevented seeding — it was an ordering accident. The genuine constraint was a different one (a
  detector-measured phenotype feature has to exist before phenotype assignment), and it is resolved by the
  stage move recorded above, not by reordering two lines.
