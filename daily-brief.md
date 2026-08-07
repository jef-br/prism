# Daily Brief

> ## **Important note from the user (Jef)**
> This note must persist daily-brief.md until Jef removes it manually.
> 
> "Jef, do nothing before you have fixed T-5010 + T-2840"
> - **T-5010**: the current behavior of `Tx_DetailCropper.cs` is wrong and needs to be fixed.
>   - Apply the margin if one edge is intersected
>   - use bounding box for resizing
>   - use bgstretch utils class for stretching
> - **T-2840**: Claude reworked how CLIP is used per image.
>           Now **300% slower** due to serialization or something (not quiet sure, 3 hypotheses in ticket)
>           Claude suggested re-exporting the ONNX. We should investigate check Huggingface to see if there are other options.
>           and export something new if it's useful. Need Claude for that.

-----



##### Changed
- **T-5060 was reverted, not fixed — last brief reported it landed and that was wrong.** The axis-ordered overflow compaction the previous brief credited with making CiMini `90861052` match its golden was reverted (`10384d1`) and the board reconciled to match (`97d4af8`). The CiMini end-to-end golden is red again on 84 fields, and the fix is reassigned to [[T-5120]], not reopened under T-5060 — [[T-4980]]'s next-action now points at T-5120.
- **CiGolden + JBComplete merged into `test/datasets/CiMini/` (`c0403e0`).** The 99-row `expected-phenotype.json`, `expected-match.json`, and the six per-bracket cases the last brief attributed to "JBComplete" now live under CiMini — its old 14-image content was replaced wholesale (100 sources: 97 loose images + a 3-member zip). Every "JBComplete" name in the previous brief and in the todos now points at the CiMini dataset.
- **CLIP/YOLO confidence thresholds lowered across the board (`51182c3`) — and it post-dates the only M11 measurement.** Classification 0.9→0.5, YOLO 0.40→0.33, HumanMinConfidence 0.50→0.30, AbsenceConfidence 0.60→0.40, out of the CARDIGAN "two ordinary shots classify to nothing" investigation that fell out of the revert. The 30.3% misassignment / 39.4% coverage / `front-packshot` 0/25 headline was scored 2026-08-05 at the *old* config, so it now describes thresholds PRISM no longer ships. `AnalyzerConfigTests` was rewritten to assert the values it loads from `analyzer_Config.json` instead of mirroring them as literals, so a retune stops red-failing the suite for a reason unrelated to the behaviour under test.
- **New ticket [[T-2840]] (Ready, P4) — CLIP output differs by submission transport.** Widening `PipelineFixture` to submit the full CiMini set surfaced 5 families whose classification differs between the in-process `dotnet test` transport (every image loose) and the ps1 HTTP capture (one seed image multipart, everything else repacked into a synthetic zip). All are near-ties barely over the 0.33 bar — e.g. `98636303`'s `OMB-E180-BV_1` vs `_3` at 0.39 vs 0.40 — so the *candidate set itself* differs per transport, not just the tie-break. `ImageSourceKind` (ZipMember vs LocalPath) is read nowhere in Matching, so the mechanism is still open.
- **New ticket [[T-5120]] (Blocked, P4) — filename/folder/sequence tokens should feed phenotyping, matching and ordering.** The [[T-5060]] successor: the four token bags (numeric/string/clip/analyzer) are siloed today — filename tokens reach matching and ordering-as-tiebreak, but phenotyping reads only clip+analyzer, so a `*_DETAIL.jpg` in a `details/` folder gets no phenotype at all when CLIP fails. A partial commit (`8491fc7`) already tokenizes the full path for keyword matching; the ticket is gated to a clean/green/roomy session because it rewrites four stages in one pass, and it inherits the CiMini-golden fix from the revert.
- **PRISM config-mapper React tech demo added (`90b60c6`)** — standalone workbench-adjacent demo, no pipeline behaviour change.

##### Next steps
- Re-score CiMini's 99 phenotype rows at the lowered thresholds before citing 30.3% anywhere — the headline now predates the shipped config, and T-2600's next action (land T-5070+T-5080, then re-score) can't be judged against a stale baseline.
- Give the Review backlog its verdicts: T-4955, T-4990, T-5000 are clean closes; T-4980 is now "golden red, fix owned by T-5120," not a close; T-4942 still needs the `-m:1` + 500-test floor confirmed on a real CI run.
- Run T-2840's isolated-batch experiment — submit one affected family alone through both transports and diff the per-prompt CLIP score vectors — to confirm or rule out batch-composition sensitivity before touching the classifier.
- Land T-5070 (`intersection-count = 0` meaning; blocks 7 of 18 phenotypes) + T-5080 (`hero-orientation` never emits SIDEON); together they're what stands between the phenotype miss rate and the M11 gate.
- Fix the two matcher defects the scoring exposed: T-5090 (evaluate every rescue token, KO on contradiction instead of returning on the first) and T-5100 (refuse a match when a discriminating token resolves to no family).
- Author the still-missing fixtures: a Bracket-4 picture-only case + a reference-free fuzzy-colour image (T-3800), the `illustration-technical-drawing` positive, and a hard-vs-soft shadow twin (T-4945).

##### Todo updates
- **Classify item 2 + root phenotype-validation** ([Classify/jbtodo.md](jb/src/core/Services/Matching/Classify/jbtodo.md), [root jbtodo.md](jbtodo.md)): both answers argue from the 2026-08-05 measurement (30.3% miss / 39.4% coverage / `front-packshot` 0/25). Commit `51182c3` (2026-08-07) then lowered every threshold that measurement ran under — Classification 0.9→0.5, YOLO 0.40→0.33, Human 0.50→0.30, Absence 0.60→0.40. So the conclusion ("failed the M11 bar, blocked on T-5070+T-5080") now rests on a baseline the pipeline no longer ships. The direction is knowable — lower thresholds accept more phenotype assignments, so coverage lifts off 39.4% — but misassignment can move either way, so this needs the re-run, not a guess. Improved from the threshold commit + T-2600, not a guess.
- **Match item 1 (fuzzy categorical) + item 2 (totalImageTokens)** ([Match/jbtodo.md](jb/src/core/Services/Matching/Match/jbtodo.md)): unchanged since the last brief — both still can't be exercised on real data (`C153KB460011` resolves at Bracket 1 on numeric `460011`, so Bracket 3 and `CollectFuzzyCategoricalEvidence` never run; `Bracket2-Intersect` and Bracket 4 both still have 0 accepts on CiMini). New but not an improvement: T-2840 puts the three `C153KB460011` files into its transport-divergence set, but that's a CLIP-determinism finding, not a fuzzy-categorical exercise. The fixtures both items need (reference-free fuzzy colour, Bracket-4 picture-only) still don't exist.
- The rest stay genuinely blocked with no new data: Export Todo 4's 7 `Tx_*` still don't self-write their param values; HeadCutter Algorithm A still needs the measured crown-offset constant; Generate is FROZEN behind the ComfyUI backend. The Analyzer calibration items (HasHuman, MultipleProducts, DominantColors, …) now sit on freshly-lowered thresholds set from a single CARDIGAN case rather than a calibration run — which makes "needs a measurement pass" more true, not less — but those boxes carry no Answer text yet to improve.
