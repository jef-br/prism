# IO Import Todo

- [ ] Fast-path already-conforming images in normalization: skip the full decode + JPEG re-encode when an input is already a conforming JPEG (no EXIF orientation rotation needed, no alpha channel). This eliminates the most expensive per-image step for inputs that are already clean — the common case for many supplier datasets — and composes with the parallel import loop (T-3000).
  - File: `jb/src/core/IO/Import/Importer.cs` (`TryNormalizeToJpeg` / `NormalizeAndRecord`).
  - Primary open question (product/architecture decision needed before implementing):
    - When an image already conforms, should import (a) COPY the source file into the job `normalized/` folder unchanged, or (b) SKIP the copy and point `ImageRecord_INPUT.NormalizedJpgPath` directly at the original source path?
    - This hinges on whether any downstream stage assumes `normalized/` physically contains every image and/or writes in place. If Transform / Generate / Export read-modify-write the normalized artifact (rather than writing new outputs elsewhere), option (b) would corrupt the user's original input and is unsafe → option (a) required. Confirm the normalized-artifact contract before choosing.
  - Secondary questions:
    - Conformance test: is "JPEG + no EXIF orientation tag + no alpha" the correct/sufficient definition, or must import also enforce max dimensions / strip color profiles / cap quality?
    - Detection cost: use ImageSharp `Image.Identify` (metadata-only, no full decode) to read format + EXIF orientation + alpha cheaply; confirm `Identify` reliably reports these for every accepted format.
  - Depends on: T-3000 (parallelize import) should land first so this optimization layers on top of the parallel loop.
  - Answer:
