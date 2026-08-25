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
- Real code work resumed. Six commits landed on `main` (`a326d12`→`6a4cbc6`) since the 24/08 brief `b4ea14c` (which itself reported "None material"); `origin/main` and local `main` are now both at `6a4cbc6`.
- **CiMini goldens recaptured (`e7db04b`)** — the standing "goldens untrustworthy until re-blessed" item is cleared. `expected-manifest.json`, `expected-match.json` (115 rows) and `expected-phenotype.json` (127 rows) all re-blessed; dataset trimmed: the whole `difficult shadows/` folder (13 imgs) and the `4471-2340-*` set deleted, several images re-exported at new sizes. `6a4cbc6` adds `test/ci/Format-CiMiniGolden.ps1` and reformats `expected-manifest.json` to house style.
- **`ModelBuilder.cs` whitespace/K&R fix landed (`a326d12`)** — the dotnet-format violation that blocked T-4942's CI floor gate is gone. The real CI run to confirm the ~500-test floor is still the untouched half of that ticket.
- **T-4945: `HardShadowEvidenceFraction` lowered 0.05 → 0.0082** (`6263fca`, `ClassifyConfig.json:69`, `//JB: … set based on experience`) against a first labelled hard-shadow pass — 6 CiMini images, 5 measured 0.0096–0.0407, all below the old 0.05 (unambiguous: 0.05 was too high). Ticket now carries the caveat inline: 0.0082 is also below SPACINI29's own measured min (0.0113), so on that set it may reproduce the old 0.01 "fires on all 86, discriminates nothing" mode; no false-positive check against ordinary images was run first.
- New workbench phenotype-mapping page started (`789df29`, `a5d4325`): `prism-mapper.jsx` deleted from `jb/techdemo/`, moved + renamed into `workbench` (`draft for ImageNGP mapper page.jsx`).
- Board barely moved: only T-4945's "Do this next" line changed. Six `jb/src/**/jbtodo.md` unchanged since 2026-08-07; every other ticket row still as of 12/08. T-2840 Ready, T-5010 Active still open.

##### Next steps
- Confirm T-4942's CI floor on a real run now that `ModelBuilder.cs` is clean (`-m:1` + the ~500-test floor); that real green run is the only thing left to clear its Review verdict.
- Run the false-positive check T-4945 flagged before trusting 0.0082: measure `HardShadowStrippedFraction` across ordinary (non-hard-shadow) CiMini/SPACINI29 images to see how many now cross it. The centering A/B half of T-4945 is still untouched.
- Re-check T-4980's "golden red on 93 fields" against the freshly recaptured goldens — that 93-field count predates `e7db04b`, so it's likely stale; re-measure before treating it as the T-5120 blocker.
- Run the labelled before/after both Match jbtodo fixes still need (`CollectFuzzyCategoricalEvidence`, `CountFilenameTokens`, both already on main via `e2e1f84`). The golden recapture is a post-fix re-bless, not the accept/reject-flip measurement near `SemanticThreshold`, so it does not substitute — then /todo-finish each.
- Update T-3800's board next-action ("no dataset has either" is stale — the Bracket-4 and reference-free fuzzy-colour cases already live in CiMini).
- Close out T-2840 (near-tie ordering residual: close here or move to T-5080), then land T-5070 + T-5080 before re-scoring the phenotype rows at shipped thresholds.

##### Todo updates
- **Classify jbtodo — _Phenotype production validation_ (FROZEN).** Its stated gap ("phenotype assignment produced zero results until 2026-07-28 … nobody has ever seen what it actually assigns", and no labelled ground truth exists) is now partly closed for CiMini: the 25/08 golden recapture (`e7db04b`) blessed a hand-labelled `expected-phenotype.json` — 127 rows across 17 populated taxonomy slots (front-packshot 24, back-packshot 11, diagonal-packshot 8, … lifestyle-context 1), 34 deliberately labelled `null` "no slot in the taxonomy" (exploded/tilted products, marketing infographics, feature collages) and 16 flagged `Confidence: low` judgement calls. This is the first labelled phenotype reference the todo asked for. Still short of acceptance: it's CiMini-sized, not the ~200-images-per-phenotype set, and it's ground-truth only — no pipeline-output-vs-golden misassignment rate has been computed, so the "<5% across all phenotypes" bar stays unmeasured and T-4970 (the light first pass) stays blocked. → [Classify/jbtodo.md](jb/src/core/Services/Matching/Classify/jbtodo.md)
- The two Match jbtodo items ([`CollectFuzzyCategoricalEvidence`, `CountFilenameTokens`](jb/src/core/Services/Matching/Match/jbtodo.md)) remain **not** liftable: the recapture re-blessed the match golden at the already-shipped (post-fix) code, which gives no before/after contrast — the accept/reject-flip measurement is still the only remaining step, a run not a lift.
