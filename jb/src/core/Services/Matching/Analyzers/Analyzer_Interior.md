# Analyzer_Interior

**Status:** implemented (pre-existing, now config-driven) - **Stage:** Classified phase 1 - **Writes:** `interior-detected`

## How it works
Sobel gradient + patch grid scan: a large enclosed cavity (interior smoother than its surrounding ring, bounded by strong edges, well inside the frame) sets interior-detected. Feeds the `interior-shot` phenotype. Thresholds now in analyzer_Config.json (`Interior` section).

## Open questions
- [ ] Product-type gating docs disagree: code comment says "wallet/bag/suitcase only", PRODUCTTYPES.MD table (pre-T-4700) named clothing-outerwear, bags-accessories, beauty-cosmetics, electronics-large, homeware-hard, furniture — all but `bags-accessories` were retired when DetOrderRules.json/ProductTypeMap.json collapsed to 5 product types (default, topwear, bottomwear, footwear, bags-accessories). Reconcile the remaining `bags-accessories`-only question and fix the docs (also the stale `InteriorAnalyzer.cs` name in PRISM-classify.md - real file is Analyzer_Interior.cs).
