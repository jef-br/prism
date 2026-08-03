# Daily Brief

##### Changed
- None. No commits since the 31/07 brief — the repo is identical to `34f2588`. T-4970 / T-4980 / T-5010 sit exactly where the last brief left them (Review), and the three lowered CLIP bars (0.33/0.25/0.1) are still the running config.

##### Next steps
- Close T-4970 with /ticket-finish (Approve is in), then re-run its coverage measurement against the shipped bars — the old numbers were taken at 0.40+ and don't transfer.
- Rework T-4980 where it bites: move the full-frame box to what `PreprocessAsync` reads, or gate `DetectSalientBoundingBox` on `intersection-count == 4` — the current diff writes a box nothing consumes and the green golden is coincidental.
- Fix T-5010's 6 red Transform fixtures before re-review — the routing restoration is correct but no verdict passes over a red suite.
- Land T-4955 + T-4990 + T-5000 before any further threshold tuning — the bars moved but the three upstream faults (stale derived features, under-counted intersections, filename false positives) did not, so post-tune phenotype numbers stay provisional.
- Still open, unchanged: T-4942 (serialize the two GPU test projects `-m:1`, assert a minimum test count); T-4960 (prefer the T-4830 alpha box over colour-distance in `Analyzer_SubjectGeometry`); T-4000 (split the 11 analyzer calibration questions into their own tickets).

##### Todo updates
- None — nothing improvable without guessing. No data has landed since the 31/07 brief, which already mined the last batch of commits into the Classify, Analyzers, and Export answers; the remaining todos (root CiMini Bracket-3/4 fixtures, Export Todo 4's 7 `Tx_*.cs` param values, Transform HeadCutter crown-offset constants, Generate behind ComfyUI) stay genuinely blocked on missing measurements or backend, not on anything derivable from current files.
