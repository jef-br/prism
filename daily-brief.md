# Daily Brief

##### Changed
- None. No new commits since the 07/07 brief (HEAD still b9de2d0 on origin/main), working tree clean. Prior brief's shipped work (det-order compaction, model-wide empty-column prune, re-blessed CiMini golden, standalone upscaler test client) already captured.

##### Todo updates
- Transform/jbtodo.md (second HeadCutter item, "spec + implement"): filled the empty answer from existing code only. The class it calls "to be created" already exists at `Tx_util_HeadCutter.cs` (not the `processingtools/` path listed); Algorithm B ships as internal `Analyze(lambda, Mat)`. Recorded what the shipped code de facto answers — no landmark model (Haar face-box + fixed `cutY = faceBox.Y + 0.75*height`, so nose-to-lips is an assumed 75%-of-box constant not a measured line), straight full-width crop, cut applied by mutating `ProcessedBytes`/`BoundingBox` in place, lowest-centroid face pick — and kept the real product calls open (family-aware mode + min-clear-face threshold unimplemented, webservice `Process()` signature unimplemented, landmark-vs-heuristic still your decision). Left explicitly non-final.
- Everything else unimprovable without guessing: root-jbtodo CiMini table + MEPAL4 already landed as shipped code (captured last brief); Order/T-2830 answer (a) already implemented; Services test-split residual is a user overhead call; Classify + Generate items FROZEN; HeadCutter Algorithm A crown-offset still blocked on the anatomical-ratio deepdive; jb/src/jbtodo.md's MMERO26/HEROAUT2/HEROAUT3 `???` entries need actual job runs, no output on disk to read.

##### Next steps
- Run MMERO26/HEROAUT2/HEROAUT3 to close the three `???` entries (01/07) in `jb/src/jbtodo.md` — HEROAUT2 is expected fast-KO (no familyID); record OK-rate/KO-timing.
- Run `Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` 5x back-to-back to confirm byte-identical det0-based assignment, then close T-2820/T-2830 and pin `expected-manifest.json`.
- `dotnet test jb/src/PRISM.sln` to confirm the compaction + empty-column-prune tests are green before trusting the golden.
- Decide the Services per-service `.csproj` split (mechanical; only multi-project-overhead-vs-defer is open) as the single test project keeps accreting stage tests.
