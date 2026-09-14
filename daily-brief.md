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
- None. HEAD = `d1e81ba` (11/09 brief) = `origin/main`; tree clean. No non-brief commit since `43f2c58` (merge of PR #32, the PRISM architecture overview + nine drawio diagrams, `fef1ebe`, 2026-09-02); all seven `jbtodo.md` files untouched since 2026-08-12; board, `jb/docs/`, and `AGENTFEEDBACK.md` unchanged. (12–13 Sep briefs skipped over the weekend.) Baseline correction: prior briefs cited `06aa545`/2026-08-26 as the last non-brief commit and referenced a commit `e2e1f84` that is not in the repo — neither is accurate.

##### Next steps
- T-5010 (SPACINI29 route confirm) and T-2840 (near-tie ordering) remain the only two closes needing no code — one evidence-harness confirmation run each; cheapest to clear.
- Author the two missing T-3800 assets (a Bracket-4 image + a reference-free fuzzy-colour image) — one asset-authoring task unblocks T-3800 and code-completes both open Match todos at once, not analysis.
- Land T-5070 + T-5080, then re-score CiMini's 99 labelled rows (T-2600) — the only remaining path off the M11 phenotype-validation block; the measurement itself is already done (see below).

##### Todo updates
- Phenotype production validation (`- [ ]`, `Classify/jbtodo.md` line 9; Answer reads "still FROZEN … next step is T-4970, blocked until T-4900 upscale epic completes"): that answer is now stale — T-4970's light first pass has already run. `jb/docs/ImageNGP/phenotype-assignment-validation.md` and the M11 row in `AGENT-TICKETS.md` record the 2026-08-05 measurement on `expected-phenotype.json` (99 rows): 30.3% misassignment, 39.4% coverage, `front-packshot` recall 0/25, plus a documented second pass that rejected lowered thresholds (coverage 7%→62% but 0/5 correct). So the acceptance criteria the todo asks to "define" already exist (<5% misassignment, no systematic per-category error — the M11 gate), and the real distribution is measured; the remaining block is no longer "run T-4970 behind T-4900" but landing T-5070 + T-5080 then re-scoring per T-2600. → [Classify/jbtodo.md](jb/src/core/Services/Matching/Classify/jbtodo.md)
- Pareo shadow-pair (`- [ ]`, root `jbtodo.md` line 17, "swapped to a different family — TBD which family"): answerable from the same file, no guess. The T-4945 hard/soft-shadow twin note (lines 135–143) records pareo family 94613033 was *dropped* ("hard to shoot cleanly"), not swapped, and the twin requirement was reassigned to two already-existing families — `2426834-7558_side-packshot_shadowhard.jpg` (98768768, FILA sneaker, hard-edged cast shadow) and `OMB-E181-CVW_2.jpg` (98636312, ZOLA bag, soft diffuse shadow), both already `[x]`. "TBD which family" is stale: no swap-to-family decision is left — close the line as superseded by those two entries. → [jbtodo.md](jbtodo.md)
- StringMatcher edit-distance + SemanticMatcher.totalImageTokens (`- [ ]`, `Match/jbtodo.md` lines 5 and 66): both are decision-complete and gated only on T-3800 validation. The StringMatcher answer already resolves the doc-vs-code conflict (reuse `ModelBuilder.ComputeLevenshteinDistance`, same `Prism.Core` assembly so no new project reference; `PRISM-match.md` updated; "ready for /todo-finish once T-3800 validation accepted"). New from existing data: the exact gating assets are already specified and spec-written `[x]` in root `jbtodo.md` — `grey-scarf.jpg` (US/UK "grey"/"gray", edit-distance 1 → 96000007) exercises the Levenshtein categorical path, and `IMG_9021`/`IMG_2619_indigo`/`IMG_7710` (the only Bracket-4 cases, where SemanticMatcher runs) exercise `totalImageTokens`. So the only work left on both is producing those image files + recapturing the two CiMini goldens — no spec or product decision remains. (Prior brief's claim that these are "live on main (commit e2e1f84)" is unfounded — no such commit exists.) → [Match/jbtodo.md](jb/src/core/Services/Matching/Match/jbtodo.md)
