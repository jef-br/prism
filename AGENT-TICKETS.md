# PRISM Agent Tickets

Main Codex thread is the orchestrator: owns ticket status, final integration, conflict resolution, and user-facing summaries.

## Team Rules

- Do not revert or overwrite edits made by other agents.
- Stay inside the ownership and write scope stated on your ticket.
- Read `jb/docs/PRISM-index.md` first; load only docs relevant to the ticket.
- Preserve pipeline order: Imported → Classified → Matched → Ordered → Renamed → Generated → Transformed → Exported.
- Do not advance past a milestone until its gate is documented and passed.
- Unresolved product decisions stay in folder-local `jbtodo.md` files — do not guess policy.

## Agent Reporting Protocol

- Report: ticket ID, changed files, commands run, pass/fail results, blockers, assumptions, next ticket.
- If blocked: stop, ask the orchestrator one targeted question — never ask the user directly.
- If work is found outside ticket scope: report a follow-up ticket, do not edit out of scope.
- Do not self-start the next ticket; orchestrator reviews completed work first.

## Orchestrator Handoff Protocol

- Satisfactory → mark `Done`.
- Incomplete but salvageable → correction to same agent or follow-up ticket.
- Missing product intent → ask user, then unblock agent.
- Milestone gates are authoritative: later tickets stay blocked until the gate passes.

## Ticket Format

Status: Ready, Blocked, Active, Review, Done. Agent type: `explorer`, `worker`, or orchestrator.

## Runtime Profiles

| Profile | Model | Use |
|---|---|---|
| `P0-orchestrator` | parent/default | Main Codex thread, integration, conflict resolution, milestone decisions |
| `P1-feature-worker` | parent/default | Primary implementation tickets |
| `P2-verifier` | haiku | Smoke-test agents — run commands, inspect results, report blockers |
| `P3-scout` | haiku | Read-only exploration, architecture maps, dependency checks |
| `P4-critical-architecture` | parent/default | Cross-cutting contracts or pipeline architecture |

## Milestone Gates

| Milestone | Feature area | Gate condition |
|---|---|---|
| M5 Classification Groundwork | ImageNGP taxonomy + rule correctness | All Classify `jbtodo.md` decisions answered; ghost-front ordering bug fixed; ONNX session migrated to singleton |
| M6 Human & Model Detection | `hero-is-human`, `contains-mannequin`, `has-human`, `head-visible`, `face-visible` | On-model and ghost phenotypes (`front-on-model-*`, `ghost-front/back/side`) fire correctly on labeled images |
| M7 Orientation & Pose | `hero-orientation`, `pose-type`, `camera-angle`, `top-view` | Packshot orientation-split phenotypes (`front-packshot`, `back-packshot`, `side-packshot`) fire from real signal |
| M8 Product & Packaging | `packaging-visible`, `product-type-label`, `multiple-products` | `interior-shot` and packshot phenotypes fire from CLIP; `packaging-visible` no longer always UNKNOWN |
| M9 Composition & Spatial | `product-coverage-ratio`, `image-occupancy`, `salient-bbox`, `vertical-centering`, `horizontal-centering` | Composition phenotypes measured; overflow slot assignment accuracy confirmed |
| M10 Semantic & Content | `text-present`, `logo-present`, `dominant-colors`, `lighting` | Content features populated; transform routing that depends on them verified |
| M11 Production Validation | All 26 phenotypes | < 5% misassignment on labeled validation set; no systematic error on any single phenotype |

## Tickets

### T-1300 · Implement Fetch_HTTPS_DirectFile.cs
**Status:** Ready | **Profile:** P1-feature-worker | **Agent:** worker

Implement `IFetchStrategy` in `jb/src/core/IO/Fetchers/Fetch_HTTPS_DirectFile.cs` to download a file from a direct HTTPS URL.

**Acceptance:**
- Validates URL against `HostRules.json`: allowed schemes, blocked hosts, redirect count limit, timeout.
- Streams download to `%TEMP%/prism/{jobID}/`.
- Returns `ImageRecord_INPUT`.
- `dotnet build jb/src/PRISM.sln` passes.

**Files:** `jb/src/core/IO/Fetchers/Fetch_HTTPS_DirectFile.cs`

---

### T-1400 · Implement Fetch_DropBox.cs
**Status:** Blocked | **Profile:** P1-feature-worker | **Agent:** worker  
**Blocked-by:** Product decision — public-only vs. OAuth-authenticated scope. Not required for V1.

Public shared links (`dropbox.com/s/...?dl=0`) can be normalized (`?dl=1`) and delegated to `Fetch_HTTPS_DirectFile`. Private links require OAuth2 + Dropbox API v2.

**Acceptance (when unblocked):**
- Scope decision documented.
- Public link normalization implemented; delegates to `Fetch_HTTPS_DirectFile`.
- `dotnet build jb/src/PRISM.sln` passes.

**Files:** `jb/src/core/IO/Fetchers/Fetch_DropBox.cs`

---

### T-1500 · Split StageShells.cs into per-stage files
**Status:** Done | **Profile:** P1-feature-worker | **Agent:** worker

`jb/src/core/Pipeline/StageShells.cs` contains 8 `internal static class` declarations (~430 lines). Rule: one type per file, filename matches type name. Naming convention: `ShellStage_Xyz.cs` (not `XyzStageShell.cs`).

**Acceptance:**
- `StageShells.cs` deleted.
- Eight new files in `jb/src/core/Pipeline/`, each with one renamed class:
  - `ShellStage_Import.cs` (was `ImportStageShell`)
  - `ShellStage_Classify.cs` (was `ClassifyStageShell`)
  - `ShellStage_Match.cs` (was `MatchStageShell`)
  - `ShellStage_Order.cs` (was `OrderStageShell`)
  - `ShellStage_Rename.cs` (was `RenameStageShell`)
  - `ShellStage_Generate.cs` (was `GenerateStageShell`)
  - `ShellStage_Transform.cs` (was `TransformStageShell`)
  - `ShellStage_Export.cs` (was `ExportStageShell`)
- `Prism.cs` call sites updated to use new class names.
- `dotnet build jb/src/PRISM.sln` passes.

**Files:** `jb/src/core/Pipeline/StageShells.cs` (delete); `ShellStage_Import.cs` through `ShellStage_Export.cs` (new); `Prism.cs` (call site renames)

---

### T-1600 · SD-8: ImageRecord_OUTPUT Width/Height/Checksum
**Status:** Done | **Profile:** P0-orchestrator

**Resolution — not a bug.** `ImageRecord_OUTPUT` inherits from `ImageRecord_Base` which already declares `Width`, `Height`, and `Checksum`. All `ImageRecord*` types carry these fields via inheritance. No fix required.

**Files:** `jb/src/core/Models/ImageRecord_Base.cs` (no changes)

---

## Verification Rules

- After project/solution setup: `dotnet build jb/src/PRISM.sln`, API/WPF run smoke, web `npm run typecheck` + `npm run build`.
- After each milestone: run relevant smoke and record result in milestone table.
- After todo/doc sync: `rg --files -g 'jbtodo.md' jb/src` and `rg -n "^- \[ \]" jb/src`.
- Before advancing milestones: `git status --short` to confirm edits stayed inside assigned ownership.
