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
- **T-4942 closed (Done 2026-08-26, `06aa545`, archived).** The CI "Assert minimum test count" floor gate ran and passed for the first time ever — 567/500. Root cause of why it never once fired: the step carried no `if: always()`, so Actions' default (skip a step unless every prior step succeeded) skipped it whenever "Unit tests (xUnit)" went red on the known-red CiMini goldens — the step was measuring "did too few tests run" but could only be reached on a run where that question was already moot. Fix is a 4-line `ci.yml` change (`0beb28c`) decoupling crash/short-circuit detection from overall golden greenness (two different questions), floor value untouched. Verified against real run `32958313230`.
- **T-4945 false-positive check ran (2026-08-26) — negative result, ticket Ready→Active.** `HardShadowEvidenceFraction` walked 0.05→0.0082→0.001→0.042 (`6263fca` set 0.0082; head config now `0.042` at `ClassifyConfig.json:69`, inline comment still stale at 0.0082). A/B harness over all CiMini+SPACINI29 at 0.042: **59/153 ordinary images (38.6%) cross it**; ordinary median 0.0381 sits almost exactly on the threshold, and the ordinary range 0.0000–0.1343 fully contains the 5 labelled-hard images' range 0.0096–0.0407. So the two populations aren't separable by this metric at *any* threshold — raising it past the ordinary p90 (0.0657) also excludes every real hard shadow. It's the `SubjectDetector` texture-vs-chroma strip fraction that doesn't discriminate, not the number. Side effect logged: at 0.0082, `top-packshot_overhead.jpg` gets trimmed 591→556px, crossing the 570px floor and KO'ing `PREPROCESS_TOO_SMALL` (deterministic across 5 runs); gone at 0.042.
- **T-4980 re-measured post-recapture (2026-08-26), still Blocked.** `CiMini_Manifest_MatchesCommittedGolden` now red on 48 fields (was 93 — 24 rows × 2, all DetOrder/FinalFileName drift, no FamilyId/Status mismatches); `FamilyAssignmentsHold` red on 7 (was 14, all `MATCHES_MULTIPLE_FAMILYIDS`). The 25/08 golden recapture (`e7db04b`) cleared roughly half the drift on its own; the remainder is unchanged in kind — still needs T-5120 (ordering rows) + T-5130 (match-assignment rows). The 93-count in the 25/08 brief's next-steps is confirmed stale.
- **Ticket process restructured (`63f2257`).** `ticket-new` SKILL now mandates four bullet lists — Problem(s)/Decision(s)/Test(s)/Reviewer(s), no narrative prose outside them, step count = sum of all bullets (so a hardcoded "two gaps" intro can't go stale as bullets are added). ~20 existing tickets reformatted into that shape in the same commit.
- **Board:** T-4942 row removed (closed); T-4945 Ready→Active; T-4980 next-action refreshed to the 48-field count.

##### Next steps
- Treat T-4945 as a `SubjectDetector` measurement problem, not a config tune — the negative result proves no threshold both fires on real hard shadows and stays quiet on ordinary texture. Either change what's measured (the texture-vs-chroma strip fraction) or spin that out as its own ticket; separately, fix the stale inline comment (still says 0.0082 while the value is 0.042).
- Run the Match jbtodo accept/reject-flip measurement near `SemanticThreshold` (`CollectFuzzyCategoricalEvidence`, `CountFilenameTokens`, both already on main) — the golden recapture was a post-fix re-bless, not the before/after contrast, so it's still the only lift left; then /todo-finish each.
- Attack T-4980's 48 remaining fields via T-5120 (ordering) + T-5130 (match-assignment); re-confirm which fields survive the recapture before starting, since ~half fell out on their own.
- Update T-3800's board next-action — "no dataset has either" is stale; the Bracket-4 and reference-free fuzzy-colour cases already live in CiMini.
- Close out T-2840 (near-tie ordering residual: close here or move to T-5080), then land T-5070 + T-5080 before re-scoring the phenotype rows at shipped thresholds.

##### Todo updates
- None liftable this pass. No `jbtodo.md` moved since 2026-08-07; all 2026-08-26 work was ticket-level (T-4942/T-4945/T-4980). The two Match items (`CollectFuzzyCategoricalEvidence`, `CountFilenameTokens`) still need the accept/reject-flip run, not a lift — the recapture re-blessed the golden at already-shipped post-fix code, giving no before/after contrast. → [Match/jbtodo.md](jb/src/core/Services/Matching/Match/jbtodo.md)
- Classify phenotype-production-validation todo unchanged (still FROZEN): the 25/08 recapture gave CiMini its first labelled `expected-phenotype.json`, but it's ground-truth only — no pipeline-output-vs-golden misassignment rate computed and it's CiMini-sized, not the ~200-per-phenotype set — so the "<5% across all phenotypes" bar stays unmeasured. Nothing improvable without a real run or the larger set. → [Classify/jbtodo.md](jb/src/core/Services/Matching/Classify/jbtodo.md)
