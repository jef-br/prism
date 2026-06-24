# Image Transform Todo

-------
- [ ] Define transform-facing ImageFeature and ImageNGP output: say which ImageFeatures and selected ImageNGP phenotypes feed crop and center logic, including what happens when transform-critical features are unknown.
  - Impact:
    - Project progress: High - Transform rules depend on selected ImageNGP phenotype plus features such as type-of-shot, orientation, background, human/head evidence, edge intersections, object bounds, and detail/whole-product evidence.
    - Effect on other TODOs: Blocks - It links ImageFeature output, selected ImageNGP phenotype, the open classification todo, crop behavior, fill policy, and fallback rules.
  - Industry standard:
    Transform engines consume bounded classification/analyzer features and selected image phenotypes rather than re-inferring product semantics inside crop code.
  - Recommended solution:
    Pass transform-relevant ImageFeatures and the selected ImageNGP phenotype with confidence from classification into transform decisions, and define a safe fallback for unavailable or below-threshold feature evidence.
  - Answer:
    Routing reads from `ImageRecord_LAMBDA.Features`:
    - `intersects-top/bottom/left/right` — edge intersect detection
    - `salient-bbox` — object bounds (required for CenterAndStretch and DetailCropper; UNKNOWN routes to ProblemImageProcessor)
    - `low-contrast` — triggers `Tx_LowContrastEnhancement` pre-step in CenterAndStretch
    - `shadow-present` — triggers shadow bbox shrink in CenterAndStretch (tighten bottom edge above shadow band)
    Routing reads `SelectedPhenotype`:
    - `"closeup-image"` or `"model-detail-closeup"` + no edge intersect → CenterAndStretch
    - `"closeup-image"` or `"model-detail-closeup"` + edge intersect + qualifies (see DetailCropper det-slot exclusion todo) → DetailCropper
    - `null` or `salient-bbox` UNKNOWN → ProblemImageProcessor
    - all other phenotyped images → CenterAndStretch (or CropSquare if disqualified by edge intersect)
    Routing also reads `OrderEvidence.DetSlot` + product type (see DetailCropper det-slot exclusion todo).

    

-------
- [ ] Define transform failure, fallback, and fill-KO policy: list which transform problems become KO, which eligible fill failures can still export, and what fallback path is used after border-intersection no-reposition cases are excluded.
  - Answer:
    KO the image when:
    - Decoded input is invalid (corrupt, unsupported encoding after format conversion).
    - Image is smaller than `Input.Images.MINIMUM_SIZE_IN_PIXELS` (570 px) in any dimension AND upscaling would exceed `MAXIMUM_UpScale` (1.42×) — so the required output cannot be reached.
    All fill/stretch failures, unknown features, and low-confidence geometry are handled by `Tx_ProblemImageProcessor` (safe resize, export with warnings). No other transform failure triggers KO.

-------
- [ ] Define crop decision output for eligible images: say how crop coordinates, anchors, confidence, and non-repositionable border-intersection decisions are represented.
  - Impact:
    - Project progress: High - Crop decisions are the main transform artifact that diagnostics and output generation need.
    - Effect on other TODOs: Unblocks - It feeds `ImageTransformationResult`, detail crop behavior, center-and-stretch fallback, border-intersection handling, and workbench snapshots.
  - Industry standard:
    Transform stages record requested crop, clamped crop, anchor rules, confidence, and warnings so outputs can be audited.
  - Recommended solution:
    Store crop rectangle, anchor edges, rule name, confidence, clamping/fill requirements, warnings, and the explicit no-reposition state for border-intersecting images.
  - Answer:
    Existing `ImageTransformationResult` fields are sufficient:
    - `CropRectangle` (BoundingBox?) — crop geometry.
    - `BackgroundFillMethod` — fill method used or empty.
    - `Warnings` — anchor notes, quality warnings.
    No new fields required.


