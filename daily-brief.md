# Daily Brief

> ## **Important note from the user (Jef)**
> This note must persist daily-brief.md until Jef removes it manually.
> As stated yesterday, **T-5010** and **T-2840** were the goal for today.
> Here's where things stand:
> **Closed (6):**
> * T-4955, T-4990, T-5000, T-5100, T-5110, T-6900 ← all Approved and verified.
> Sent back with specific gaps, moved Review → Ready (3):
> * **T-4942** (CI floor gate has never passed a real run — blocked by an unrelated formatting violation in ModelBuilder.cs)
> * **T-5090** (fix correct, but the ticket's own required re-audit of 4 other matches was skipped)
> * **T-5200** (indexing is safe, but a bundled behavior change ships untested).
> Moved Review → Blocked (1):
> * **T-4980** — golden is red on 93 fields, real fix depends on T-5120 which hasn't started.
> New tickets (2):
> * **T-5130** (Excel column-fill-rate gate)
> * **-6920** (per-family colour-code check, needs /pair).
> Still running at the time of writing:
> * **T-6910**: reviewer agent, just started (21:15 GMT+1, expected duration 50m)
> Housekeeping:
> * reverted  uncommitted brace-style regression in BoundingBox.cs;
> * flagged an un-popped git stash (stash@{0}) from the T-5100 review to clean up later. All committed to main.
> **TAKEAWAY FOR THE DAILY-BRIEF.MD**
> T-2840 and T-5010 are not done yet. The daily brief for 12/08/2026 should reflect that.

##### Changed
- **None.** No commits since the 26/08 brief (`91e77cf`); working tree clean, board unchanged — T-4945 still Active, T-4980 still Blocked at 48 fields, everything else as it stood yesterday. The 26/08 brief already captured that day's real deltas (T-4942 close, T-4945 negative false-positive result, T-4980 re-measure 93→48, ticket-process restructure `63f2257`); nothing has landed on main since.

##### Next steps
- Treat T-4945 as a `SubjectDetector` measurement problem, not a config tune — the 38.6%-cross negative result proves no threshold both fires on real hard shadows and stays quiet on ordinary texture; separately fix the stale inline comment at `ClassifyConfig.json:69` (still says 0.0082 while the value is 0.042).
- Run the Match accept/reject-flip measurement near `SemanticThreshold` (`CollectFuzzyCategoricalEvidence`, `CountFilenameTokens`, both on main) — the recapture was a post-fix re-bless, not a before/after contrast, so it's the only lift left; then /todo-finish each.
- Attack T-4980's 48 remaining fields via T-5120 (ordering) + T-5130 (match-assignment); re-confirm which fields survive the recapture before starting, since ~half fell out on their own.
- Update T-3800's board next-action — "no dataset has either" is stale; the Bracket-4 and reference-free fuzzy-colour cases already live in CiMini.
- Close out T-2840 (near-tie ordering residual: close here or move to T-5080), then land T-5070 + T-5080 before re-scoring the phenotype rows at shipped thresholds.

##### Todo updates
- Classify phenotype-production-validation todo — the "<5% misassignment" bar is **not merely unmeasured; a first real pass exists and failed hard.** The board's M11 gate records a measurement on the same 99-row labelled set (`test/datasets/CiMini/expected-phenotype.json`, taken 2026-08-05 as JBComplete before the 2026-08-06 → CiMini merge): **30.3% misassignment, 39.4% coverage, `front-packshot` recall 0/25.** That directly satisfies the todo's own "run the pipeline, compare to ground truth, measure accuracy" step and both fails the aggregate <5% bar and pins a systematic single-category collapse (front-packshot never assigned) — which is exactly the acceptance clause "no systematic error on any single category," and exactly what T-5070 + T-5080 target. So the honest todo state is "first pass measured on the 99-row set, 30.3% + front-packshot 0/25, blocked on T-5070/T-5080," not "unmeasured/FROZEN pending T-4970"; the genuinely-open piece is only the larger ~200-per-phenotype production set. All numbers from the board's M11 gate, no run performed. → [Classify/jbtodo.md](jb/src/core/Services/Matching/Classify/jbtodo.md)
- The two Match items (`CollectFuzzyCategoricalEvidence`, `CountFilenameTokens`) still need the accept/reject-flip run near `SemanticThreshold`, not a lift — both fixes shipped on main (`e2e1f84`), and the golden recapture re-blessed at already-shipped post-fix code, giving no before/after contrast. Nothing improvable without that run. → [Match/jbtodo.md](jb/src/core/Services/Matching/Match/jbtodo.md)
