# Analyzer_Interior

**Status:** implemented (pre-existing, now config-driven) - **Stage:** Classified phase 1 - **Writes:** `interior-detected`

## How it works
Sobel gradient + patch grid scan: a large enclosed cavity (interior smoother than its surrounding ring, bounded by strong edges, well inside the frame) sets interior-detected. Feeds the `interior-shot` phenotype. Thresholds now in analyzer_Config.json (`Interior` section).

## Open questions
- [ ] Product-type gating docs disagree: code comment says "wallet/bag/suitcase only", PRODUCTTYPES.MD table says clothing-outerwear, bags-accessories, beauty-cosmetics, electronics-large, homeware-hard, furniture. Reconcile and fix the docs (also the stale `InteriorAnalyzer.cs` name in PRISM-classify.md - real file is Analyzer_Interior.cs).
