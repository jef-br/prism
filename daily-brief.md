# Daily Brief

##### Changed
- None. No commits landed after the 15/07 brief (`b4395ba` is still HEAD on `main`); every ticket close it reported (T-2820/T-2830, T-3500, T-3600, T-3700, T-4100→T-4110, T-3300 step 0) remains the current state.

##### Next steps
- **T-4110:** fix the concrete bug first — `YoloDetector.cs:65` appends no execution provider, so YOLO is CPU-only even on a GPU box; route CLIP/YOLO/Upscale through one shared `OnnxSessionFactory`, then re-run CiMini golden 5× after the version-pin bump (CLIP runtime change can shift FP results, reopening T-2820).
- **T-3300:** net10 is aligned, so add the first real-HTTP roundtrip test for one `Http*Service` against `WebApplicationFactory` before the per-service `.csproj` split — the split only pays once a distributed run is proven.
- **T-3800:** cheapest item first — `Stopwatch` around `TryMatchBySubstringRescue` on a CiMini/full batch to settle whether the brute-force digit-index scan is a real hotspot before building an n-gram index.
- **T-4000:** start `Analyzer_FacePose` (6 features, unblocks the most on-model phenotypes) plus the config-centralization item, which is milestone-independent and can start anytime.
- **Docs-vocab sweep:** unblocked; work the ~25 confirmed `core/Images/*` path instances from last brief's inventory, skipping the deliberate "deleted, do not reintroduce" type notes.

##### Todo updates
- **Match — StringMatcher edit-distance gap** — resolved the doc-vs-code direction from existing files: the code side is exact-only (inverted token index in `StringMatcher.cs`, `ComputeStringScore` has no edit-distance term, zero `levenshtein`/`editdistance` anywhere in the Match folder), while `PRISM-match.md:76` is the lone place asserting typo tolerance — its own numeric section (`:49`, `:53`) already disclaims it, so line 76 reads as doc drift, not an unbuilt feature. If instead the code should gain tolerance, `ModelBuilder.ComputeLevenshteinDistance` (line 928) is already wired at distance ≤ 1 on tokens length ≥ 4 — the exact bounded helper the todo names. Why: every claim is grep-verified against the current files, no guessing: [jbtodo.md](jb/src/core/Services/Matching/Match/jbtodo.md).
