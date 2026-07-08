# Analyzer_IsIllustration

**Status:** implemented (pre-existing, now config-driven) - **Stage:** Classified phase 1 - **Writes:** `is-illustration`

## How it works
Three signals must all pass: high-frequency edge density >= 12%, near-white border flatness >= 80%, <= 8 populated color clusters. Feeds `illustration-technical-drawing`. Thresholds now in analyzer_Config.json (`IsIllustration` section).

## Open questions
- [ ] PRISM-classify.md path note points at Classify/Analyzers/ - real path is Images/Analyzers/. Fix doc.
