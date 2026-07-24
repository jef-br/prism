# Daily Brief

##### Changed
- **Ticket board re-synced against the source `jbtodo.md` files (eac5674)** — the only commit since the last brief; `AGENT-TICKETS.md` only, no code touched. Three open tickets had drifted from the files they claim to track:
  - **T-2600** listed 6 open Classify items; the source file has 2, both FROZEN. The other 4 were already closed via the todo lifecycle (decisions in `jb/docs/PRISM-classify.md`, `imagePhenotypes.md`, `PRODUCTTYPES.MD`) but the board was never refreshed. Blocked-by line rewritten to say "both remaining items FROZEN pending prerequisite work," not "all decisions unanswered."
  - **T-3800** listed 3 Match items; the source file has always had a 4th — the fuzzy-fallback four-layer future-work note — that was never added to the board. Now on as item 3, in file order. None of the 4 have been `/todo-finish`'d yet.
  - **T-4000** claimed 27 Analyzer items; the source has 26. The phantom "centralize `AnalyzerConfig.cs`" item isn't in the source file and its concern was functionally superseded by T-4400's S109 fold-in — single-consumer `*AnalyzerConfig.cs` classes were folded as nested `Config` types into their owning `Analyzer_*.cs` files (the *decentralized* direction, opposite to one shared `AnalyzerConfig.cs`, but it resolves the same scattered-standalone-config complaint). `ColorAnalyzerConfig`/`YoloAnalyzerConfig` stay standalone (genuinely multi-consumer).
- No code, config, or doc files changed since the last brief; repo still on a single branch (`main`).

##### Todo updates
- **Match fuzzy-fallback future-work note** ([jb/src/core/Services/Matching/Match/jbtodo.md](jb/src/core/Services/Matching/Match/jbtodo.md)) — its `Answer:` field is still empty, but the note's own Recommended-solution already settles it ("do not build speculatively; record-only"). Suggested Answer, drawn only from the note itself: close as a documentation-move into `jb/docs/PRISM-match.md`, no code — the 4-layer (dictionary/stemming/fuzzy/semantics) build stays deferred with each layer's prerequisite cost already enumerated in the note. Improvable without guessing because the decision is the note's own stated conclusion, not a new call.
- **Match items 1/2/4** (edit-distance gap, substring-rescue perf, `totalImageTokens`) — each already carries its 2026-07-17 "implemented/measured on main" resolution (commit e2e1f84); the only thing still open is validation-run acceptance. Can't advance without a run → no improvement without guessing.
- Everything else (root CiMini coverage gap, T-4000's 26 analyzers, Transform HeadCutter A + spec, Classify 2 FROZEN, Generate FROZEN) unchanged from the last five briefs — none improvable from existing data alone.

##### Next steps
- Run the two no-code Match `/todo-finish`es now standalone: substring-rescue perf (measured, "not worth an n-gram index") and the fuzzy-fallback future-work note (documentation-move) — neither waits on Bracket-4 golden coverage, and the board now agrees with the source so no fresh reconciliation is needed first.
- Build the CiMini expansion (root `jbtodo.md`) — still the sole blocker on Match items 1 and 4 (grey-scarf typo case, Bracket-4 "z" near-threshold edge case); 0 of 14 golden images reach Bracket 4 today.
- T-4000: start `Analyzer_FacePose` (6 features, unblocks the most on-model phenotypes, milestone-independent — can start anytime).
- Board's only "Ready" work stays T-3800 (Match) and T-4000 (Analyzer TOC); T-2600 stays Blocked behind T-4000's Analyzer stubs + a BypassPhenotypes decision.
