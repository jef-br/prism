# Analyzer_Packaging

**Status:** STUB - **Wave:** 3 - **Will write:** `packaging-visible`, `scale-reference-present`

## Proposed workings
packaging-visible: CLIP prompts ("product in its retail box/blister/bottle" vs "without packaging"); YOLO bottle/box-adjacent classes support FMCG types. scale-reference-present: CLIP prompts for hand/coin/ruler; a small person box relative to the subject box supports "held product". Feeds the `packaging-shot` and `scale-reference-shot` phenotypes.
