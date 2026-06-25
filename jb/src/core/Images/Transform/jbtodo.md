# Image Transform Todo

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
- [ ] Implement Tx_DetailCropper: square crop anchored at bounding box edges, with optional headcut and greedy crop.
  - File: `jb/src/core/Images/Transform/Tx_DetailCropper.cs` — pixel work gated behind `ImageProcessorAvailable() = false`.
  - What is needed: (1) Read `salient-bbox` from `InputImage.Features`. (2) Detect whether the bounding box intersects an image edge. (3) For non-intersecting images: apply greedy crop centered on saliency region; apply headcut placement when `head-visible` and `hero-is-human` features are above configured thresholds. (4) For border-intersecting images: anchor crop to touched edges; record the no-reposition decision. (5) Apply fill when the crop extends beyond original bounds. (6) Populate full `ImageTransformationResult` including crop rectangle, headcut flag, border-intersection flag, fill method used, and warnings.
  - Prerequisites: All saliency map, headcut, greedy crop, fill policy, and border-intersection todos above must be answered. `salient-bbox`, `head-visible`, `hero-is-human` features must be populated by the classifier.
  - Image processor: Same as Tx_CenterAndStretch.
  - Fix: Implement after all prerequisites are answered and classifier features are available.

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
