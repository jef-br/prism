# Analyzer_Exposure

**Status:** implemented - **Wave:** 3 - **Writes:** `overexposed`, `underexposed`

## How it works
Sampled luminance histogram; on SOLIDCOLOR backgrounds, background-like pixels are excluded first so a white packshot is not flagged. Flags flip when the blown-out (>= 0.98) or crushed (<= 0.02) fraction of counted pixels exceeds `Exposure.FlaggedFraction`.

## Open questions
- [ ] FlaggedFraction 0.25 is a guess - calibrate against images a retoucher would actually reject.
