# Analyzer_FilenameEvidence

**Status:** implemented - **Wave:** 1 - **Writes:** `ProductTypeId` (fallback), `hero-orientation`

## How it works
`hoodie_4435345_A_FRONT.jpg` gives product type "hoodie" (topwear) and orientation FRONT.
Filename stem tokenized on non-alphanumerics. Product type only when the IEM gave none (never overrides Excel). Orientation tokens (front/back/side/top/bottom/diagonal + multilingual variants) write `hero-orientation` at the configured confidence, only when the current value is UNKNOWN or weaker.

## Open questions
- [ ] Orientation token list is hard-coded - move to config if client vocabularies diverge.
- [ ] Ambiguous tokens ("angle" -> DIAGONAL) need real-batch validation.