-------
- [ ] Define resize decision output: say how preprocessor reports upscale, downscale, or no-resize decisions.
  - Impact:
    - Project progress: Medium - Resize metadata supports quality control and manifest diagnostics.
    - Effect on other TODOs: Influences - It affects `ImageTransformationResult`, output dimensions, config limits, and workbench display.
  - Industry standard:
    Image transforms record input size, target size, scale factor, interpolation method, and whether quality limits were exceeded.
  - Recommended solution:
    Emit resize mode, scale factor, input/output dimensions, interpolation method, and warning when configured upscale/downscale limits are approached.
  - Answer:
    Existing `ImageTransformationResult` fields are sufficient:
    - `ResizeMode` (string) — "upscale", "downscale", or "none".
    - `ScaleFactor` (double) — linear scale factor (1.0 = no resize).
    - `InputWidth` / `InputHeight` and `OutputWidth` / `OutputHeight`.
    - `Warnings` — warning when upscale approaches `MAXIMUM_UpScale` (1.42×).
    No new fields required.

-------
- [ ] Define transform result for border-intersecting detail crops: say how the no-reposition decision is recorded and whether the image exports unchanged or becomes KO.
  - Impact:
    - Project progress: Medium - Border-intersecting detail crops need a deterministic output status after classification has marked them as non-repositionable.
    - Effect on other TODOs: Influences - It depends on border intersection detection and feeds crop decision output, `ImageTransformationResult` status, and manifest projection.
  - Industry standard:
    Crop systems preserve edge contact when content is intentionally or inherently clipped at the source boundary and record when transforms are skipped.
  - Recommended solution:
    Record the border-intersection no-reposition decision, then define whether the unmodified normalized image can be exported with a warning or must become KO.
  - Answer:
    When `Tx_DetailCropper` detects that a crop cannot be repositioned (border intersection blocks manipulation), it delegates to `Tx_CropSquare` internally. The image is not KO'd and not exported unchanged — it receives a square crop without background extension. `ImageTransformationResult.TransformerType` records `Tx_CropSquare`; `Warnings` records the reason for the fallback.

-------
- [ ] Define detail crop saliency map behavior for eligible images: say how the most important object region influences square crop placement when no border intersection blocks repositioning.
  - Impact:
    - Project progress: Medium - Saliency improves detail crops but depends on object bounds and crop policy.
    - Effect on other TODOs: Influences - It affects crop anchors, greedy crop behavior, and transform diagnostics.
  - Industry standard:
    Detail crop algorithms use saliency or attention maps to keep the highest-value region inside the target aspect ratio.
  - Recommended solution:
    Center the square crop on the dominant saliency region while respecting edge anchors and minimum content retention.
  - Answer:

-------
- [ ] Define detail crop headcut thresholds and placement: say which human/head confidence thresholds enable headcut and how top crop placement changes for eligible non-intersecting images.
  - Impact:
    - Project progress: Medium - Headcut rules affect fashion/clothing outputs and must be predictable.
    - Effect on other TODOs: Influences - It depends on human/head traits, top-edge intersection, border-intersection no-reposition handling, and crop anchors.
  - Industry standard:
    Apparel pipelines make person-specific crop rules explicit and gated by confidence to avoid accidental face/head removal.
  - Recommended solution:
    Apply headcut only when configured and the answered human/head detection signals meet explicit thresholds; otherwise preserve the detected head region.
  - Answer:

-------
- [ ] Define detail crop greedy crop behavior for eligible images: say how much original content to keep when no headcut is requested and no border intersection blocks repositioning.
  - Impact:
    - Project progress: Medium - Greedy crop behavior controls balance between detail focus and preserving context.
    - Effect on other TODOs: Influences - It uses saliency, crop decision output, and fill policy.
  - Industry standard:
    Crop systems define minimum content retention and padding rules so outputs are consistent across image sets.
  - Recommended solution:
    Keep as much original content as possible while meeting target aspect ratio and configured margin, using fill only when needed.
  - Answer:

-------
- [ ] Define detail crop fill policy for eligible images: say how missing pixels are created when the crop extends beyond the original image and border-intersection rules do not block manipulation.
  - Impact:
    - Project progress: Medium - Detail crop fill must align with global background fill quality rules.
    - Effect on other TODOs: Influences - It depends on background fill policy and KO rejection rules.
  - Industry standard:
    Missing pixels outside a crop are generated with the least destructive background-compatible method and recorded as a transform operation.
  - Recommended solution:
    Use the global fill policy in priority order and record fill method, confidence, and warnings in `ImageTransformationResult`.
  - Answer:
    Use the global background fill policy from the "background fill policy" todo (tiered by extension ratio). No separate detail-crop fill policy needed.

