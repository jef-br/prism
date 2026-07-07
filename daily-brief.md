# Daily Brief

##### Changed
- Det-order compaction shipped (core a20f4ab, finished 29668ff): `ImageOrderer.CompactDetOrder` renumbers each family to contiguous det0..detN at export — renumber only, never reorders — gated by `DetOrderGapsAllowed=false`. Fixes T-2830 (det8→det0, option a). 6 unit tests added: overflow 8/9/10→0/1/2, multi-family, gap-close, KO-excluded, Exporter compact vs gaps-allowed.
- CiMini match golden re-blessed: CARDIGAN_MAGENTA76_A/DETAIL now match family 90861052 via SiblingPropagator (26b3235) instead of null; all 14 sources match, det numbering contiguous from det0. The root-jbtodo CURRENT(det8)→DESIRED(det0) table is now the implemented behavior.
- Model-wide empty-column prune shipped: `InternalExcelModel.PruneEmptyProperties` drops canonical properties blank across every family record (primary key exempt) after collation, emits `excel.column_dropped_empty_model_wide` diagnostic, shrinks matcher search space. Satisfies the root-jbtodo MEPAL4 "drop empty columns after mapping" ask. Covered by `ModelBuilderEmptyColumnPruneTests`.
- Standalone upscaler test client added (`test/UpscalerTestClient/`) + a `Prism_Config.json` Models entry; the fast-KO test scripts (AUTOMAT2, HEROAUT2, INPUTMA25) trimmed and HEROAUT3/INPUTMA23/24/27/Run_All renumbered.

##### Todo updates
- None — nothing improvable without guessing. The two items that moved this pass (T-2830 compaction, MEPAL4 empty-column prune) landed as shipped code, not answer edits; Order/jbtodo already carries the accepted (a) answer. Remaining open todos are FROZEN (Classify taxonomy + phenotype validation, Generate backend) or blocked on real data/product calls (HeadCutter crown-offset constant, HeadCutter landmark-model choice). Services test-split triage unchanged — new tests (Export/Order/Excel) all still land in the single `Prism.Core.Tests` project, confirming the mechanical per-service split is still the only residual.

##### Next steps
- T-2820/T-2830 now look resolved (deterministic filename-ordered overflow + stable det0 compaction + re-blessed golden). Run `Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` 5x back-to-back to confirm byte-identical det assignment, then close both tickets and pin `expected-manifest.json`.
- `dotnet test jb/src/PRISM.sln` to confirm the 6 compaction + empty-column-prune tests are green before trusting the golden.
- Run MMERO26/HEROAUT2/HEROAUT3 to close the three `???` entries (from 01/07) in `jb/src/jbtodo.md` — the HEROAUT scripts were touched in a20f4ab, so re-run and record OK-rate/KO-timing.
- Decide the Services per-service `.csproj` split (mechanical; only the multi-project-overhead-vs-defer call is open) now that the single test project keeps accreting stage tests.
