# Daily Brief

##### Changed
- **T-3300 distributed-services seam landed on `main` and closed** (`1ebd00e` merge, `c916121` close → archive). Ingest is de-serviced to in-process-only ingress; `Prism.ServiceHost` now constructs each of the 4 public services inside its own `Hosts()` branch; `PRISM_UPSCALE_URL` lets Transform delegate upscaling to a remote Upscale host; real-HTTP roundtrip tests cover the 4 service clients; a distributed CI golden (4 service hosts + API) runs the same golden as in-process, nightly; tests are split into per-public-service projects. Review fixes (`53502f9`): cancellation reaches the remote upscale call, HTTP failure paths now have tests, and all five service-host ports are pre-checked so a stale port fails loud, not slow.
- **T-3500 in-process Import→Match handoff via `NormalizedJpegBytes`** landed (`523074e`) — removes the redundant image re-decode between Import and Match.
- **T-3800 Match work** landed (`e2e1f84`, review-fixed in `f40beed`): fuzzy categorical matching; `totalImageTokens` now counts real filename tokens so candidate-pool size no longer leaks into `stringSignal`; substring-rescue perf measured via `SubstringRescuePerfMeasurement.cs`; StringMatcher fuzzy thresholds moved into `MatchingConfig.json` (no shadow defaults).
- **Docs path/vocab sweep done** (`488381f` + `de13c42`) — stale `core/Images/*` references fixed across `jb/docs/`, clearing last brief's open "Docs-vocab sweep" next-step.
- **New root `jbtodo.md`** (`53d48d6`): records that CiMini has 0/14 images reaching Bracket 4 plus several other waterfall branches (Bracket-2 intersect, fuzzy fallback, substring rescue, both sibling-propagation tiers, convergence bonus, FolderNameEnricher, 2 of 3 KO codes) with zero real-data coverage — each documented with a photo+Excel-row example/counter-example so the expanded dataset can be built without re-deriving bracket logic.
- Note: the 16/07 brief reported "None" while this seam work was still on its branch; it has now merged, so this is the real delta across two brief cycles.

##### Next steps
- Close the three Match todos that only block on validation: run the labeled/CiMini before-after for `totalImageTokens` and the fuzzy thresholds near `SemanticThreshold`, then `/todo-finish` substring-rescue (already measured: 250 unmatched ≈ 336 ms, 2,500 ≈ 1.1 s at 3k-family scale — no n-gram index warranted).
- Build the expanded CiMini dataset (new root `jbtodo.md`) — until Bracket 4 and the other zero-coverage branches actually fire, the T-3800 fuzzy/rescue/sibling code stays unexercised on real data.
- **T-4110** still the top architecture item: route CLIP/YOLO/Upscale through one shared `OnnxSessionFactory` (fixes `YoloDetector.cs:65` appending no execution provider → CPU-only on a GPU box), then re-run the CiMini golden 5× to confirm no FP drift.
- **T-4000:** start `Analyzer_FacePose` (unblocks the most on-model phenotypes) plus the milestone-independent config-centralization item.
- **T-4400:** phase 1 (SA1402 one-type-per-file = 9, SA1649 = 1) is cheap and CI-gates a house rule immediately; but confirm the SA1101 `this.`-prefix direction with the user before phase 4 — it adds 472 prefixes and contradicts the "short, practical" style line.

##### Todo updates
- None improvable by me this pass without guessing. The three Match todos (substring-rescue perf, `totalImageTokens` precision, fuzzy categorical matching) already moved on-repo on 17/07 via T-3800 and now block only on an empirical accept/reject validation run — code alone can't settle them: [jbtodo.md](jb/src/core/Services/Matching/Match/jbtodo.md).
- Root CiMini-coverage todo and the fuzzy-fallback scope entry need user-supplied data (a real dataset) or a product decision, not analysis — deliberately left for the user: [jbtodo.md](jbtodo.md).
