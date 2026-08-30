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
- **None.** Three-day gap since the 27/08 brief (`8f9c4c9`), which is still `main`'s HEAD; that commit touched only `daily-brief.md`, so nothing substantive has landed since the 26/08 deltas. Working tree clean, board identical to what the last brief recorded — T-4945 still Active, T-5010 still Active, T-4980 still Blocked at 48 fields, T-5120 still Blocked on the `pack`/`packshot` collision. The board now carries a performance cluster (T-6930 chunk-barrier parallelism, T-6940 match-only fast path, T-6950 full-res decode, T-7000 job-folder cleanup) — all still Ready, none started.

##### Next steps
- T-4945 (Active): the 38.6%-cross result already proves the fraction metric can't discriminate at any threshold, so this is a `SubjectDetector` measurement change, not a config tune — treat it as such rather than re-sweeping `HardShadowEvidenceFraction`.
- T-5010 (Active): run the evidence harness on SPACINI29 and check real routes against `spacini29-image-routing-list.md` — this is a measurement task with the dataset already in hand, so it's the cheapest Active ticket to close.
- T-4980's 48 red fields wait on T-5120, which is itself Blocked on the `pack`/`packshot` keyword collision — resolve that collision first or the whole chain stays stuck.
- Land T-5070 + T-5080 next: they gate both M11 phenotype validation and T-2600, and T-5070 alone blocks 7 of 18 phenotypes (`intersection-count = 0` has no defined meaning).
- The perf backlog is pure-measurement to start: T-6930 wants sustained core-use before/after a flat chunking scheme, T-6950 wants the wall-clock win quantified before deciding on a deliberate CiMini golden re-bless — measure before touching code on either.

##### Todo updates
- Classify phenotype-production-validation todo — its answer's entire named blocker chain is now **closed in the archive**: **T-4970 (the "near-term next step"), T-4900 (the upscale epic it waited on), and T-4955 ("must be fixed before that") are all Done.** T-4970 *was* that first+second validation pass — at shipped thresholds it measured coverage 37.2% (32/86 SPACINI29 images); rule changes alone moved correct 15→33 and wrong 38→13; all 13 remaining errors are upstream (9 CLIP orientation, 4 detector under-counts), none fixable in `ImageRoles.json`; and two fresh-process runs produced byte-identical JSON, so the measurement apparatus is verified reliable, not assumed. An authoritative write-up already exists at `jb/docs/ImageNGP/phenotype-assignment-validation.md`. So the honest state is not "FROZEN pending T-4970" but: reliability + rule-coverage validated on SPACINI29; genuinely still open = (a) whether those 32 assignments are *correct* — accuracy was deliberately not scored; (b) the non-solid-background half (`lifestyle-hero`/`lifestyle-context`), unmeasured because MMERO26 KO'd 59/60 on `MATCHES_MULTIPLE_FAMILYIDS` and a KO'd image never reaches `Refine`; (c) the ~200-per-phenotype production set. All from the T-4970 archive entry + that doc, no run performed. → [Classify/jbtodo.md](jb/src/core/Services/Matching/Classify/jbtodo.md)
