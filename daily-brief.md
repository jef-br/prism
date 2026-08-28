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
- **None.** No commits since the 27/08 brief (`8f9c4c9`, current HEAD); working tree clean, board unchanged — T-4945 still Active, T-4980 still Blocked at 48 fields, everything else as it stood. Last real deltas were in the 26/08 brief (T-4942 close, T-4945 negative false-positive result, T-4980 re-measure 93→48, ticket-process restructure `63f2257`); the 27/08 brief was itself a no-change day and nothing has landed on main since.

##### Next steps
- Unfreeze the Classify phenotype-production-validation todo and re-answer it off `jb/docs/ImageNGP/phenotype-assignment-validation.md` — its whole forward sequence ("FROZEN, next step is the light first pass = T-4970, blocked until T-4900 completes, T-4955 first") is dead: all three are archived Done and the first pass already ran.
- Run the Match accept/reject-flip measurement near `SemanticThreshold` (`CollectFuzzyCategoricalEvidence`, `CountFilenameTokens`, both on main) — the recapture was a post-fix re-bless, not a before/after contrast, so it's the only lift left; then /todo-finish each.
- Treat T-4945 as a `SubjectDetector` measurement problem, not a config tune — the 38.6%-cross negative result proves no single threshold both fires on real hard shadows and stays quiet on ordinary texture.
- Attack T-4980's 48 remaining fields via T-5120 (ordering) + T-5130 (match-assignment); re-confirm which fields survive the recapture before starting, since ~half fell out on their own.
- Close out T-2840 (near-tie ordering residual: close here or move to T-5080), then land T-5070 + T-5080 before re-scoring the phenotype rows at shipped thresholds.

##### Todo updates
- Classify phenotype-production-validation todo — **every ticket its Answer names as a future blocker is already closed, and the "light first pass" it defers to has run.** The Answer reads "FROZEN … next step is the light first pass … ticket T-4970, blocked until the T-4900 upscale epic completes. T-4955 must be fixed before that." Archive state: T-4970 Done 2026-08-03 (first *and* second pass), T-4900 epic Done, T-4955 Done — so all three clauses are false. T-4970's authoritative write-up (`jb/docs/ImageNGP/phenotype-assignment-validation.md`) already carries the pass: at shipped thresholds SPACINI29 coverage is **37.2% (32/86)**, and the rule-change replay moved correct **15→33 / wrong 38→13** at a 0.30 bar, with all 13 survivors upstream (9 CLIP orientation errors, 4 detector under-counts) — none fixable in `ImageRoles.json`. So the honest state is "unfreeze; first+second pass done and documented, rule engine sound (86/86 exact replay); still open = (1) *accuracy* of the 32 assignments, deliberately not measured, (2) the non-solid-background half (`lifestyle-hero`/`lifestyle-context`) unmeasured because MMERO26 KO'd 59/60 on `MATCHES_MULTIPLE_FAMILYIDS` before `Refine`, (3) the ~200-per-phenotype production set." All from the T-4970 archive entry + its doc, no run performed. → [Classify/jbtodo.md](jb/src/core/Services/Matching/Classify/jbtodo.md)
- The two Match items (`CollectFuzzyCategoricalEvidence`, `CountFilenameTokens`) still need the accept/reject-flip run near `SemanticThreshold`, not a lift — both fixes shipped on main (`e2e1f84`), and the golden recapture re-blessed at already-shipped post-fix code, giving no before/after contrast. Nothing improvable without that run. → [Match/jbtodo.md](jb/src/core/Services/Matching/Match/jbtodo.md)
