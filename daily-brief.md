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
- Nothing of substance. `origin/main` HEAD is now `3e75344` (19/10 brief, 2026-10-19); the previous brief body named `2710846` (16/10) as HEAD, and the single intervening commit is that 19/10 brief's own `daily-brief.md`-only overwrite (5 insertions, 5 deletions) — no code, doc, ticket, or todo change. No 17/10, 18/10, or 20/10 brief commit exists (gap days); HEAD == `origin/main`, working tree clean, nothing unpushed. Last real code/doc commit remains PR #32 (`fef1ebe`, architecture overview + nine drawio diagrams, 2026-09-02). State files unchanged: board + `AGENT-TICKETS-ARCHIVE.md` `06aa545` (2026-08-26); `jb/docs/` `fef1ebe` (2026-09-02); `AGENTFEEDBACK.md` + all seven `jbtodo.md` `789df29` (2026-08-25) — note this is the same unchanged 2026-08-25 content the prior brief recorded, but git reports its last-touch hash as `789df29`, not the `e7db04b` that brief named (hash drift only, no content delta). Re-verified every anchor the sections below rest on against the live files: `SemanticMatcher.cs:76` (`CountFilenameTokens`) + `:114-115` (`stringSignal`), `StringMatcher.cs:317`/`:328` (`CollectFuzzyCategoricalEvidence`) + `:541` (`CountFilenameTokens`); the three intended Bracket-4 candidates `IMG_2619_indigo`/`IMG_7710`/`IMG_9021` (`expected-match.json:462/467/472`) all `MATCH_NOT_FOUND`; `90861083_e.jpg` on disk and `expected-phenotype.json:487` still `"Phenotype": null`; root `jbtodo.md` line 17 (pareo) and line 120 (illustration positive) — all accurate, none actioned by Jef yet.

##### Next steps
- T-5010 (SPACINI29 route confirm) and T-2840 (near-tie ordering residual) each still close on one evidence-harness run, no code — both remain Jef's stated goals.
- Land T-5070 + T-5080, then re-score CiMini's 99 labelled rows (T-2600) — still the only path off the M11 phenotype block; the measurement itself is already done.
- T-3800: close item 1 (fuzzy categorical) — the shipped golden already exercises it (`grey-scarf`/`charcol-wrap`/`graphite-scarf` on disk and in `expected-match.json`), so the board's "no dataset has either" is stale on the fuzzy half; leave only item 4 (Bracket-4 asset) blocked.
- T-3800 item 4 still needs a purpose-built Bracket-4 asset: the three intended images all KO *before* `SemanticMatcher` (golden: `IMG_2619_indigo` below `bracket3MinDistinctTokens=2`, `IMG_7710`+`IMG_9021` no Excel evidence), so neither the `totalImageTokens` fix nor Bracket 4 itself is exercised by any CiMini row.
- Illustration positive (`90861083_e.jpg`): on disk and in `expected-phenotype.json` as a `null` placeholder; `Analyzer_IsIllustration` is closed, so the only work left is re-capturing the golden so its row carries a real `is-illustration` judgment. No code.

##### Todo updates
- SemanticMatcher.totalImageTokens (`- [ ]`, item 2): the todo's answer still calls the before/after on a labeled set "the open validation before /todo-finish." The fix is shipped and re-confirmed in code (`SemanticMatcher.cs:76` `totalImageTokens = stringMatcher.CountFilenameTokens(filename)`; `StringMatcher.cs:541`; pool size no longer leaks into `stringSignal` at `:114-115`), but `expected-match.json` shows every CiMini image KOs before `SemanticMatcher` ever scores — the three intended Bracket-4 candidates `IMG_2619_indigo`/`IMG_7710`/`IMG_9021` (`:462/467/472`) all `MATCH_NOT_FOUND`. So the `stringSignal` path has zero CiMini coverage and the A/B can't run on CiMini as-is — blocked on the same purpose-built Bracket-4 asset T-3800 item 4 owes, not merely "a labeled run." Standing carry-over, re-verified unchanged this pass. → [jb/src/core/Services/Matching/Match/jbtodo.md](jb/src/core/Services/Matching/Match/jbtodo.md)
- StringMatcher edit-distance gap (`- [ ]`, item 1): the fix (`CollectFuzzyCategoricalEvidence`, `StringMatcher.cs:328`, called at `:317`) is shipped, but the item's answer still reads non-final with real-data validation "not yet run." The shipped golden exercises all three arms: positive accept `grey-scarf.jpg`→96000007 (`grey` vs Color `gray`, 1 edit); free-text guardrail `charcol-wrap.jpg`→KO (`charcol` only in a Description string, no categorical field); exact-match control `graphite-scarf.jpg`→96000008 (graphite vs gray >1 edit, fuzzy silent). Still open: no row isolates the distance-2 or sub-4-char guard alone, and it's a captured snapshot, not a before/after A/B. Standing carry-over, re-verified unchanged this pass. → [jb/src/core/Services/Matching/Match/jbtodo.md](jb/src/core/Services/Matching/Match/jbtodo.md)
- Illustration positive case (`- [ ]`, root `jbtodo.md` line 120): asset half done — image on disk (`test/datasets/CiMini/90861083_e.jpg`) and `expected-phenotype.json:487` carries a row, but as `"Phenotype": null` with a "not yet judged" placeholder. Remaining work is mechanical: re-capture the phenotype golden (producer `Analyzer_IsIllustration` is closed) so the row resolves to `illustration-technical-drawing` / `is-illustration=true`. Standing carry-over, re-verified unchanged this pass. → [jbtodo.md](jbtodo.md)
- Pareo shadow-pair (`- [ ]`, root `jbtodo.md` line 17): superseded by the file's own settled decision (lines 136-144) — the hard/soft-shadow twin dropped the pareo family (94613033, "hard to shoot cleanly") and is now FILA sneaker (hard, `2426834-7558_side-packshot_shadowhard.jpg`) + ZOLA bag (soft, `OMB-E181-CVW_2.jpg`), both `[x]`. Soft half is on disk and green in both goldens; only gap is the hard-shadow variant file, which the note already flags as "may be added later under this same prefix." The open `[ ]` at line 17 is superseded, not pending a family choice. Standing carry-over, re-verified unchanged this pass. → [jbtodo.md](jbtodo.md)