-------
- [ ] Define center-and-stretch cleanup method: choose the cleanup technique used after stretching or filling background.
  - Impact:
    - Project progress: Medium - Cleanup improves visual quality but follows fill method selection.
    - Effect on other TODOs: Influences - It affects background extension diagnostics and KO policy.
  - Industry standard:
    Post-fill cleanup uses local smoothing, feathering, or inpainting only near seams while preserving subject pixels.
  - Recommended solution:
    Use feathering and local smoothing by default, with optional inpainting for artifacts above a configured threshold.
  - Answer:
    Seam feathering and local smoothing at extension boundaries after edge extension passes.
    Inpainting handles its own seam implicitly when it is the fill method (>142% tier).
    No separate blur pass at any tier.

-------
- [ ] Implement Tx_CenterAndStretch: center salient object on a square canvas and fill or stretch the background.
  - File: `jb/src/core/Images/Transform/Tx_CenterAndStretch.cs` — pixel work gated behind `ImageProcessorAvailable() = false`.
  - What is needed: (1) Read salient-object bounding box from `InputImage.Features` (`salient-bbox` feature, populated by the classifier). (2) Compute canvas offsets to center the object at target resolution. (3) Fill or stretch the background using the decided fill policy. (4) Apply cleanup per the cleanup-method decision. (5) Populate full `ImageTransformationResult` with crop rectangle, fill method, output dimensions, and any warnings.
  - Prerequisites: Transform-facing ImageFeature definition, background fill policy, and cleanup method todos must be answered. `salient-bbox` must be written by the classifier (currently UNKNOWN).
  - Image processor: OpenCV (via EmguCV) or ImageSharp advanced operations for background extension. Inpainting requires a dedicated model if chosen as fill method.

  - Fix: Implement after prerequisites are answered and `salient-bbox` is populated by the classifier.

-------
- [ ] Implement Tx_DetailCropper: square crop anchored at bounding box edges, with optional headcut and greedy crop.
  - File: `jb/src/core/Images/Transform/Tx_DetailCropper.cs` — pixel work gated behind `ImageProcessorAvailable() = false`.
  - What is needed: (1) Read `salient-bbox` from `InputImage.Features`. (2) Detect whether the bounding box intersects an image edge. (3) For non-intersecting images: apply greedy crop centered on saliency region; apply headcut placement when `head-visible` and `hero-is-human` features are above configured thresholds. (4) For border-intersecting images: anchor crop to touched edges; record the no-reposition decision. (5) Apply fill when the crop extends beyond original bounds. (6) Populate full `ImageTransformationResult` including crop rectangle, headcut flag, border-intersection flag, fill method used, and warnings.
  - Prerequisites: Saliency map behavior, headcut thresholds, greedy crop behavior, fill policy, and border-intersection result todos must all be answered. `salient-bbox`, `head-visible`, and `hero-is-human` features must be populated by the classifier.
  - Image processor: Same as Tx_CenterAndStretch.

  - Fix: Implement after all prerequisites are answered and classifier features are available.

