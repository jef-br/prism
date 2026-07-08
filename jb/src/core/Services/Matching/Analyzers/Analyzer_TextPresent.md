# Analyzer_TextPresent

**Status:** STUB - **Wave:** 3 - **Will write:** `text-present`

## Proposed workings
Text = many small, high-contrast, similarly-sized connected strokes aligned in rows. Cheap first: stroke-width-transform / MSER-style connected components on the gradient map. Upgrade path: EAST or DBNet text-detection ONNX (both small) if heuristic precision is insufficient. Directly unblocks the `size-chart` phenotype rule (text-present + coverage/occupancy already measured).

## To decide
- [ ] Heuristic vs ONNX first - prototype the heuristic on real size charts and care labels.
