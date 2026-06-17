# Models Module Todo

## Architecture

- [ ] Split `ImageRecord_LAMBDA.cs` — `TagCollection` is a second type in the same file.
  - Rule: every C# class/record/struct must be in its own `.cs` file named after the type.
  - Type to extract: `TagCollection` (line 107) → `jb/src/core/Models/TagCollection.cs`.
  - Current file: `jb/src/core/Models/ImageRecord_LAMBDA.cs`.
  - Answer:

## Spec deviations

- [ ] SD-8: `ImageRecord_OUTPUT` missing `Width`, `Height`, and `Checksum` fields.
  - File: `jb/src/core/Models/ImageRecord_OUTPUT.cs`.
  - Spec says (`PRISM-models.md`): `ImageRecord_OUTPUT` must carry `Width`, `Height`, and `Checksum` (when available) as required fields.
  - Current behavior: Neither field exists on the record. `Exporter.cs` reads width and height from the lambda record to build output, but those values cannot be stored on the output record because the fields are absent.
  - Fix: Add `int Width`, `int Height`, and `string? Checksum` to `ImageRecord_OUTPUT`. Populate them in `Exporter.BuildOutputRecord` from the normalized artifact dimensions.
  - Answer:
