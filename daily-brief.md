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
- T-6910 closed — RefinePhenotypes, the pipeline's one single-threaded full-resolution pass, is now parallel; measured **4.87x** speedup and the measurement trap recorded. Three residual findings split into new tickets: T-6930 (Pass-1 chunk barrier caps effective parallelism at ~3 of 8 threads), T-6940 (match-only runs still pay for feature-analysis matching never reads), T-6950 (stop full-res decode for scale-invariant analyzers).
- T-2840 promoted to Ready — CLIP batch-composition sensitivity **confirmed** via the isolated-batch experiment (one family, both transports, per-prompt CLIP score vectors diffed). hero-orientation threshold raised 0.33→0.42 to clear the measured jitter band. `ImageSourceKind` (ZipMember vs LocalPath) is still read nowhere in Matching, so the mechanism stays the open residual.
- T-5010 now Active — stale gate-widening/phenotype text corrected; the `Tx_ProblemImageProcessor` "reports pre-upscale output metadata" defect split out as new ticket T-5140.
- CiMini dataset expanded (`4cced83`) then again 08-14 (`7f9e8af` "shadow additions") — the root-jbtodo bracket/ordering/phenotype/transform fixtures now exist on disk: Bracket-4 picture-only trio (`IMG_9021`, `IMG_2619_indigo`, `IMG_7710`), fuzzy-categorical scarf trio (`grey-scarf`/`graphite-scarf`/`charcol-wrap`), the sibling-propagation set, plus a new `difficult shadows/` folder of 12 images.
- New tickets since the last brief: T-7000 (successful jobs never delete their `%TEMP%/PRISM/{jobId}` artifact folder), T-5140, T-5210 (SiblingPropagator may reinvent token evidence Brackets 1-3 already built), and T-6930/T-6940/T-6950 from the T-6910 close.
- Excel filesize cap raised to accept ~15 MB workbooks (JLINE5 is 12.7 MB, `85d6472`); matching test infra simplified (`9136350`); a raw-CLIP-output harness (`TempClipRawOutputHarness`) + reworked targeted match-only run scripts landed for the T-2840 transport probe (`e0437da`) — investigation aids, no pipeline behaviour change.

##### Next steps
- Recapture both CiMini goldens (`-Mode Match` and `-Mode Full`, per its README) now that `4cced83`/shadow additions changed the image set — every phenotype/match number is untrustworthy until the goldens re-bless.
- Run the now-authorable Match validations: `grey-scarf.jpg` exercises `CollectFuzzyCategoricalEvidence` at Bracket 3 and `IMG_9021`/`IMG_2619_indigo`/`IMG_7710` are the Bracket-4 picture-only cases — do the before/after accept-reject diff T-3800 was blocked on.
- Update T-3800's board next-action — "no dataset has either" is stale; the Bracket-4 and reference-free fuzzy-colour images now exist in CiMini.
- Re-score CiMini's 99 phenotype rows at the shipped thresholds (post-`51182c3`, plus the new 0.42 hero-orientation) before citing 30.3% / 39.4% anywhere; still gated on landing T-5070 + T-5080.
- Label the new `difficult shadows/` set hard vs soft to feed T-4945's threshold re-tune.
- Clear the Review verdicts: T-4942 still needs the `-m:1` + 500-test floor confirmed on a real CI run; T-4980 stays "golden red, fix owned by T-5120," not a close.

##### Todo updates
- **Match item 1 (fuzzy categorical) + item 2 (totalImageTokens)** ([Match/jbtodo.md](jb/src/core/Services/Matching/Match/jbtodo.md)): the fixtures both answers called missing now exist in CiMini as of `4cced83` — `grey-scarf.jpg` (filename `grey`, edit-distance 1 from family 96000007's `Color=gray`, no reference number, so it reaches Bracket 3 where `CollectFuzzyCategoricalEvidence` runs) and the Bracket-4 picture-only trio `IMG_9021`/`IMG_2619_indigo`/`IMG_7710`. The T-3800 blocker "no dataset has either" is resolved at the authoring level; what stays open is the before/after accept-reject run (and a CiMini golden recapture first), not writing the fixtures. Improved from the dataset commit, not a guess.
- **Classify item 2 + root phenotype-validation** ([Classify/jbtodo.md](jb/src/core/Services/Matching/Classify/jbtodo.md), [root jbtodo.md](jbtodo.md)): still await the re-score, and the baseline moved again — T-2840 raised hero-orientation 0.33→0.42 on top of `51182c3`'s lowered thresholds, so the 2026-08-05 30.3% / 39.4% / `front-packshot` 0/25 headline now predates two threshold changes, not one. Direction unchanged: needs the run, not a guess.
- The rest stay genuinely blocked with no new data: Export Todo 4's 7 `Tx_*` still don't self-write their param values; HeadCutter Algorithm A still needs the measured crown-offset constant; Generate stays FROZEN behind the ComfyUI backend. The new `difficult shadows/` set is fresh data for T-4945's threshold tune but carries no todo Answer text to improve yet.
