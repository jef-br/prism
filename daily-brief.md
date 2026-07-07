# Daily Brief

##### Changed
- T-2800 fixed and merged (PR #4): GPU upscaler was never built on the in-process path (only the API path constructed it) — `PipelineServiceFactory` now wires it in-process; `Upscaler_g_p_u.cs` gains tiled inference for large images; `Upscaler_g_p_uTests.cs` added. `Invoke-CiPipeline.ps1` now surfaces the real pipeline failure reason instead of a generic error.
- Transform todos closed (saliency anchor, headcut thresholds, Tx_DetailCropper): `PRISM-transform-generate.md` corrected — Transform stage is fully active, no `ImageProcessorAvailable()=false` gate; `Tx_DetailCropper` ships the full 0–4 edge-intersection decision tree; headcut Algorithm B (Haar `frontalface`, cut at 75% face height) is live. Only `Tx_util_HeadCutter` Algorithm A remains open.
- Classify analyzers deleted (`Analyzer_Interior.cs`, `Analyzer_IsIllustration.cs`) and `.csproj` repointed; Classify jbtodo trimmed to two FROZEN items.
- T-2800/T-2810 archived to `AGENT-TICKET-ARCHIVE.md`.
- Two new Ready tickets block trusting a golden manifest: T-2820 (det-slot for tied images changes every run — CiMini families 94613033 and 90861083 flip-flop; needs a deterministic secondary tie-break) and T-2830 (det numbering starts at det8, not documented zero-based det0).

##### Todo updates
- Transform `Tx_util_HeadCutter` Algorithm A (`jb/src/core/Images/Transform/jbtodo.md`) — filled the empty answer bullet by deriving the Haar search band from the accepted 1:4–1:8 head:body ratio plus the already-shipped Algorithm B Haar path: restrict `DetectMultiScale` to the top ~25% of the lambda BoundingBox (widest 1:4 case), ~75% fewer pixels scanned and torso/hand false positives fall outside the region; cap the scale sweep at minSize≈H/8, maxSize≈H/4. Derivation from existing data, no new constant; still flags the crown-offset as the one open point before wiring in.
- Everything else unimproved: Services test-suites triage already recorded last pass and untouched by this week's diffs; Classify/Generate todos FROZEN; HeadCutter spec (landmark model, thresholds) needs product decisions; root job-expectation log (MMERO26/HEROAUT2/HEROAUT3 still `???` from 01/07) needs real runs. Nothing else improvable without guessing.

##### Next steps
- Sequence T-2820 → T-2830 → recapture golden: land a deterministic tie-break (filename or original-index secondary key) in `ImageOrderer`/`Order` before pinning any `expected-manifest.json`.
- T-2830 first needs the intent call: is the det8 start a real off-by-N indexing bug or an undocumented convention — resolve against `PRISM-order-rename.md` before touching code or CLAUDE.md.
- Approve or reject the Services per-service `.csproj` split (still pending from last pass — mechanical work, only the multi-project overhead-vs-defer call is open).
- Run MMERO26/HEROAUT2/HEROAUT3 to close the three `???` entries in the root job-expectation log.
