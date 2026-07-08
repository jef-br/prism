# Analyzer_Mannequin

**Status:** STUB - **Wave:** 2 - **Will write:** `contains-mannequin`

## Proposed workings
Mannequin = person-shaped but not human: YOLO person detection + near-zero skin-tone-area inside the person box + no face from Analyzer_FacePose sets contains-mannequin true. CLIP prompt pair ("garment on a mannequin" vs "on a person") arbitrates borderline cases. Depends on Analyzer_FacePose.
