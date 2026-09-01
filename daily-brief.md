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
- **None.** `main`'s HEAD is the 31/08 brief (`1119d54`), which touched only `daily-brief.md`; the last substantive commit remains the T-4942 close (`06aa545`, 26/08). Working tree clean, board unchanged — T-4945 Active, T-5010 Active, T-2840 Ready, T-4980 Blocked at 48 fields behind T-5120, T-5120 Blocked on the `pack`/`packshot` collision, perf cluster (T-6930/T-6940/T-6950/T-7000) all Ready and unstarted.

##### Next steps
- T-5070 + T-5080 are the highest-leverage work on the board: T-2600's re-scoring of the 99-row CiMini set puts phenotype misassignment at 30.3%, and these two (packshot phenotypes unreachable while `intersection-count = 0`; `hero-orientation` UNKNOWN on 37% and never SIDEON) are named as the two causes of nearly all of it — both Ready, neither a threshold tune.
- T-5010 (Active) stays the cheapest close: a pure evidence-harness run on SPACINI29 checking real routes against `spacini29-image-routing-list.md`, dataset in hand, no code change to reach a verdict.
- T-2840 (Ready): decide only whether the near-tie ordering residual closes here or moves to T-5080 — root cause (CLIP batch-composition sensitivity, not tie-break logic) is already established; don't re-investigate.
- The two Match `jbtodo.md` items are code-complete and review-approved (T-3800) but structurally blocked on assets that don't exist — spin the unowned dataset work (a Bracket-3 `grey`/`gray` fuzzy case + a real Bracket-4 image) its own ticket so the `/todo-finish` close-out has an owner.
- T-4980's 48 red fields wait on T-5120, itself Blocked on the `pack`/`packshot` keyword collision — resolving that collision is the single unblock for the whole chain.

##### Todo updates
- Classify — *Phenotype production validation* (item 2): the answer's "still FROZEN … near-term next step is the light first pass (ticket **T-4970**), blocked until the T-4900 upscale epic completes; **T-4955** must be fixed before that" is now stale on every dependency — T-4970, T-4900 and T-4955 are all archived/Done, and the light first pass it was waiting on has already run: `expected-phenotype.json` (99 rows) was scored for the first time on 2026-08-05 via the in-process harness at shipped config → **30.3% misassignment, 39.4% coverage, 23.1% precision** (`front-packshot` recall 0/25), per T-2600 and the M11 gate. So the honest state isn't FROZEN-pending-prereqs but **Blocked on the two root causes T-2600 names**: [[T-5070]] (the `intersection-count = 0` gate leaves ~75% of images unable to reach any packshot phenotype, `closeup-image` absorbing the rejects) and [[T-5080]] (`hero-orientation` UNKNOWN on 37%, never once SIDEON) — neither a threshold, both must land before step 4 re-measures. The "commission a labeled set" blocker is also gone: the 99-row set exists and the confusion matrix is computable, so step 4 is unblocked, not passed. → [Classify/jbtodo.md](jb/src/core/Services/Matching/Classify/jbtodo.md)
