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
- The 29/10 brief (`c46c653`) reached `origin/main`; after this session's fetch, local HEAD = `origin/main` = `c46c653` with no lag — the recreate-from-default fetch delay the last few briefs flagged did not recur this pass.
- Two briefs are missing: no 30/10 and no 31/10 commit exist, so the chain runs 27/10 → 28/10 → 29/10 → (gap) → 01/11. The scheduled routine did not fire on either day; today's (Sunday) did. No back-fill.
- No code/doc/ticket/todo change in the window: `git diff d0a05b2 c46c653 -- ':(exclude)daily-brief.md'` is empty, and nothing has landed since. Last real code/doc commit remains PR #32 (`fef1ebe`, architecture overview + nine drawio diagrams, 2026-09-02). Working tree clean — no regression to revert this pass. State-file hash-drift continues and stays content-free — ignore it.

##### Next steps
- T-5010 (SPACINI29 route confirm) and T-2840 (near-tie ordering residual) each still close on one evidence-harness run, no code — both remain Jef's stated goals, neither actioned since 22/10.
- Author the purpose-built Bracket-4 asset T-3800 item 4 owes — it is the sole remaining blocker to /todo-finish on both re-verified Match fixes (`SemanticMatcher.totalImageTokens`, `StringMatcher` fuzzy-categorical).
- Land T-5070 + T-5080, then re-score CiMini's 99 labelled rows (T-2600) — still the only path off the M11 phenotype block; the measurement itself is already done.
- Two scheduled briefs missed (30/10, 31/10): if daily cadence matters, check the claude.ai routine's trigger — the gap is the routine not firing, not a repo problem.

##### Todo updates
- Export Todo 1 — "only exported images" definition ([Export/jbtodo.md](jb/src/core/lib/Export/jbtodo.md)): resolvable from code, and the proposed `OutputRecord.ExportStatus == "Ok"` predicate is unsafe as a count of real exports. `Exporter.BuildOutputRecords` (`Exporter.cs:68`) sets `ExportStatus = "Ok"` **unconditionally** for every non-KO lambda that resolves to a normalized-input entry — no branch ever writes any other value — so it means "non-KO with a resolved input", not "a file was written". The ZIP's own OK-membership test is stricter (`Exporter.cs:93-100`): a non-KO lambda lands in `OK/` only when `ProcessedBytes` exists or `ArtifactPath` is a readable file, otherwise it is skipped with `continue` while still carrying `ExportStatus == "Ok"`. So keying header `#OK`/`#KO` on `ExportStatus` overcounts exports and hides byteless-but-non-KO rows; the accurate "exported" predicate is non-KO AND (`ProcessedBytes != null` OR `File.Exists(ArtifactPath)`) — the exact test the archive loop already uses.
- Nothing else improvable without guessing: the remaining Match, Classify, Analyzers, Generate, and Transform todos each now gate on a measurement run (evidence harness / CiMini re-score), an open product call, or the unbuilt ComfyUI backend — no existing data advances them this pass.
