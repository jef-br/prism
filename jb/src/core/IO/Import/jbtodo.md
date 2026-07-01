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
    NOT FINALIZED — decision (a)/(b) still reserved for you. The following only resolves the
    todo's *primary open question* ("confirm the normalized-artifact contract before choosing")
    from existing code, so the choice can be made on facts rather than assumption.

    Normalized-artifact contract — VERIFIED read-only at every downstream consumer:
    - Transform: `TransformService.cs:55` passes `input.NormalizedJpgPath` into
      `ImagePreProcessor.Preprocess` as a read-only input path; the preprocessor `Image.Load`s it
      (`ImagePreProcessor.cs:125`) and encodes the result into a `MemoryStream`
      (`ImagePreProcessor.cs:134`) — never back to the path. Transform output lives only in
      `lambda.ProcessedBytes` (in memory), never written to disk in place.
    - Export: `Exporter.cs` prefers `lambda.ProcessedBytes` for OK images (`Exporter.cs:90`) and
      reads the file only as a fallback when those bytes are null (`Exporter.cs:93-95`) and for KO
      images (`Exporter.cs:107-110`). Read-only.
    - Match: `MatchingService.cs:199` `Image.Load<Rgba32>(source.NormalizedJpgPath)` — read-only.
    - No `File.Write/Copy/Move` or `Save(<path>)` anywhere in core targets `NormalizedJpgPath`;
      every `.Save(...)` in the Transform stage writes to a `MemoryStream`, not a file.

    Implication for the (a)/(b) choice: the safety blocker this todo names — "downstream
    read-modify-write would corrupt the user's original under option (b)" — does NOT exist in the
    current code, so it does not by itself force option (a). Two residual considerations remain
    (both from existing code) to weigh before choosing (b):
    1. Export reads `NormalizedJpgPath` directly for KO images and as the OK fallback
       (`Exporter.cs:93-110`); under (b) those reads would serve the user's *original* file into
       the export ZIP. Fine only if that is the intended KO/fallback content.
    2. `NormalizedJpgPath` today always points inside `jobTempFolder/normalized/`
       (`Importer.cs:296-326`); under (b) it would point outside the job temp folder at the
       original source, whose lifetime/cleanup is owned elsewhere — confirm the source path stays
       valid through Export before adopting (b).