-------
- [ ] Implement ImagePreProcessor: EXIF orientation → flat JPG → Canny+local-contrast bounding box → upscale decision.
  - File: `jb/src/core/Images/ImagePreProcessor.cs`
  - Steps in order:
    1. Apply EXIF orientation metadata (rotate/flip pixel data, strip EXIF tag).
    2. Convert to flat single-layer JPG (no alpha, no layers, sRGB).
    3. Compute salient-object bounding box using the Canny + local-contrast approach shown in the Python reference in the same file — port to C# using EmguCV (OpenCV wrapper).
    4. Apply upscale decision based on bbox largest dimension vs config thresholds:
       - < `Input.Images.MINIMUM_SIZE_IN_PIXELS` (570 px) → KO
       - ≥ `Output.Images.Processed.MINIMUM_SIZE_IN_PIXELS` (800 px) → OK, no resize
       - Between 570 and 800 → Upscale; max allowed scale factor = `Output.Images.Resize.MAXIMUM_UpScale` (1.42)
       - Required scale > 1.42 → KO
    5. Return intermediate image bytes and the populated `BoundingBox` to `ImageTransformer`.
  - Answer (proposed transcription from existing data — this todo body + `PRISM-classify.md` "Border Intersection Detection" (Stage 1 salient bounds → Canny → Hough) and the config limits already named in the body; pending approval):
    The five steps and all thresholds are already specified — implement in the stated order, no new constants:
    1. Apply EXIF orientation (rotate/flip pixels, strip the EXIF tag).
    2. Flatten to single-layer sRGB JPG (no alpha, no layers).
    3. Compute the salient-object `BoundingBox` via the in-file Canny + local-contrast Python reference, ported to C# with EmguCV — consistent with `PRISM-classify.md` "Border Intersection Detection" Stage 1 (salient bounds first). `BoundingBox` carries integer `X/Y/Width/Height/Top/Left/Right/Bottom` only (per `PRISM-transform-generate.md` "Salient Object Bounds"); no confidence/method fields.
    4. Upscale decision on the bbox largest dimension against the already-named config limits: `< Input.Images.MINIMUM_SIZE_IN_PIXELS` (570) → KO; `≥ Output.Images.Processed.MINIMUM_SIZE_IN_PIXELS` (800) → OK no resize; 570–800 → upscale capped at `Output.Images.Resize.MAXIMUM_UpScale` (1.42); required scale > 1.42 → KO. This is the same KO rule already stated in the answered "transform failure/fallback/fill-KO policy" todo above.
    5. Return the intermediate bytes + populated `BoundingBox` to `ImageTransformer`; a UNKNOWN/absent bbox routes to `Tx_ProblemImageProcessor` per the routing matrix. No new data introduced.

-------
- [ ] Define and implement the full ImageTransformer routing matrix.
  - File: `jb/src/core/Images/Transform/ImageTransformer.cs` — update `SelectTransformer()`.
  - Decision tree (evaluated in order):
    1. `salient-bbox` is UNKNOWN OR `SelectedPhenotype` is null → `Tx_ProblemImageProcessor`
    2. Any edge intersect is true (`intersects-top/bottom/left/right`):
       a. AND `SelectedPhenotype` is `"closeup-image"` or `"model-detail-closeup"`
       b. AND det-slot is not in the excluded slots for this product type (see det-slot exclusion todo)
       → `Tx_DetailCropper`
       c. Otherwise (intersecting image that does not qualify) → `Tx_CropSquare` (fallback)
    3. No edge intersects → `Tx_CenterAndStretch`
    4. Default → `Tx_CropSquare`
  - Answer:
    Decision tree is confirmed as specified above. Additional clarification:
    - `Tx_DetailCropper` may also internally delegate to `Tx_CropSquare` when it detects a no-reposition case during pixel processing (border intersection blocks manipulation). This is an internal fallback, not a routing decision.
    - `Tx_ProblemImageProcessor` never calls `Tx_CropSquare` — it applies safe resize only.

-------
- [ ] Define the exact det-slot exclusions that prevent routing to Tx_DetailCropper.
  - From the product design:
    - Default product type: images in Det0, Det1, or Det2 are excluded from DetailCropper (→ CropSquare).
    - "Clothing" product type: images in Det0 or Det1 are excluded (→ CropSquare).
  - Open questions that must be answered before implementing the routing matrix:
    - Which product types are classified as "clothing"? Is this a config list or a hard-coded set?
    - Where is product type stored — on `ImageRecord_LAMBDA`, `FamilyIDRecord`, or a separate product-type lookup?
    - Is the exclusion evaluated against `OrderEvidence.DetSlot` (int index) or the `_det#` suffix string?
  - Answer:
    - Clothing product types (from `DetOrderRules.json`): `clothing-tops`, `clothing-bottoms`, `clothing-outerwear`, `clothing-dresses`. The exclusion logic reads the config key prefix "clothing-" — no hard-coded list needed; any product type starting with "clothing-" uses the clothing exclusion rule.
    - Product type must be stored on `ImageRecord_LAMBDA` as `string? ProductTypeId`. `ImageOrderer` writes it when it calls `ResolveProductType()`. Required code change: add `ProductTypeId` field to `ImageRecord_LAMBDA`.
    - Det-slot is `lambda.DetOrder` (int, 0-based from `ImageRecord_Base`).
    - Exclusion rules:
      - Default (non-clothing): det-slots 0, 1, 2 are excluded from `DetailCropper` → `CropSquare`.
      - Clothing (`clothing-*`): det-slots 0, 1 are excluded → `CropSquare`.

