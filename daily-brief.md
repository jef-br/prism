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
---
##### Changed
- None. No commits since the last brief — `Daily brief - 07/08/26` is still the tip of `main`, working tree clean. The two gating tickets (T-5010, T-2840) are unstarted.

##### Next steps
- Per Jef's standing note, clear T-5010 and T-2840 before anything else — both are still open, so the rest of the board waits on them.
- T-5010 is blocked on a user decision, not code: give every SPACINI29 row in `spacini29-image-routing-list.md` a blessed intended route, then the `Tx_DetailCropper` rewrite (margin-on-single-intersect, bbox resize, BgStretch stretch) can proceed against it.
- T-2840 is code-ready: run the isolated-batch experiment — one affected family through both transports, diff the per-prompt CLIP score vectors — to settle batch-composition sensitivity before touching the ONNX export or the classifier.
- Re-score CiMini's 99 phenotype rows at the lowered thresholds (`51182c3`) before citing 30.3% misassignment / 39.4% coverage anywhere — the headline predates the shipped config, so T-2600's re-score gate can't be judged against it yet.
- Clear the Review backlog: T-4955, T-4990, T-5000 are clean closes; T-4980 is "golden red, fix owned by T-5120," not a close; T-4942 still needs the `-m:1` + 500-test floor confirmed on a real CI run.
- Land T-5070 (`intersection-count = 0` meaning; blocks 7 of 18 phenotypes) + T-5080 (`hero-orientation` never emits SIDEON) — together they stand between the phenotype miss rate and the M11 gate.

##### Todo updates
- None — nothing improvable without guessing. No repo change since the last brief, and that pass already exhausted what the existing data supports: Classify/root phenotype answers still await the post-`51182c3` re-score, both Match items (fuzzy categorical, totalImageTokens) still lack the reference-free fuzzy-colour and Bracket-4 fixtures needed to exercise them, Export Todo 4's 7 `Tx_*` still don't self-write their params, HeadCutter Algorithm A still needs the measured crown-offset constant, and Generate stays FROZEN behind the ComfyUI backend.
