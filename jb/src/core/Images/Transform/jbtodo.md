# Image Transform Todo

- [x] Define detail crop saliency map behavior for eligible images: say how the most important object region influences square crop placement when no border intersection blocks repositioning.
  - Answer:
    The BoundingBox produced by ImagePreProcessor is the saliency anchor for all Transform stage work. No additional saliency computation happens in the Transform stage. Tx_CenterAndStretch centers the BoundingBox on the canvas.

-------
- [x] Define detail crop headcut thresholds and placement: say which human/head confidence thresholds enable headcut and how top crop placement changes for eligible non-intersecting images.
  - Answer:
    Controlled by a job-level `Headcut` bool in `PrismProcessingParameters`, threaded through the Transform service chain. No classification confidence check in Transform. Human presence is determined by Analyzer_HasHuman (runs in ImagePreProcessor). Face position is found by Tx_util_HeadCutter's Haar cascade.

-------
- [x] Define detail crop greedy crop behavior for eligible images: say how much original content to keep when no headcut is requested and no border intersection blocks repositioning.
  - Answer:
    The original image is NOT cropped to the BoundingBox. The original image is repositioned so the BoundingBox center aligns with the canvas center. Background pixels outside the BoundingBox are stretched by Tx_util_BgStretch in all 4 directions to cover uncovered canvas edges.

-------
- [ ] Tx_util_HeadCutter Algorithm A — anatomy-guided search space refinement: when `has-human == true`, use the lambda BoundingBox dimensional proportions combined with human anatomical ratios (e.g. head ≈ 1/8 of body height) to narrow the Haar face-detection search region before running DetectMultiScale. Requires a deepdive into apparel-image anatomical ratio distributions to determine reliable constants.
  - File: `jb/src/core/Images/Transform/Tx_util_HeadCutter.cs`
  - Blocked until: anatomical ratio constants are agreed upon.
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
