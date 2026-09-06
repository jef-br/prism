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
- **None.** No code or board movement since the 04/09 brief. HEAD is `ded7275` (that brief commit, which only touched `daily-brief.md`); `origin/main` matches it and the working tree is clean. Every commit back to the 02/09 brief is a daily-brief edit — the last non-brief change was the architecture-docs merge (`43f2c58`). The board file itself hasn't been touched since `06aa545` (T-4942 close, 2026-08-26): still T-4945 Active, T-5010 Active, T-2840 Ready, T-4980 Blocked (48 fields, behind T-5120), T-5120 Blocked on the `pack`/`packshot` collision, T-3800 Blocked on the two unauthored bracket assets, perf cluster (T-6930/T-6940/T-6950/T-7000) all Ready and unstarted.

##### Next steps
- T-5070 + T-5080 remain the single unblock for M11 / T-2600: both Ready, both root-cause the 30.3% phenotype misassignment (`intersection-count = 0` closes ~75% of packshot phenotypes; `hero-orientation` UNKNOWN on 37%, never SIDEON) — neither is a threshold tune.
- T-5010 (Active) is still the cheapest close: a pure evidence-harness run on SPACINI29 checking real routes against `spacini29-image-routing-list.md` — dataset in hand, no code change to reach a verdict.
- T-2840 (Ready) is already root-caused to CLIP batch-composition sensitivity, not tie-break logic; decide only whether the near-tie ordering residual closes here or moves to T-5080, then close — don't re-investigate.
- Spin the two unauthored assets (a Bracket-3 `grey`/`gray` file, a real Bracket-4 image) into their own dataset ticket — they alone gate T-3800 and both Match `jbtodo.md` closes, and no ticket owns them.
- T-4980's 48 red golden fields still wait on T-5120, itself Blocked on the `pack`/`packshot` keyword collision — resolving that one collision unblocks the whole chain.

##### Todo updates
- Match — *StringMatcher edit-distance* and *SemanticMatcher totalImageTokens* (both still `- [ ]`, answers ending "pending T-3800 validation"): both are already code-complete, unit-tested, doc-updated (`PRISM-match.md`) and **review-Approved** on main (`e2e1f84`/`f40beed`, T-3800 Review: Approve 2026-07-25). Per T-3800's own body the review gate is satisfied and the only thing left is empirical before/after validation, which is `Blocked-by` "CiMini expansion — no ticket owns it": it needs a Bracket-3 fuzzy case (filename `grey`, Excel `gray`) and an image that actually reaches Bracket 4, neither of which exists in any dataset. So these two answers can be sharpened from "pending validation" to "code landed and approved; blocked solely on two unauthored assets that no ticket owns" — the same asset gap the Next-steps line flags. → [Match/jbtodo.md](jb/src/core/Services/Matching/Match/jbtodo.md)
- Classify — *Phenotype production validation*: the answer still reads "still FROZEN … near-term next step is the light first pass … ticket T-4970, blocked until the T-4900 upscale epic completes. T-4955 must be fixed before that." All three named blockers are now closed (T-4900 Done 2026-07-30, T-4955 and T-4970 archived), so the pass has actually run — twice. First+second pass on SPACINI29 (T-4970, write-up `jb/docs/ImageNGP/phenotype-assignment-validation.md`): rule fixes lifted correct 15→33, wrong 38→13, coverage 37.2% (32/86), all 13 survivors upstream (9 CLIP orientation errors, 4 detector under-counts), none fixable in `ImageRoles.json`. A second pass on the labeled CiMini set (99 rows, M11 gate) then measured 30.3% misassignment, 39.4% coverage, `front-packshot` recall 0/25. So this todo is no longer frozen-pending-upscale: reliability is proven (byte-identical reruns), and the one open item is *accuracy*, now tracked by T-2600 behind T-5070 + T-5080. → [Classify/jbtodo.md](jb/src/core/Services/Matching/Classify/jbtodo.md)
- Root dataset — *pareo shadow-pair* (`- [ ] pareo shadow-pair swapped to a different family … TBD which family, see note below`): the "TBD which family" is already answered further down the same file. The hard/soft-shadow twin pair (T-4945) was **not** swapped to another single family — the pareo (family 94613033) was dropped entirely ("hard to shoot cleanly") and replaced by two different pre-existing products: `2426834-7558_side-packshot_shadowhard.jpg` (FILA sneaker, family 98768768) for the hard-edged cast shadow and the existing `OMB-E181-CVW_2.jpg` (ZOLA bucket bag, family 98636312) for the soft diffuse one. No new family or Excel row is needed, so the open checkbox is stale relative to the note it points at. → [jbtodo.md](jbtodo.md)
