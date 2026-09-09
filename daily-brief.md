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
- None. No git movement since the 08/09 brief — HEAD = `origin/main` = `8334119` (that brief, `daily-brief.md`-only); tree clean; board still at `06aa545` (T-4942 close, 2026-08-26). No ticket, todo, doc, or feedback file has moved.

##### Next steps
- Deprioritise authoring the hard-shadow twin: T-4945's labelled pass shows `HardShadowStrippedFraction` can't separate hard from ordinary shadows at any threshold, so no new asset unblocks hard-shadow validation until the `SubjectDetector` measurement change lands — that detector change, not the image, is now the critical path for the whole shadow-pair dataset.
- T-4945 board says the fix "needs a `SubjectDetector` measurement change, not a config tune," but it's still framed as a threshold-calibration ticket — consider splitting the detector-algorithm change into its own ticket so the config/threshold half can't be mistaken for the remaining work.
- T-5010 (SPACINI29 route check) and T-2840 (near-tie ordering) are still the only two code-free closes on the board — cheapest to clear.

##### Todo updates
- Root dataset — *pareo shadow-pair swap* (`- [ ]`, line 17) and the hard/soft twin block (lines 137–138): last brief flagged that the hard-shadow file is missing on disk; the deeper point, from T-4945's 2026-08-25 labelled harness run (existing data), is that authoring or swapping *any* asset won't help yet — the metric can't discriminate. All 5 confirmed-hard CiMini images measure `HardShadowStrippedFraction` 0.0096–0.0407, i.e. **all below the current 0.042 threshold**, so `HasHardShadowEvidence` is false for every one. Worse, two *ordinary* shots in the very FILA family meant to host the hard twin fall inside that same range (`_bottom-packshot_sole` 0.0144, `_top-packshot_overhead` 0.0407) — no threshold on this metric separates the classes. So the real blocker on line 17 isn't "pick a cleaner-shooting family" and it isn't the missing `shadowhard.jpg`; it's the pending `SubjectDetector` measurement change in T-4945. The pareo swap should stay parked behind that, not chased as an asset task. → [jbtodo.md](jbtodo.md)
