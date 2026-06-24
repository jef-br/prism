# Image Transform Todo

-------
- [ ] HANDMADE BY ME: Temporarily GATE the phenotypes so we can get basic transformations online.
  - Status: gate implemented as `ImageTransformer.BypassPhenotypes` (currently `true`). While on, transform routing ignores `SelectedPhenotype` and decides off geometry only (`salient-bbox` + edge intersects): bbox present + no intersect → `Tx_CenterAndStretch`; bbox + intersect → `Tx_CropSquare`; no bbox → `Tx_ProblemImageProcessor`. `Tx_DetailCropper` (phenotype-driven) is unreachable while bypassing. Flip the flag to `false` once phenotype assignment is validated; this todo stays open until then.

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
- [ ] Implement Tx_CenterAndStretch: center salient object on a square canvas and fill or stretch the background.
  - File: `jb/src/core/Images/Transform/Tx_CenterAndStretch.cs` — pixel work gated behind `ImageProcessorAvailable() = false`.
  - What is needed: (1) Read salient-object bounding box from `InputImage.Features` (`salient-bbox` feature, populated by the classifier). (2) Compute canvas offsets to center the object at target resolution. (3) Fill or stretch the background using the decided fill policy. (4) Apply cleanup per the cleanup-method decision. (5) Populate full `ImageTransformationResult` with crop rectangle, fill method, output dimensions, and any warnings.
  - Prerequisites: `salient-bbox` must be written by the classifier (currently UNKNOWN). Saliency map, headcut, and greedy crop todos above must be answered.
  - Image processor: OpenCVSharp4 for background extension; inpainting requires no additional model (INPAINT_TELEA/INPAINT_NS).
  - Fix: Implement after `salient-bbox` is populated and remaining sub-todos are answered.

-------
- [ ] Implement Tx_DetailCropper: square crop anchored at bounding box edges, with optional headcut and greedy crop.
  - File: `jb/src/core/Images/Transform/Tx_DetailCropper.cs` — pixel work gated behind `ImageProcessorAvailable() = false`.
  - What is needed: (1) Read `salient-bbox` from `InputImage.Features`. (2) Detect whether the bounding box intersects an image edge. (3) For non-intersecting images: apply greedy crop centered on saliency region; apply headcut placement when `head-visible` and `hero-is-human` features are above configured thresholds. (4) For border-intersecting images: anchor crop to touched edges; record the no-reposition decision. (5) Apply fill when the crop extends beyond original bounds. (6) Populate full `ImageTransformationResult` including crop rectangle, headcut flag, border-intersection flag, fill method used, and warnings.
  - Prerequisites: All saliency map, headcut, greedy crop, fill policy, and border-intersection todos above must be answered. `salient-bbox`, `head-visible`, `hero-is-human` features must be populated by the classifier.
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
  - Answer:

-------
- [ ] Document and implement the Tx_CenterAndStretch three-step internal flow.
  - File: `jb/src/core/Images/Transform/Tx_CenterAndStretch.cs`
  - Pre-steps (applied to source image before any pixel repositioning):
    - If `low-contrast` feature is true → run `Tx_LowContrastEnhancement` on source first.
    - If `shadow-present` feature is true → shrink `salient-bbox` bottom edge upward above the detected shadow band.
  - Step 1 — Tight crop: shrink source canvas to the (adjusted) `salient-bbox`, removing excess background.
  - Step 2 — Center: place the cropped object on the target square canvas so the object center aligns with the canvas center, with `Transformation.Positioning.Margin` (4.2%) applied on all sides. This leaves uncovered canvas area where background was removed.
  - Step 3 — Fill: call `Tx_util_BgStretch` on all uncovered canvas edges.
  - Answer:

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
  - Must satisfy the dual-interface: `Process(byte[] arr, int stride, float upscale_factor)` webservice form + callable as sub-step from other Tx_ classes.
  - Answer:

-------
- [ ] Define and implement Tx_LowContrastEnhancement: decide algorithm and application scope.
  - File: `jb/src/core/Images/Transform/processingtools/Tx_LowContrastEnhancement.cs` (currently empty).
  - Called as a pre-step inside `Tx_CenterAndStretch` when the `low-contrast` ImageFeature is true.
  - Purpose: improve foreground/background separation to sharpen subsequent bounding box accuracy; not a visual quality enhancement for export.
  - Candidate algorithms: CLAHE (Contrast Limited Adaptive Histogram Equalization) via OpenCVSharp4, or histogram stretching via ImageSharp.
  - Open question: apply enhancement to the full image or only the detected background region?
  - Answer:

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
  - Answer:
