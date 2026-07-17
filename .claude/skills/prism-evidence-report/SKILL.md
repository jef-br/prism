---
name: prism-evidence-report
description: Produce a data-backed report on PRISM pipeline behavior (import, CLIP labelling, features, phenotypes, matching, ordering, transform) by running the in-process evidence harness on test datasets. Use whenever the user asks for a report, evidence, or "show me actual data" about how the pipeline handled images — even without a slash command.
user-invocable: true
---

Produce a report on PRISM pipeline behavior using real per-image evidence. The API and result
manifests do NOT expose CLIP tags, feature snapshots, MatchEvidence, or OrderEvidence — the only
way to get them is the in-process harness in this skill's `harness/` folder, which runs the
pipeline directly (visible via `InternalsVisibleTo("Prism.Core.Tests")`).

## 1. Scope the run from the user's ask

- **Datasets:** folder names under `test/datasets/` (e.g. `CiMini`, `TinyTest`). Always include at least `CiMini`. If unspecified: mind size — check the dataset folder before submitting something huge unless required for the report.
- **Sections** — request only what the report needs:

| User asks about | Sections |
|---|---|
| import / ingress / what was accepted or rejected | `import` |
| CLIP labelling | `tags` |
| measured features / analyzers | `features` |
| phenotypes / image roles | `phenotype,features,tags` (phenotypes derive from features) |
| matching / FamilyID attribution | `match,tags` (bracket 4 + convergence use CLIP labels) |
| det ordering / renaming | `order,phenotype` |
| transform / generation decisions | `transform` (slow: also runs Generate + Transform stages) |

Identity fields (name, size, KO status, family) and matching-stage counters are always included.

## 2. Run the harness

1. Copy `harness/EvidenceDumpHarness.cs` (relative to this SKILL.md) to `jb/src/tests/Prism.Core.Tests/EvidenceDumpHarness.cs`. Do not edit it — it is parameterized by env vars.
2. Run (Bash; ~60s model load + roughly 1-2s per image on CPU):
   ```
   export PRISM_EVIDENCE_OUT='<output dir — Desktop unless the user says otherwise>' \
   && export PRISM_EVIDENCE_DATASETS='CiMini,TinyTest' \
   && export PRISM_EVIDENCE_SECTIONS='import,tags,match' \
   && dotnet test jb/src/tests/Prism.Core.Tests/Prism.Core.Tests.csproj --filter "FullyQualifiedName~EvidenceDumpHarness" -v q
   ```
3. **Delete `jb/src/tests/Prism.Core.Tests/EvidenceDumpHarness.cs` immediately after the run.** Parallel sessions commit from this shared worktree and have swept this temp file into a commit before — never leave it lying around, never `git add .` while it exists.

Gotchas:
- `MSB3026/MSB3027 file locked by testhost (PID)` → a stale test host is holding the DLL; `taskkill //PID <pid> //F` and rerun.
- The harness is a no-op without `PRISM_EVIDENCE_OUT` — if the JSON files don't appear, check the env vars reached the process.
- Verify substantive output: non-KO images must have non-empty `TagsInfluential` when `tags` was requested — an all-empty dump means classification didn't run, not "nothing to report".

## 3. Write the report

- lands in `PRISM_EVIDENCE_OUT`
- One `{Dataset}-{datetime}-evidence.json` per dataset. Full dump.
- One Aggregate with a short Python script to `{Dataset}-{datetime}-summary.json` (counts per `MatchEvidence.AcceptedMatcherName`, KO reasons, phenotype coverage)


Report format (matches the accepted 2026-07-10 report):
- Single self-contained HTML file (inline CSS, no external assets, `prefers-color-scheme` aware), written to the Desktop unless told otherwise.
- Plain language — clarity over academic wording; explain terms like phenotype in one sentence where first used.
- Use ACTUAL data: real filenames, real CLIP labels with scores, verbatim `SafeExplanation` strings, real KO reasons with example files.
- Structure: What ran (table) → per-section findings with real samples → verdict table (PASS/warn per check) → bottom line.
- Cross-reference known tickets (AGENT-TICKETS.md) before calling something a new defect — e.g. low phenotype coverage and overflow det-slots are known M5-M7 state, not bugs.
