# Analyzer_ProductColor

**Status:** implemented - **Wave:** 3 - **Writes:** `product-color`

## How it works
Largest surviving dominant-color bucket maps to the nearest named color in the configured palette (`Colors.Palette` in analyzer_Config.json). Confidence configurable (`Colors.ProductColorConfidence`, default 0.80).

## Open questions
- [ ] Palette granularity: 12 names now; client color vocabularies may need more (navy vs blue, bordeaux vs red).
- [ ] Nearest-RGB is crude for dark/desaturated colors - consider LAB distance (see ImageFeatures.md dominant-colors decision).
