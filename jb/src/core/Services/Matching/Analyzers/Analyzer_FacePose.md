# Analyzer_FacePose

**Status:** STUB (highest-value stub) - **Wave:** 2 - **Will write:** `has-head`, `head-visible`, `has-face`, `face-visible`, `body-visible`, `pose-type`

## Proposed workings
Gated on a YOLO person detection; runs inside the person box only:
- Faces/heads: OpenCV Haar cascades (frontal + profile) over the top of the person box (OpenCV in Classify is approved - all heavy lifting in one spot). The old code hint applies: "Haar-Features can help detect facial features."
- body-visible (full/three-quarter/half/bust): person-box-to-frame ratio + which borders the box intersects.
- pose-type (standing/sitting/crouching/lying): box aspect ratio first cut; yolov8n-pose ONNX if too coarse.

## To decide
- [ ] Haar cascades vs yolov8n-pose from the start (pose model gives keypoints, so head/body/pose come in one pass).
- [ ] These features currently come from CLIP for 4 dims - resolve write-precedence (confidence comparison like FilenameEvidence).