-------
- [ ] Document and implement the Tx_CenterAndStretch three-step internal flow.
  - File: `jb/src/core/Images/Transform/Tx_CenterAndStretch.cs`
  - Pre-steps (applied to source image before any pixel repositioning):
    - If `low-contrast` feature is true → run `Tx_LowContrastEnhancement` on source first.
    - If `shadow-present` feature is true → shrink `salient-bbox` bottom edge upward above the detected shadow band.
  - Step 1 — Tight crop: shrink source canvas to the (adjusted) `salient-bbox`, removing excess background.
  - Step 2 — Center: place the cropped object on the target square canvas so the object center aligns with the canvas center, with `Transformation.Positioning.Margin` (4.2%) applied on all sides. This leaves uncovered canvas area where background was removed.
  - Step 3 — Fill: call `Tx_util_BgStretch` on all uncovered canvas edges.
  - Answer (proposed transcription from existing data — this todo body + `PRISM-transform-generate.md` "Repositioning and Margin Application" / "Background Extension"; the two pre-steps are the already-answered routing inputs at the top of this file; pending approval):
    The flow is already specified — fix the order and cross-references rather than invent behavior:
    - Pre-step A (`low-contrast` IF true): run `Tx_LowContrastEnhancement` on the source first (see its own todo — algorithm/scope still open, so this pre-step is gated on that answer).
    - Pre-step B (`shadow-present` IF true): shrink the `salient-bbox` bottom edge upward above the detected shadow band before cropping.
    - Step 1 (tight crop): shrink the source canvas to the adjusted `salient-bbox`, removing excess background — matches doc "crop original image using bounding box coordinates".
    - Step 2 (center): place the cropped object on the target square canvas with object-center aligned to canvas-center and `Transformation.Positioning.Margin` (4.2%) on all sides; this leaves uncovered canvas where background was removed — matches doc "Repositioning and Margin Application". Border-intersection no-reposition does NOT apply here: `Tx_CenterAndStretch` is only reached on the no-edge-intersect routing branch.
    - Step 3 (fill): call `Tx_util_BgStretch` (see its answered tiered-fill todo) on every uncovered canvas edge, then the answered cleanup (seam feathering + local smoothing).
    Populate `ImageTransformationResult` (`CropRectangle`, `BackgroundFillMethod`, resize fields, `Warnings`) per the answered "crop/resize decision output" todos. No new data — all four referenced sub-decisions are already answered in this file or in `PRISM-transform-generate.md`.

