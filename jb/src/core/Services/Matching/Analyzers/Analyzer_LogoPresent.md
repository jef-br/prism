# Analyzer_LogoPresent

**Status:** STUB - **Wave:** 3 - **Will write:** `logo-present`

## Proposed workings
Logo = small, compact, high-contrast, color-consistent connected region distinct from product texture. Connected components on the gradient map inside the subject box, filtered by relative size (0.2-5%), compactness, low internal color variance. Upgrade path: small logo-detection ONNX if prints/patterns cause too many false positives.
