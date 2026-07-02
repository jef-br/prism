# Daily Brief

##### Changed
- Fetcher dispatch now wired: `FetchDispatcher` + `Fetch_DropBox` added; `PrismProcessIngressReader.AddRemoteInputRecordsAsync` routes URLs via `dispatcher.CanHandle`/`FetchAsync` off `configuration.FetchDispatcher`. This is T-2700's scope — implemented in code, but the ticket still reads Status: Ready.
- New public match-only API surface: `PrismMatchLiteIngressReader` + `MatchOnlyResult` model; `Program.cs` exposes a multipart match-lite route (`prismService.MatchLite`) and `MatchOnlyAsync`. Advances the root `jbtodo.md` "keep matchingservice open to the public" line — no ticket tracks it yet.
- `ImageMatcher.cs` reworked (~232 lines churn); `PrismService`, `MatchingService`, `ClassificationService`/`IClassificationService` touched alongside.
- Visual dedup retuned: `VisualHasher.cs` hash-size + Hamming-distance parameters changed (commit fde6d8a).
- Test harness: new `test/MatchingTestClient` console client + `Run_MatchingTestClient.ps1`/`Run_TinyTest.ps1`; existing `test-scripts/` moved `jb/` → `test/`.
- Config-path reality: `ImageNGP.json` / `ImageRoles.json` actually live in `jb/src/core/config/`, not `jb/src/core/ImageNGP/` as the docs and `AGENTFEEDBACK.md` table state (that folder now holds only the schema + validator + vocabulary).

##### Todo updates
- Classify taxonomy todo — recorded an existing-data verification (2026-07-01, pending approval): the 26 phenotype NAMES reconcile cleanly across `ImageNGP.json` ↔ `imagePhenotypes.md` ↔ `ImageRoles.json` (set-diff empty in every direction; every id referenced by ≥1 role rule, 0 orphans), and corrected the stale config path in the answer. Why safe: this is exactly the name-level cross-check the todo's own close-out asks for, done with existing files only, no invention. Left open: per-rule IF-*combination* equivalence still unverified.
- Everything else unimproved — Transform saliency/headcut/greedy/HeadCutter need user product decisions (T-2300/T-2200); Classify `RecordUnknownFeatures` + phenotype validation blocked on taxonomy/labeled set; Generate backend needs a running server. Nothing improvable without guessing.

##### Next steps
- Reconcile T-2700: fetcher dispatch is implemented — mark Done, or note the remaining acceptance gaps (content-type-based routing, explicit KO reason for unsupported URLs) if any are still open.
- Decide whether the new match-only route needs a ticket; if it satisfies the "matchingservice public" `jbtodo.md` item, record that decision so the todo can close.
- Finish the taxonomy close-out: run the per-rule IF-combination equivalence check (`ImageRoles.json` rules ↔ `imagePhenotypes.md` definitions) — the only gap left after name-level reconciliation.
- Fix the stale config-path pointers (docs + `AGENTFEEDBACK.md` say `core/ImageNGP/`; real path is `core/config/`) — needs your OK since those files are approval-gated.
- Approve the Classify verification above and the standing `illustration-technical-drawing` option (b) recommendation to clear M5 gate item 2.