-------
- [ ] Implement Tx_util_BgStretch with the confirmed tiered fill strategy.
  - File: `jb/src/core/Images/Transform/Tx_util_BgStretch.cs`.
  - Tiers based on extension ratio (filled canvas area / source image area):
    - ≤125%: basic edge extension (mirror or clamp border pixels outward).
    - ≤142%: content-aware edge extension (patch-based or frequency-aware border propagation).
    - >142%: OpenCV inpainting — INPAINT_TELEA preferred, INPAINT_NS as alternative.
    - >250%: solid white fill (#FFFFFF).
  - Never use Gaussian blur as a fill method.
  - Apply seam feathering at extension boundary after edge extension passes (tiers 1 and 2).
  - Must satisfy the dual-interface todo: `Process(byte[] arr, int stride, float upscale_factor)` webservice form + callable as sub-step from other Tx_ classes.
  - Answer (proposed transcription from existing data — `PRISM-transform-generate.md` "Background Extension → Fill Method — Tiered by Extension Ratio" + ticket `AGENT-TICKETS.md` T-1700; pending approval):
    The strategy is already fully specified and agreed across two accepted artifacts — this answer is reconciliation, not new design. Implement exactly the four tiers keyed on extension ratio (= filled canvas area / source image area):
    - Tier 1 (≤125%): basic edge extension — mirror or clamp border pixels outward.
    - Tier 2 (≤142%): content-aware edge extension — patch-based or frequency-aware border propagation.
    - Tier 3 (>142%): OpenCV inpainting (via EmguCV) — `INPAINT_TELEA` preferred, `INPAINT_NS` alternative.
    - Tier 4 (>250%): solid white fill `#FFFFFF`.
    Rules (same source): never Gaussian blur; apply seam feathering at the extension boundary after tiers 1 and 2 only (tier 3 inpainting handles its own seam implicitly, tier 4 needs none); this is a sub-step helper, NOT an `IImageTransformation` implementor. Expose `Process(byte[] arr, int stride, float upscale_factor)` per the dual-interface contract so it is callable both from the webservice and as a sub-step from `Tx_CenterAndStretch`/`Tx_DetailCropper`. Cleanup is the already-answered "center-and-stretch cleanup method" todo above (feathering + local smoothing at seams; no separate blur pass). No new data introduced.

-------
- [ ] Define and implement Tx_LowContrastEnhancement: decide algorithm and application scope.
  - File: `jb/src/core/Images/Transform/processingtools/Tx_LowContrastEnhancement.cs` (currently empty).
  - Called as a pre-step inside `Tx_CenterAndStretch` when the `low-contrast` ImageFeature is true.
  - Purpose: improve foreground/background separation to sharpen subsequent bounding box accuracy; not a visual quality enhancement for export.
  - Candidate algorithms: CLAHE (Contrast Limited Adaptive Histogram Equalization) via OpenCV/EmguCV, or histogram stretching via ImageSharp.
  - Open question: apply enhancement to the full image or only the detected background region?
  - Answer:

-------
- [ ] Spec and implement Tx_util_HeadCutter: utility class that crops a human head at the nose-to-lips boundary, with family-aware fallback for covered or out-of-shot faces.
  - File: `jb/src/core/Images/Transform/processingtools/Tx_util_HeadCutter.cs` (to be created).
  - Called by `ImageTransformer.cs` before any `Tx_*` script is launched, when `ImageFeatureAnalyzer` has flagged the image with any of: `has-human = true`, `has-head = true`, or `skin-tone-area > 0`.
  - Crop target: the horizontal cut falls between the bottom of the nose and the top of the lips — not at the chin, not at the forehead.
  - Two operating modes:
    1. Family-aware mode (preferred): process the entire FamilyID group together. Detect the face/head position from one or more images in the group where the face is clearly visible, then apply the derived cut line consistently to all images in the family — including those where the face is covered (e.g. helmet, mask) or out of shot. This produces visually consistent headcuts across the family.
    2. Per-image mode (fallback / public webservice mode): detect and cut the head individually per image. Used when no family context is available (e.g. called via the raw-bytes webservice interface).
  - Open questions to answer before implementing:
    - Which facial landmark model / library should be used to locate the nose-bottom and lip-top coordinates? (e.g. MediaPipe Face Mesh, dlib 68-point, ONNX face landmark model)
    - In family-aware mode, what is the minimum number of clear-face images in the family required to derive the shared cut line? What happens when that threshold is not met (fall back to per-image, or skip headcut entirely)?
    - Should the cut line be a straight horizontal crop, or can it be a slight curve / soft mask to avoid a harsh edge?
    - How is the derived cut position represented and passed to the Tx_ caller (e.g. a pixel Y-coordinate on the normalized image, or a ratio of image height)?
  - Signature must satisfy the dual-interface todo: `Process(byte[] arr, int stride, float upscale_factor)` for webservice use (per-image mode); family-aware mode is PRISM-internal only and receives the Lambda collection for the family.
  - Answer: