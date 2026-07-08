# Analyzer_DominantColors

**Status:** implemented - **Wave:** 3 - **Writes:** `dominant-colors` (top-4 hex list)

## How it works
Samples inside the subject box, excluding background-like pixels (distance to border estimate) and skin-tone pixels (a model skin tone is never a product color). Survivors quantize into an 8x8x8 RGB grid; top 4 buckets above the minimum share, strongest first.

## Hard cases (calibrate on real batches)
- [ ] White product on white background: exclusion eats everything, so UNKNOWN by design. Verify MinSampleFraction does this reliably without killing pale products.
- [ ] Skin-colored product on a human (tan bathing suit on tanned model): skin exclusion may eat the product. Consider restricting skin exclusion to person-box pixels once FacePose lands.
