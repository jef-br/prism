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
- **Architecture docs landed on `main`** (PR #32, `fef1ebe` → merge `43f2c58`): new `jb/docs/architecture/` — `ARCHITECTURE.md` (578 lines) + nine `*.drawio.svg` diagrams (system-context, pipeline-stages, assembly-map, classify-chain, matching-waterfall, transform-routing, record-lifecycle, job-lifecycle, deployment-topologies) + a Python `_src/` build pipeline (`build.py`, `drawio.py`) that regenerates the SVGs; `PRISM-index.md` gained the pointer. Docs-only: no code, config, or test touched. Board unchanged since the 01/09 brief — T-4945 Active, T-5010 Active, T-2840 Ready, T-4980 Blocked at 48 fields behind T-5120, T-5120 Blocked on the `pack`/`packshot` collision, perf cluster (T-6930/T-6940/T-6950/T-7000) all Ready. Working tree clean.

##### Next steps
- T-5070 + T-5080 stay the single unblock for M11 / T-2600: both Ready, both named as the root causes of the 30.3% phenotype misassignment (`intersection-count = 0` closes off ~75% of packshot phenotypes; `hero-orientation` UNKNOWN on 37%, never SIDEON) — neither is a threshold tune.
- T-5010 (Active) is still the cheapest close: a pure evidence-harness run on SPACINI29 checking real routes against `spacini29-image-routing-list.md` — dataset in hand, no code change to reach a verdict.
- T-3800 (Blocked) is the gate on both Match `jbtodo.md` closes — spin the two assets it needs (a reference-free fuzzy-colour Bracket-3 image and a real Bracket-4 image; CiMini has neither) into their own dataset ticket so the `/todo-finish` close-out has an owner.
- T-4980's 48 red golden fields still wait on T-5120, itself Blocked on the `pack`/`packshot` keyword collision — resolving that one collision unblocks the whole chain.
- The new `jb/docs/architecture/_src/build.py` is the regeneration path for the nine diagrams — regenerate from it rather than hand-editing an SVG when a stage or assembly boundary moves.

##### Todo updates
- Match — *StringMatcher edit-distance gap*: the answer's "implemented on main (T-3800 rescue, `e2e1f84`) … Ready for /todo-finish once T-3800 validation is accepted" now understates the block. T-3800 has moved to **Blocked** on the board ("Author … a reference-free fuzzy-colour image; no dataset has either"), and the validation this todo needs is exactly the `grey-scarf.jpg` → `gray` Bracket-3 categorical case the root `jbtodo.md` still lists as an unbuilt asset. So it isn't "code-complete, pending acceptance" — it's **asset-blocked**: the fuzzy-categorical before/after can't be run until that image exists, and T-3800 is where that authoring is tracked. → [Match/jbtodo.md](jb/src/core/Services/Matching/Match/jbtodo.md)
- Match — *SemanticMatcher.totalImageTokens*: same correction. The answer's remaining open item — "the before/after on a labeled set (accept/reject flips near `SemanticThreshold`) is still the open validation before /todo-finish" — is blocked, not merely pending: the root `jbtodo.md` constraint states no CiMini image reaches Bracket 4 today, and T-3800 (now Blocked) owns authoring the Bracket-4 image needed to exercise `stringSignal` near the threshold. Fix shipped in `e2e1f84`; verdict waits on that missing asset, not on a review sign-off. → [Match/jbtodo.md](jb/src/core/Services/Matching/Match/jbtodo.md)
