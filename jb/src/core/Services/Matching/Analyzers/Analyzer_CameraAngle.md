# Analyzer_CameraAngle

**Status:** STUB - **Wave:** 3 - **Will write:** `camera-angle`, `top-view`

## Proposed workings
- Combine cheap signals: subject-box vertical placement/aspect (overhead flat lays: wide, centered, shadow-free), shadow direction below the subject, and CLIP prompts ("photographed from directly above" / "at eye level") to arbitrate.
- Filename TOP tokens already contribute via Analyzer_FilenameEvidence.
- Line detection to determine vanishing points *might* be useful
- could exif metadata still be used at this point?


