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
    - File-path correction: the class already exists at `jb/src/core/Images/Transform/Tx_util_HeadCutter.cs` (not the `processingtools/` path listed above). It ships the per-image Algorithm B path as an internal `Analyze(ImageRecord_LAMBDA lambda, Mat colorMat)`, reusing the BGR Mat from ImagePreProcessor (no second decode).
    - Open questions the shipped code has *de facto* answered (read off the code, not a decision):
      - Landmark model: none. Pipeline is Haar face-box → fixed proportion, not landmarks. `CascadeClassifier` on `haarcascade_frontalface_default.xml` (`DetectMultiScale` over the full gray frame) yields a face rect; the nose-to-lips cut is approximated as `cutY = faceBox.Y + 0.75*faceBox.Height`. So "nose-to-lips" is an assumed 75%-of-face-box constant, not measured — accuracy rides entirely on how consistently Haar frames the face, and there is no landmark evidence to place the actual nose/lip line.
      - Crop shape: straight full-width horizontal cut (`SubMat(0, cutY, cols, rows-cutY)`), re-encoded to JPEG. No curve, no soft mask.
      - Cut delivery: not returned as a Y-coordinate or height ratio. The utility mutates `lambda.ProcessedBytes` and shifts `lambda.BoundingBox` up by `cutY` in place — PRISM-internal collection path only.
      - Multi-face pick: qualifies only faces whose centroid sits in the top half (`f.Y + f.Height/2 < imageHeight/2`), then picks the one furthest from the top edge (lowest centroid Y).
    - Still genuinely open (unchanged, needs your call — the code does NOT settle these):
      - Family-aware mode is not implemented. Only per-image detection exists; no shared cut line derived across a family, so the "minimum clear-face images / fallback threshold" question is untouched.
      - The webservice `Process(byte[], int, float)` per-image signature is not implemented — only the internal `Analyze` path exists.
      - Whether to replace the 0.75-of-face-box heuristic with a real landmark model for the true nose-to-lips line (ties into Algorithm A's crown-offset deepdive above).
