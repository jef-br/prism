# Daily Brief

##### Changed
- None. `HEAD` = `origin/main` = `c4b5935` (the 21/07 brief itself); no code commits since `1b68b26`, so this is the third consecutive no-code-delta brief. Working tree clean. Open ticket set unchanged: T-2600 (M5 Classify, Blocked), T-3800 (Match todos, Ready), T-4000 (Analyzer TOC, Ready), T-4400 (Roslyn analyzers, Active — phase 1 Approved).

##### Next steps
- **/todo-finish the substring-rescue perf todo now, standalone** — measured (250 unmatched ≈ 336 ms, 2,500 ≈ 1.1 s at 3,000-family scale), "not worth an n-gram index," no CiMini/Bracket-4 dependency; the only Match item that closes today with no code change.
- **T-4400 phase 2 (S109) is the next gated rule** — 98 magic-number literals in core, each either moved to config per the shadow-defaults rule or explicitly justified as structural; shadow-defaults enforcement at compiler grade.
- **Build the CiMini expansion (root `jbtodo.md`)** — it gates *both* remaining Match /todo-finishes: `totalImageTokens` needs the Bracket-4 "z" edge case, and the fuzzy-categorical fix needs the "grey-scarf vs 'gray'" typo case. Neither exists in the current 14-image golden (0 of 14 reach Bracket 4).
- **T-4000:** start `Analyzer_FacePose` (6 features, unblocks the most on-model phenotypes) plus the milestone-independent `AnalyzerConfig.cs` centralization — the config item can start anytime.
- Confirm the SA1101 `this.`-prefix direction with the user before T-4400 phase 4 — 472 mechanical prefixes contradict the "short, practical" style line; unchanged blocker.

##### Todo updates
- Match `totalImageTokens` item (`SemanticMatcher.TryMatch`, item 4): its answer says the fix is on main (commit `e2e1f84`, `totalImageTokens = stringMatcher.CountFilenameTokens(filename)`, pool size no longer leaks into `stringSignal`) with unit tests passing, but "the before/after on a labeled set (accept/reject flips near `SemanticThreshold`) is still the open validation before /todo-finish." That validation is structurally impossible on today's golden: this code path *is* Bracket 4 (`SemanticMatcher`), and the root `jbtodo.md` records that 0 of 14 CiMini images ever reach Bracket 4 — so no committed real-data case exercises `stringSignal` at all. Same CiMini-expansion blocker as the fuzzy-categorical item (last brief), specifically the root todo's not-yet-covered Bracket-4 "z" edge case; unlike the substring-perf item it is **not** standalone-closeable. Cross-referenced item 4's answer against the root todo's Bracket-4 coverage note, not a guess: [jbtodo.md](jb/src/core/Services/Matching/Match/jbtodo.md).
