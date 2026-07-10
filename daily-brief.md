# Daily Brief

##### Changed
- Prior brief baseline was stale (claimed HEAD b9de2d0, "None changed"); tree is now at 172bde6 with a large batch landed. Deltas below.
- Restructure live: `jb/src/core/` split into `Services/` (`Prism.Services.*`) + `lib/` (`Prism.Lib.*`), scoped namespaces, contracts as `Prism.Contracts` (matches AGENTFEEDBACK 07/08 note).
- Detector swap: YOLOv8n → YOLO26 (yolo26s), NMS-free — `NmsIouThreshold` removed; per-feature analyzer chain + ProductType-from-IEM added; YOLO26 model now resolved via `PrismConfigLocator` (not source-tree-only).
- Test suite: shared pipeline-run fixture cut the suite 29.4 min → 6.1 min; `SPACINI29_TINY_*` renamed `CiMini_*`, duplicate tests dropped; `SubjectEdgeDetectorRealImageTests` no longer passes vacuously.
- CI: checkout/upload-artifact bumped to @v5 (node24); Services/ model layout documented for the runner.
- API/web: job-listing route added + barebones `/jobs` page.
- Todo board: `jb/src/jbtodo.md` now empty — MMERO26/HEROAUT2/HEROAUT3 `???` entries removed at 717cc80 (last brief's "run these to close" step is now moot). Root `jbtodo.md` rewritten: old CiMini/MEPAL4 notes dropped, now holds web-workbench refinement + Import/Match-fusion asks.

##### Todo updates
- Transform/Engine/jbtodo.md (HeadCutter "spec + implement" answer): corrected the stale file-path — class is now `Services/Transform/Engine/Tx_util_HeadCutter.cs`, not `Images/Transform/` (07/08 restructure moved it). Re-verified against the code that `Analyze` signature, Haar `haarcascade_frontalface_default.xml` path, `cutY = bestFace.Y + 0.75*Height`, full-width `SubMat` crop, and in-place `ProcessedBytes`/`BoundingBox` mutation are all unchanged after the move. Kept non-final; product decisions (family-aware mode, min-clear-face threshold, webservice `Process()` signature, landmark-vs-heuristic) still open.
- Everything else unimprovable without guessing: Order/T-2830 answer (a) already implemented (`CompactDetOrder` confirmed at new `Services/Matching/ImageOrderer.cs` path); Classify + Generate items FROZEN; HeadCutter Algorithm A crown-offset still blocked on the anatomical-ratio deepdive; Analyzers TOC items each defer to per-analyzer .md (no cross-file data to resolve them here); Services test-split residual is a user overhead call; root jbtodo (web workbench + Import/Match fusion) is new design work needing your direction, not a fill-in.

##### Next steps
- `dotnet build jb/src/PRISM.sln` + `dotnet test` to confirm the restructure/YOLO26 swap compiles clean and the 6.1-min fixture suite is green before trusting anything downstream.
- Run `Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` 5x back-to-back to confirm byte-identical det0-based assignment, then close T-2820/T-2830 and pin `expected-manifest.json`.
- Decide the Services per-service `.csproj` split (mechanical; only multi-project-overhead-vs-defer is open) before the single test project accretes more stage tests. 
- Triage the two new root-jbtodo asks: web-workbench refinement (darkmode, less scrolling, import/export feedback) and Import↔Match fusion to kill double image I/O — both want your scope before an agent picks them up.
