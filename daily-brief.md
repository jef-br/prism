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
- None. HEAD = `736f9fb` (14/09 brief) = `origin/main`; tree clean. Last non-brief commit is still `43f2c58`/`fef1ebe` (PR #32, PRISM architecture overview + nine drawio diagrams, 2026-09-02) — every commit since is a daily-brief. All seven `jbtodo.md` untouched since 2026-08-11 (root `jbtodo.md` 2026-08-12); board, `jb/docs/`, and `AGENTFEEDBACK.md` unchanged. Baseline note: the 11–14/09 briefs' `Changed` line still cites HEAD as `d1e81ba` — that's now three brief commits stale (12–13 Sep skipped, then 14/09 landed as `736f9fb`); repo substance is identical either way.

##### Next steps
- T-5010 (SPACINI29 route confirm) and T-2840 (near-tie ordering) still close on one evidence-harness run each, no code — cheapest two to clear.
- Split the Analyzer_ProductType **term-collision audit** out of T-4000 as its own ticket now: T-4000 records it as unblocked since the 2026-08-05 review (VINGINO79's 2,847 images + JBComplete supply the real batch vocabulary); only the vocabulary-unification half stays blocked.
- Run the `HeroPersonMinArea = 0.15` sweep over SPACINI29's 86 images (the method T-4990 used for the edge detector) — the one remaining Analyzer_HasHuman question, defined and runnable, no new data needed.
- Author the two missing T-3800 assets (a Bracket-4 image + a reference-free fuzzy-colour image) — one asset-authoring task code-completes both open Match todos at once.
- Land T-5070 + T-5080, then re-score CiMini's 99 labelled rows (T-2600) — still the only path off the M11 phenotype block; the measurement itself is already done.

##### Todo updates
- Analyzer_ProductType (`- [ ]`, `Analyzers/jbtodo.md` line 9, "vocabulary unification + term-collision audit open"): T-4000 splits these two halves — the term-collision audit is **unblocked** (real batch vocabulary now exists: VINGINO79 2,847 images + JBComplete, per the 2026-08-05 split review) and answerable now; only the ProductTypeMap↔TranslationDictionary unification stays blocked, on the Classify→TranslationConfig project-reference-direction decision. So the line should split: audit → ready to ticket; unification → blocked, same blocker as line 52. → [Analyzers/jbtodo.md](jb/src/core/Services/Matching/Analyzers/jbtodo.md)
- Analyzer_HasHuman (`- [ ]`, `Analyzers/jbtodo.md` line 11, "partial-body recall to validate"): recall ground truth already exists — SPACINI29's notes name the 12 legs-only (bottom-half-human) images, and T-4970 found the single miss, `23211095_35_A.jpg` (a draped poncho where YOLO returned zero people). The open item narrows from "validate recall" to one measurement: whether `HeroPersonMinArea = 0.15` is the right threshold given that evidence — the SPACINI29 sweep in T-4000's Test(s), not yet run. → [Analyzers/jbtodo.md](jb/src/core/Services/Matching/Analyzers/jbtodo.md)
- Analyzer_Interior (`- [ ]`, `Analyzers/jbtodo.md` line 18, "product-type-gating question to reconcile"): T-4000 reclassifies this as a code/doc contradiction, not a calibration sweep, and records that JBComplete already closed the interior-shot case five ways — `OMB-E129-TGV_4`, `OMB-E166-BV_4`, `OMB-E180-BV_5`, `OMB-E181-CVW_5`/`_6`. So the remaining work is reconciling the gating doc against the code using those five settled cases, not gathering data. → [Analyzers/jbtodo.md](jb/src/core/Services/Matching/Analyzers/jbtodo.md)
- Retire `ImageOrderer.ResolveProductType` fallback (`- [ ]`, `Analyzers/jbtodo.md` line 51): this is a pure cleanup gated only on the Analyzer_ProductType audit above (the refined `ProductTypeId` path already wins when set, per the line itself) — no product decision left, so it sequences directly behind that split, not behind the blocked unification half. → [Analyzers/jbtodo.md](jb/src/core/Services/Matching/Analyzers/jbtodo.md)
