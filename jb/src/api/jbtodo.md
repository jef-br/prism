# API Module Todo

## Architecture

- [ ] Split `PrismApiModels.cs` into one file per type.
  - Rule: every C# class/record/struct must be in its own `.cs` file named after the type.
  - Types to split out (each to its own file in `jb/src/api/`):
    - `PrismHealthResponse.cs`
    - `PrismSafeConfigResponse.cs`
    - `PrismVisibleFeatureFlags.cs`
    - `PrismQueueConfigResponse.cs`
    - `PrismJobStartEnvelope.cs`
    - `PrismPreCoreErrorResponse.cs`
    - `PrismJsonResultEnvelope.cs`
    - `PrismJsonImagesEnvelope.cs`
    - `PrismJobUrls.cs`
  - Current file: `jb/src/api/PrismApiModels.cs` — 9 types.
  - Answer: Implement.

## Spec deviations

- [ ] SD-13: JSON output `images` shape is a flat `ManifestImageRow` list — deviates from spec.
  - File: `jb/src/api/PrismApiModels.cs` — `PrismJsonImagesEnvelope.Ok` and `.Ko` are `IReadOnlyList<ManifestImageRow>`.
  - Spec says (`PRISM-api.md`): each item in `images.ok[]` and `images.ko[]` must be:
    ```json
    {
      "sourceReference": "...",
      "lambda": { /* bounded ImageRecord_LAMBDA journey data */ },
      "output": { /* ImageRecord_OUTPUT or null */ }
    }
    ```
  - Current behavior: `ManifestImageRow` is a flat projection of the manifest. The per-image journey (`lambda`, `output`) is not serialized. Frontend visualisation of what happened to each image is not possible from the JSON result.
  - Fix requires: a new `ImageJourneyItem` record (or equivalent) that wraps `sourceReference`, `lambda` data, and `output` data; update `PrismJsonImagesEnvelope`; update `Exporter.cs` to project each `ImageRecord_LAMBDA` into the journey shape.
  - Answer (proposed from existing docs — PRISM-api.md "JSON Output" per-journey-item shape + PRISM-models.md IRL/IRO; pending approval):
    The spec is authoritative and the documented Fix is the right shape — implement it. Existing data already defines every part:
      - `sourceReference` = MIR.`SourceReference` / IRI original filename or URL (safe provenance).
      - `lambda` = the bounded IRL journey, i.e. IRL's "ordered per-image route list for web visualization (stage name, status, safe message, bounded evidence, optional diagnostic ref)" — not the flat manifest row. Per PRISM-api.md this is exactly what lets the frontend visualise what happened to each image.
      - `output` = IRO projection (final filename, dimensions, MIME, export status) when an exportable artifact exists; `null` for KO items (PRISM-api.md: "`output` is `null` for KO items"). Default JSON export embeds no image bytes.
    So `PrismJsonImagesEnvelope.Ok`/`.Ko` should be `IReadOnlyList<ImageJourneyItem>`, and `Exporter.cs` projects each `ImageRecord_LAMBDA` into `{ sourceReference, lambda, output }`. No new spec data is introduced — this only realigns the implementation with the existing PRISM-api.md contract.
