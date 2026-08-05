# Analyzer_SubjectGeometry

**Status:** implemented - **Wave:** 3 - **Writes:** `salient-bbox`, `image-occupancy`, `product-coverage-ratio`, `crop-tightness`, `product-aspect-ratio`, `vertical-centering`, `horizontal-centering`

## How it works
Subject box = highest-confidence YOLO detection (works on any background - no gating), else the bounding rectangle of pixels far from the border background color, else UNKNOWN (never guess). All geometric features derive from the box.

## Limitations / milestones
- [ ] `product-coverage-ratio` is a box-area approximation. Milestone: yolo26s-seg (or similar) provides pixel masks for true coverage. Same milestone retires the color-distance fallback entirely.

## Retired
- *Fallback box on transparent-background images should use alpha instead of color distance* — retired 2026-08-05 with [[T-4960]]. T-5030 made Import composite every input onto white and emit JPG before any analyzer runs, so no transparent-background image and no alpha-derived box exists downstream of Import. The colour-distance fallback is now the only producer for these images rather than the worse of two, and there is nothing left to prefer over it.
