# Image Transform Todo

- [ ] Tx_util_HeadCutter Algorithm A — anatomy-guided search space refinement: when `has-human == true`, use the lambda BoundingBox dimensional proportions combined with human anatomical ratios (e.g. head ≈ 1/8 of body height) to narrow the Haar face-detection search region before running DetectMultiScale. Requires a deepdive into apparel-image anatomical ratio distributions to determine reliable constants.
  - File: `jb/src/core/Images/Transform/Tx_util_HeadCutter.cs`
  - Blocked until: anatomical ratio constants are agreed upon.
  - Answer:
    - The ratio of head-to-body should lie between 1:4 (kids) and 1:8 (adults) anything outside that is weird
    - 

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
