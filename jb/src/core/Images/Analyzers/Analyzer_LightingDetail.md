# Analyzer_LightingDetail

**Status:** STUB - **Wave:** 3 - **Will write:** `lighting`, `lighting-detail`

## Proposed workings
lighting EASY/HARD from histogram shape (high-key studio = upper-range mass with smooth rolloff = EASY; harsh bimodal shadows = HARD) + gradient-direction coherence (one dominant light direction vs scattered). Share the histogram pass with Analyzer_Exposure when implementing. lighting-detail carries raw descriptors for diagnostics.
