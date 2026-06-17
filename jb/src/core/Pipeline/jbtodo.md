# Pipeline Module Todo

## Architecture

- [ ] Split `StageShells.cs` into one file per stage shell class.
  - Rule: every C# class must be in its own `.cs` file named after the type.
  - Types to split out (each to its own file in `jb/src/core/Pipeline/`):
    - `ImportStageShell.cs`
    - `ClassifyStageShell.cs`
    - `MatchStageShell.cs`
    - `OrderStageShell.cs`
    - `RenameStageShell.cs`
    - `GenerateStageShell.cs`
    - `TransformStageShell.cs`
    - `ExportStageShell.cs`
  - Current file: `jb/src/core/Pipeline/StageShells.cs` — 8 classes, ~430 lines.
  - Note: `ClassifyStageShell` is the largest at ~205 lines; it will become the biggest standalone file.
  - Answer:
