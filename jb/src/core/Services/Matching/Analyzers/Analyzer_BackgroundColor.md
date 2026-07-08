# Analyzer_BackgroundColor

**Status:** implemented - **Wave:** 3 - **Writes:** `background-color`

## How it works
Only when `background-type == SOLIDCOLOR`: mean color of the 5% border strips (same estimate as the edge detector) maps to the nearest palette name. REALLIFE/UNKNOWN backgrounds stay UNKNOWN - a mean over a scene names nothing.

## Open questions
- [ ] Gradient studio backgrounds (white to grey sweep) - is the border mean representative enough?
