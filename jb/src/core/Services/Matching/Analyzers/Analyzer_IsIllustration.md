# Analyzer_IsIllustration

**Status:** implemented (pre-existing, now config-driven) - **Stage:** Classified phase 1 - **Writes:** `is-illustration`

## How it works
Three signals must all pass: high-frequency edge density >= 12%, near-white border flatness >= 80%, <= 8 populated color clusters. Feeds `illustration-technical-drawing`. Thresholds now in analyzer_Config.json (`IsIllustration` section).

## Open questions
- [x] ~~PRISM-classify.md path note points at Classify/Analyzers/~~ — checked 2026-08-05 (T-4000 split pass): `PRISM-classify.md:268` already reads `jb/src/core/Services/Matching/Analyzers/Analyzer_IsIllustration.cs`, the correct post-restructure path. The todo outlived its defect, and its own suggested path (`Images/Analyzers/`) was itself made stale by the 2026-07-08 `Services/`/`lib/` split. Nothing to fix.

No open calibration questions remain on this analyzer.
