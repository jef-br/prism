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
  - Answer:
