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
- **None.** No code or board movement since the 06/09 brief. HEAD and `origin/main` are both `8845deb` (that brief commit, which only touched `daily-brief.md`); working tree clean. Every commit since the 02/09 brief is a daily-brief edit — the last non-brief change was the architecture-docs merge (`43f2c58`, 02/09). The board file hasn't been touched since `06aa545` (T-4942 close, 2026-08-26): still T-4945 Active, T-5010 Active, T-2840 Ready, T-4980 Blocked (48 fields, behind T-5120), T-5120 Blocked on the `pack`/`packshot` collision, T-3800 Blocked on the unauthored bracket assets, perf cluster (T-6930/T-6940/T-6950/T-7000) all Ready and unstarted.

##### Next steps
- One dataset ticket now owns **three** unauthored assets, not two: the Bracket-3 `grey`/`gray` fuzzy file and a real Bracket-4 image (gate T-3800 + both Match `jbtodo.md` closes) plus `90861083_e.jpg`, the sole positive case for the `illustration-technical-drawing` phenotype — none is owned by any ticket.
- Reconcile a stale reload memory: `AGENTFEEDBACK.md` still records "`illustration-technical-drawing` → null/no-phenotype, do not add a phenotype for it", but that phenotype is live in `ImageRoles.json:101-107` (`hero-is-human=FALSE` + `is-illustration=true`) and documented in `PRISM-classify.md:252-254` — the memory was superseded and now misleads.
- T-5070 + T-5080 remain the single unblock for M11 / T-2600: both root-cause the 30.3% phenotype misassignment (`intersection-count = 0` closes ~75% of packshot phenotypes; `hero-orientation` UNKNOWN on 37%, never SIDEON) — neither is a threshold tune.
- T-5010 (Active) is still the cheapest close: a pure evidence-harness run on SPACINI29 checking real routes against `spacini29-image-routing-list.md` — dataset in hand, no code change to reach a verdict.
- T-2840 (Ready) is already root-caused to CLIP batch-composition sensitivity, not tie-break logic; decide only whether the near-tie ordering residual closes here or moves to T-5080, then close — don't re-investigate.
- T-4980's 48 red golden fields still wait on T-5120, itself Blocked on the `pack`/`packshot` keyword collision — resolving that one collision unblocks the whole chain.

##### Todo updates
- Root dataset — *`90861083_e.jpg` illustration image* (`- [ ]`, line 120): the whole chain that would consume it already exists — `Analyzer_IsIllustration` is implemented and closed (Analyzers `jbtodo.md` line 19), the `is-illustration` feature is live in `ImageNGP.json:29`, the `illustration-technical-drawing` phenotype rule is live in `ImageRoles.json:101-107` (fires on `hero-is-human=FALSE` + `is-illustration=true`) and documented in `PRISM-classify.md:252-254`, and host family 90861083 already carries real front/back photos (`23211008_02_A/B.jpg`). So the answer sharpens from "needs `is-illustration=true`" to: this single unauthored image is the *only* missing piece for the `illustration-technical-drawing` phenotype to have any positive test case anywhere; no Excel row is needed. Caveat worth flagging on the todo: `AGENTFEEDBACK.md` still asserts this phenotype was dropped to null/no-phenotype — that memory contradicts the live rule and should be resolved before anyone acts on the "no positive case" framing. → [jbtodo.md](jbtodo.md)
- Root dataset — *pareo shadow-pair* (`- [ ]`, line 17): this checkbox is now fully complete downstream, not just "stale relative to the note". The replacement twin pair is already checked off in the same file (lines 137-138, both `- [x]`): `2426834-7558_side-packshot_shadowhard.jpg` (FILA sneaker, existing family 98768768) for the hard-edged cast shadow and `OMB-E181-CVW_2.jpg` (ZOLA bucket bag, existing family 98636312) for the soft diffuse one — two different pre-existing families, no new family or Excel row. The pareo (94613033) was dropped ("hard to shoot cleanly"). So line 17 is not TBD or pending — the work it names is done and checked below it, and the box can simply flip to `- [x]`. → [jbtodo.md](jbtodo.md)
