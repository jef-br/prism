# Pipeline Stage Shells Todo

- [ ] Replace ExportStageShell.Run() stub with real Exporter delegation.
  - File: `jb/src/core/Pipeline/StageShells.cs` line 390.
  - Block: Ticket T-1100 (Exported Stage) is blocked by T-1000. `Exporter.cs` itself is also a comment-only stub. The shell currently only calls `MarkStageCompleted`.
  - Fix: When T-1100 is activated, delegate to `Exporter.cs` (implement zip/JSON export there first). Remove the TODO comment.
