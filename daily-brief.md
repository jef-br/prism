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
- No repo substance change. Only delta since the prior brief: the 17/09 brief landed (`33d8d7e` → `4f703f4`), no content. Last non-brief commit is still `43f2c58`/`fef1ebe` (PR #32, architecture overview + nine drawio diagrams, 2026-09-02); every commit since is a daily-brief. All seven `jbtodo.md` untouched since 2026-08-11 (root 2026-08-12); board 2026-08-26, `AGENTFEEDBACK.md` 2026-08-12, `jb/docs/` 2026-09-02 — all unchanged.

##### Next steps
- Split T-3800: its item-1 (fuzzy categorical) validation is now satisfiable from the shipped golden — `grey-scarf`/`charcol-wrap`/`graphite-scarf` are captured green (see Todo updates), so the board's "no dataset has either" next-action is stale on the fuzzy half. Close item 1; leave only item 4 (a real Bracket-4 asset) blocked.
- The three intended Bracket-4 images (`IMG_9021`, `IMG_2619_indigo`, `IMG_7710`) all KO in the golden ("no Excel evidence" / one token below `bracket3MinDistinctTokens=2`) — none reach `SemanticMatcher`, so item 4's `totalImageTokens` fix is still unexercised. It needs a purpose-built asset: a 0-image family plus a filename that survives the waterfall to Bracket 4, not another Bracket-3-resolvable name.
- Illustration positive case: asset `90861083_e.jpg` is on disk and in `expected-phenotype.json` (as a `null` placeholder); `Analyzer_IsIllustration` is closed, so the only work left is re-capturing the golden so its row carries a real `is-illustration` judgment. No code.
- T-5010 (SPACINI29 route confirm) and T-2840 (near-tie ordering) still close on one evidence-harness run each, no code.
- Land T-5070 + T-5080, then re-score CiMini's 99 labelled rows (T-2600) — still the only path off the M11 phenotype block; the measurement itself is already done.

##### Todo updates
- StringMatcher edit-distance gap (`- [ ]`, `jb/src/core/Services/Matching/Match/jbtodo.md`, item 1): the fix (`StringMatcher.CollectFuzzyCategoricalEvidence`) is on main but its answer says real-data validation is "still missing / not yet run" — that's now stale. The shipped golden `expected-match.json` (live-captured 2026-08-25, after the ticket's 2026-08-05/06 "still needed" notes) already exercises all three arms of the pipeline (filename tokens → categorical index → fuzzy Levenshtein ≤1 → accept/reject): positive accept `grey-scarf.jpg`→96000007 (`grey` fuzzy-matches Color `gray`, 1 edit, reaches Bracket 3 because the name carries no reference number); free-text guardrail `charcol-wrap.jpg`→KO (`charcol`/`wrap` land only in a Description string, no categorical field, so no evidence); exact-match control `graphite-scarf.jpg`→96000008 (graphite vs gray is >1 edit, so the fuzzy path correctly does not over-fire). Still genuinely open: no row isolates the distance-2 or sub-4-char guard on its own (`charcol`→`charcoal` is distance-2 but also free-text, so it can't separate the two rejection reasons), and this is a captured snapshot, not a before/after A/B run. → [jb/src/core/Services/Matching/Match/jbtodo.md](jb/src/core/Services/Matching/Match/jbtodo.md)
- Illustration positive case (`- [ ]`, root `jbtodo.md` line 120, "`90861083_e.jpg` … needs `is-illustration=true`"): asset-creation half is done — the image is on disk (`test/datasets/CiMini/90861083_e.jpg`) and `expected-phenotype.json` already carries a row for it, but as `"Phenotype": null` with a "not yet judged" placeholder. Remaining work is narrow and mechanical: re-capture the phenotype golden (producer `Analyzer_IsIllustration` is closed) so the row resolves to `illustration-technical-drawing` / `is-illustration=true`. → [jbtodo.md](jbtodo.md)
- Pareo shadow-pair (`- [ ]`, root `jbtodo.md` line 17, "swapped to a different family … TBD which family"): superseded by the file's own settled decision (lines 135-143) — the hard/soft-shadow twin dropped the pareo family (94613033, "hard to shoot cleanly") and is now the FILA sneaker (hard) + ZOLA bag (soft) pair, both `[x]`. Soft half `OMB-E181-CVW_2.jpg` is on disk and green in both goldens; only gap is the hard-shadow variant file, which the todo already flags as "may be added later under this same prefix." The open `[ ]` at line 17 is superseded, not pending a family choice. → [jbtodo.md](jbtodo.md)
