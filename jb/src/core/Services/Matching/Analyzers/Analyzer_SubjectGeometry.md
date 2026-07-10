# Analyzer_SubjectGeometry

**Status:** implemented - **Wave:** 3 - **Writes:** `salient-bbox`, `image-occupancy`, `product-coverage-ratio`, `crop-tightness`, `product-aspect-ratio`, `vertical-centering`, `horizontal-centering`

## How it works
Subject box = highest-confidence YOLO detection (works on any background - no gating), else the bounding rectangle of pixels far from the border background color, else UNKNOWN (never guess). All geometric features derive from the box.

## Limitations / milestones
- [ ] `product-coverage-ratio` is a box-area approximation. Milestone: yolo26s-seg (or similar) provides pixel masks for true coverage. Same milestone retires the color-distance fallback entirely.
- [ ] Fallback box on transparent-background images should use alpha instead of color distance.
