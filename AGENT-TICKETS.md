# PRISM Agent Tickets

This is the standing ticket board for PRISM sub-agents. The main Codex thread is the orchestrator: it owns ticket status, final integration, conflict resolution, and user-facing summaries.

## Team Rules

- Agents are not alone in the codebase. They must not revert or overwrite edits made by others.
- Agents must stay inside the ownership and write scope stated on their ticket.
- Agents must read `jb/docs/PRISM-index.md` first, then only the docs and local todo files relevant to their ticket.
- Agents must preserve the fixed pipeline order: Imported > Classified > Matched > Ordered > Renamed > Generated > Transformed > Exported.
- Agents must respect and use the existing folder structure in their assigned area. For the web workbench, use the existing `jb/src/workbench/web` structure and its `app`, `components`, `sections`, `services`, and `styles` folders.
- Agents must not move to a later milestone until the current milestone has a documented smoke test and passes.
- Unresolved product decisions stay in folder-local `jbtodo.md` files. Agents must not guess product policy to force a todo closed.
- `jb/src/jbtodo.md` is frozen. Do not implement or thaw the fixture-folder decision until the user explicitly asks for it.

## Agent Reporting Protocol

Every spawned agent must report back to the orchestrator when it is done, blocked, or needs review.

- If done, the agent reports: ticket ID, changed files, commands run, pass/fail results, blockers if any, assumptions, and the recommended next ticket or reason no next ticket should start.
- If blocked, the agent stops work inside its scope and asks the orchestrator one targeted question. The agent must not ask the user directly.
- If the orchestrator can answer from `jb/docs`, local code, configs, `AGENTFEEDBACK.md`, or the ticket board, the orchestrator answers the agent and lets it continue.
- If the orchestrator cannot answer from existing project information, the orchestrator asks the user here, then relays the answer back to the agent.
- If the agent finds work outside its ticket scope, it reports the suggested follow-up ticket instead of editing out of scope.
- If the agent is blocked by a non-existing but scheduled resource, it may draft a new dependency ticket. The ticket must be tightly scoped to that missing resource and must explain why the current ticket is blocked.
- Dependency tickets require orchestrator approval before any agent starts them. The orchestrator may approve only when existing project information is enough to validate the ticket.
- Agents do not start their own next ticket. The orchestrator reviews the completed work first.

## Orchestrator Handoff Protocol

When an agent finishes, the orchestrator reviews the work before moving on.

- If the work is satisfactory: valid, brief-complete, and working under its acceptance criteria, the orchestrator marks the ticket `Done`.
- If the work is incomplete but salvageable, the orchestrator sends a focused correction back to the same agent or creates a follow-up ticket.
- If the work is blocked by missing product intent, the orchestrator asks the user a targeted question before unblocking the agent.
- If a proposed dependency ticket cannot be approved from existing docs, code, configs, `AGENTFEEDBACK.md`, or the ticket board, the orchestrator informs the user in simple English and asks how the missing resource should be handled.
- Once a ticket is `Done`, the orchestrator identifies the next eligible ticket, starts the correct agent/profile, and updates the user in this chat that the team is moving to the new ticket.
- The orchestrator keeps milestone gates authoritative: later tickets remain blocked until the required smoke test has passed.

## Ticket Format

Each ticket uses:
- `Status`: Ready, Blocked, Active, Review, Done.
- `Agent type`: `explorer`, `worker`, or orchestrator.
- `Runtime profile`: model and reasoning effort to use when spawning the agent.
- `Owner`: role responsible for the ticket.
- `Write scope`: files or folders the agent may edit.
- `Context`: files the agent should read first.
- `Task`: concrete work to complete.
- `Acceptance criteria`: conditions required before the ticket can move to Done.
- `Runtime prompt`: prompt text the orchestrator can send to the sub-agent.

Every runtime prompt inherits the shared Agent Reporting Protocol and Orchestrator Handoff Protocol above, even when the per-ticket prompt is shorter.

## Runtime Profiles

Use the parent/default model unless a ticket needs a different tradeoff. Verification agents should use smaller/faster profiles when they only run commands and summarize output. Architecture-heavy or cross-contract work should use higher reasoning.

| Profile | Model | Reasoning | Use |
|---|---|---|---|
| `P0-orchestrator` | parent/default | high | Main Codex thread, ticket integration, conflict resolution, milestone decisions. |
| `P1-feature-worker` | parent/default | high | Primary implementation tickets that create or change app/backend behavior. |
| `P2-verifier` | `gpt-5.4-mini` | medium | Smoke-test agents that run commands, inspect results, and report blockers without production edits. |
| `P3-scout` | `gpt-5.4` | medium | Read-only exploration, architecture maps, dependency checks. |
| `P4-critical-architecture` | parent/default | xhigh | Cross-cutting contracts or pipeline architecture when a mistake would block several milestones. |

For already-started `T-100`, Cicero inherited the parent/default model and reasoning profile.

## Milestone Gates

| Milestone | Area | Gate |
|---|---|---|
| M0 Build Boundaries | `jb/src` | Passed on 2026-06-10: .NET solution and separate API/core/classify/transform/WPF projects build; API and WPF run; web workbench builds through npm. |
| M1 Workbench | `jb/src/workbench` | Passed on 2026-06-09: web workbench starts locally, returns HTTP 200, and renders empty/loading/error/progress/result states without backend data. |
| M2 API | `jb/src/api` | Passed on 2026-06-12: API online end-to-end. T-210 smoke verified health/config/process/progress/result and the pre-core error payload; minimal multipart job returns a BatchManifest with all 8 stages in order. |
| M3 Core | `jb/src/core` | Passed on 2026-06-12: T-300 built the core shell and T-310 verified the build, a minimal end-to-end job emitting all 8 stage names in order, and fail-fast `PrismConfigurationException` on invalid config. Unblocks T-400 (M4). |
| M4 Pipeline | `jb/src/core` stage folders | Stages are implemented and proven one by one in definitive order. |

M1 started with the web workbench. A minimal WPF project shell now exists, opens a real window, and references core directly; WPF parity work remains after API and core contracts stabilize.

## Tickets

### T-000 Ticket Board Setup

- `Status`: Done
- `Agent type`: orchestrator
- `Runtime profile`: `P0-orchestrator`
- `Owner`: Main Codex thread
- `Write scope`: `AGENT-TICKETS.md`
- `Context`: `jb/docs/PRISM-index.md`, `AGENTFEEDBACK.md`
- `Task`: Create this root ticket board with milestone gates, team rules, ticket format, initial tickets, and verification rules.
- `Acceptance criteria`: `AGENT-TICKETS.md` exists, includes all initial tickets, and no production code is changed for setup.
- `Runtime prompt`: No sub-agent needed. The orchestrator owns this setup.

### T-050 Build Boundaries And Solution Setup

- `Status`: Done
- `Runtime agent`: Main thread plus API/WPF workers (`019eb08d-ad05-7761-a517-a779a52cd640`, `019eb08e-00a8-73a1-83f4-f13406b4d63d`)
- `Agent type`: worker
- `Runtime profile`: `P1-feature-worker`
- `Owner`: Main Codex thread
- `Write scope`: `jb/src`, project files, API/WPF startup shells, compile-only contract cleanup
- `Context`: `jb/docs/PRISM-index.md`, `jb/docs/PRISM-pipeline-core.md`, `jb/docs/PRISM-workbench.md`, existing `jb/src` source files
- `Task`: Create a buildable project/solution structure with separate build targets for API, full core, image transformations, image classification, web workbench, and WPF workbench.
- `Acceptance criteria`: All requested projects exist; class-library projects build; API and WPF `dotnet run` succeed; web workbench remains npm-native and builds; no fake PRISM pipeline behavior is introduced.
- `Review note`: Completed `jb/src/PRISM.sln`, `Directory.Build.props`, `Prism.Core.Contracts`, `Prism.Core.Images.Transform`, `Prism.Core.Images.Classify`, `Prism.Core`, `Prism.Api`, and `Prism.Workbench.Wpf`. API and WPF target .NET 10 because this machine has ASP.NET Core/Desktop 10 runtimes installed; class libraries target net8.0.
- `Verification note`: Passed `dotnet build jb/src/PRISM.sln`, separate `dotnet build` for core/classify/transform/API/WPF, API `dotnet run --no-build` with `/PRISM/health` returning 200, WPF `dotnet run --no-build` with a real window staying open, `npm run typecheck`, `npm run build`, and `git diff --check`.
- `Runtime prompt`: No sub-agent should repeat this setup. Future agents should use the project files created here and keep web workbench build/run commands npm-native.

### T-100 Workbench Bootstrap

- `Status`: Done
- `Runtime agent`: Cicero (`019eacec-c4db-72d1-9776-5f4bd0160c75`)
- `Agent type`: worker
- `Runtime profile`: `P1-feature-worker` (started with inherited parent/default model)
- `Owner`: Workbench agent
- `Write scope`: `jb/src/workbench`, `jb/docs/PRISM-workbench.md` only if documentation must be clarified.
- `Context`: `jb/docs/PRISM-index.md`, `jb/docs/PRISM-workbench.md`, `jb/docs/PRISM-api.md`, `jb/src/workbench/workbench.md`, `jb/src/workbench/web/index.tsx`
- `Task`: Make the web workbench runnable as the first frontend milestone. Include upload surface, one job-parameter location, route display placeholders, and an API client boundary. Do not invent hidden pipeline behavior or fake PRISM-owned facts.
- `Acceptance criteria`: The web workbench can start locally, renders without backend data, and has visible states for empty input, loading, API error, progress placeholder, and result placeholder.
- `Review note`: Cicero completed the scaffold under `jb/src/workbench/web`. Dependencies were installed after user approval. `npm run typecheck` and `npm run build` pass locally.
- `Dependency note`: The user approved installing `next`, `react`, `react-dom`, `typescript`, `@types/node`, `@types/react`, and `@types/react-dom`. `npm install` completed under `jb/src/workbench/web`.
- `Audit note`: `npm audit --audit-level=moderate` reports vulnerabilities through `next@16.2.2`/`postcss` and suggests `npm audit fix --force` to install `next@16.2.7`. Do not run force fixes or change dependency versions without explicit approval.
- `Verification note`: `npm run typecheck` passed, `npm run build` passed, and `npm run start` responded with HTTP 200 at `http://127.0.0.1:3000` on 2026-06-09. Source inspection confirms the scaffold keeps the `app`, `components`, `sections`, `services`, and `styles` folders and exposes the required empty, loading, API error, progress placeholder, and result placeholder states without backend data.
- `Build-boundary note`: `T-050` added a separate runnable WPF workbench shell under `jb/src/workbench/wpf`; it opens a real window but does not yet implement full WPF parity.
- `Runtime prompt`: You are the Workbench Bootstrap agent for PRISM. You are not alone in the codebase; do not revert others' edits. Read `jb/docs/PRISM-index.md`, then the workbench/API docs and `jb/src/workbench` files. Work only in `jb/src/workbench` unless a workbench doc clarification is unavoidable. Make the web workbench runnable first, with upload surface, grouped job parameters, route placeholders, and API client boundary. Do not fake hidden pipeline behavior. Finish by listing changed files and the local start/smoke command.

### T-110 Workbench Smoke Test

- `Status`: Done
- `Runtime agent`: Plato (`019eade7-e0e6-7c40-8c46-53a8be872a8e`)
- `Agent type`: worker
- `Runtime profile`: `P2-verifier`
- `Owner`: Verification agent
- `Write scope`: `AGENT-TICKETS.md` for result notes only, plus generated build/cache output if commands create it.
- `Context`: `AGENT-TICKETS.md`, `jb/docs/PRISM-workbench.md`, workbench package/project files once they exist.
- `Task`: Prove the web workbench starts locally and renders required states without backend data.
- `Acceptance criteria`: Start command succeeds, smoke scenario is documented, and any failure is recorded with exact command/output summary.
- `Verification note`: Plato ran `npm run typecheck`, `npm run build`, and `npm run start`; HTTP probe returned `STATUS:200` from `http://127.0.0.1:3000`. Source inspection confirmed empty input, loading, API error, progress placeholder, result placeholder, fixed route order, upload support, one grouped parameter location, and typed API client boundary.
- `Runtime prompt`: You are the Workbench Verification agent for PRISM. Do not edit production code. Run the documented workbench start/build/smoke commands after T-100. Verify empty, loading, and error states can render without backend data. Update only ticket result notes if asked by the orchestrator. Finish with commands run, pass/fail result, and blockers.

### T-200 API Online End-to-End

- `Status`: Done
- `Orchestrator note`: Marked Done on 2026-06-12 after the T-210 smoke PASS confirmed the API is online end-to-end. This satisfies the M2 gate and unblocks T-300.
- `Runtime agent`: Main thread
- `Agent type`: worker
- `Runtime profile`: `P1-feature-worker`
- `Owner`: API agent
- `Write scope`: `jb/src/api`, API-facing contracts under `jb/src/core` only when needed, `jb/docs/PRISM-api.md` only if docs need clarification.
- `Context`: `jb/docs/PRISM-index.md`, `jb/docs/PRISM-api.md`, `jb/docs/PRISM-pipeline-core.md`, `jb/src/api`, `jb/src/core/Prism.cs`
- `Task`: Create the API host/routes for health, config, process, progress, and result. API is not considered online until routes call core and return documented shapes.
- `Acceptance criteria`: API can call core for a minimal real smoke job, exposes progress/result URLs, and returns documented pre-core errors for invalid input.
- `Prework note`: `T-050` created `jb/src/api/Prism.Api.csproj` and a runnable minimal API host. `/PRISM/health` returns 200, `/PRISM/config` reports configuration is not wired, and `/PRISM/process` returns 501. This does not satisfy T-200 because process/progress/result do not call core yet.
- `Review note`: Main thread implemented real API endpoints for health, config, process, live progress SSE, and result retrieval. Added a minimal core adapter and shared job/result/progress contracts so API routes call `Prism.Process(...)` without implementing full pipeline behavior before T-300.
- `Verification note`: Passed `dotnet build jb/src/PRISM.sln`. Started API on `http://127.0.0.1:5057`; `GET /PRISM/health` and `GET /PRISM/config` returned 200 with readiness/config fields; invalid non-multipart `POST /PRISM/process` returned documented pre-core error payload; minimal multipart request with one `.jpg` and one `.xlsx` returned a queued job envelope with progress/result URLs; `GET /PRISM/jobs/{JobID}/progress` streamed SSE route-stage events; `GET /PRISM/jobs/{JobID}/result` returned stored JSON result with `BatchManifest`.
- `Runtime prompt`: You are the API Online agent for PRISM. You are not alone in the codebase; do not revert others' edits. Read `jb/docs/PRISM-index.md`, then API and pipeline docs. Work in `jb/src/api` and only touch API-facing core contracts when required. Implement health/config/process/progress/result routes that call core end-to-end. Finish with changed files and API smoke command.

### T-210 API Smoke Test

- `Status`: Done
- `Verification note`: Passed on 2026-06-12 (haiku verifier). `dotnet build jb/src/PRISM.sln` succeeded (0 errors, 14 warnings). API started on http://localhost:5000. `GET /PRISM/health` → 200 with readiness fields; `GET /PRISM/config` → 200 with config fields; non-multipart `POST /PRISM/process` → 400 with documented pre-core error payload (`INVALID_PAYLOAD`); minimal multipart (1 `.jpg` + 1 `.xlsx`) → 202 queued envelope with progress/result URLs; `GET /PRISM/jobs/{id}/progress` → 410 after terminal completion (documented replay behavior); `GET /PRISM/jobs/{id}/result` → 200 with BatchManifest listing all 8 stages in order (Imported→Classified→Matched→Ordered→Renamed→Generated→Transformed→Exported). Test image/xlsx were placeholders.
- `Agent type`: worker
- `Runtime profile`: `P2-verifier`
- `Owner`: Verification agent
- `Write scope`: `AGENT-TICKETS.md` for result notes only, plus generated build/cache output if commands create it.
- `Context`: `AGENT-TICKETS.md`, `jb/docs/PRISM-api.md`, API project files once they exist.
- `Task`: Prove API online behavior against documented routes and error payloads.
- `Acceptance criteria`: Health/config respond, minimal process path calls core, progress/result shapes are exposed, invalid payload returns documented pre-core error shape.
- `Runtime prompt`: You are the API Verification agent for PRISM. Do not edit production code. Run focused API smoke commands after T-200. Verify health/config/process/progress/result behavior and invalid payload errors. Finish with commands run, pass/fail result, and blockers.

### T-300 Core Backend Shell

- `Status`: Done
- `Runtime agent`: sonnet worker (2026-06-12)
- `Orchestrator note`: Marked Done on 2026-06-12. Changes confined to `jb/src/core` (rewrote `Prism.cs` as a management-only facade and `Pipeline.cs` as the 8-stage boundary owner; added `PrismConfiguration.cs` with fail-fast validation, `PrismConfigurationException.cs`, `PrismConfigLocator.cs`, `Pipeline/StageShells.cs`). API `Process(...)` contract preserved. `dotnet build jb/src/PRISM.sln` succeeded (0 errors; 14 pre-existing warnings). Smoke run returned a Completed job whose RouteSummaries list all 8 stages in order. Pending formal T-310 verification before M3 is declared passed.
- `Agent type`: worker
- `Runtime profile`: `P4-critical-architecture`
- `Owner`: Core backend agent
- `Write scope`: `jb/src/core`, core docs only if documentation must be clarified.
- `Context`: `jb/docs/PRISM-index.md`, `jb/docs/PRISM-pipeline-core.md`, `jb/docs/PRISM-models.md`, `jb/src/core/Prism.cs`, `jb/src/core/Pipeline.cs`, `jb/src/core/Prism_Config.json`
- `Task`: Build the global Prism backend shell: `Prism.cs`, config loading, request/result contracts, job lifecycle, and `Pipeline.cs` stage boundaries. Keep `Prism.cs` management-only.
- `Acceptance criteria`: Core starts with validated config and can run one minimal end-to-end job path that emits every definitive stage name in order.
- `Prework note`: `T-050` created buildable core project boundaries: `Prism.Core`, `Prism.Core.Contracts`, `Prism.Core.Images.Classify`, and `Prism.Core.Images.Transform`. These projects compile, but `Prism.cs`, config startup, request/result contracts, and a minimal stage-order job path are not implemented yet.
- `Runtime prompt`: You are the Core Backend Shell agent for PRISM. You are not alone in the codebase; do not revert others' edits. Read `jb/docs/PRISM-index.md`, then pipeline/model docs and core files. Work only in `jb/src/core` unless a core doc clarification is required. Build the Prism facade, config startup, request/result contracts, job lifecycle, and Pipeline stage shell. Preserve `Prism.cs` as readable management-only code. Finish with changed files and core smoke command.

### T-310 Core Smoke Test

- `Status`: Done
- `Runtime agent`: haiku verifier (2026-06-12)
- `Verification note`: Passed on 2026-06-12. `dotnet build jb/src/PRISM.sln` → 0 errors. Minimal multipart job (1 `.jpg` + 1 `.xlsx`) returned a Completed result whose RouteSummaries list all 8 stages in exact order (Imported→Classified→Matched→Ordered→Renamed→Generated→Transformed→Exported). Fail-fast verified by code inspection: `PrismConfiguration.Load()` throws `PrismConfigurationException` on invalid/missing config, invoked from the `Prism` constructor via `Initialize()`. (Note: fail-fast confirmed by inspection rather than a destructive runtime test, to avoid leaving the config broken.) Working tree left clean; config restored.
- `Agent type`: worker
- `Runtime profile`: `P2-verifier`
- `Owner`: Verification agent
- `Write scope`: `AGENT-TICKETS.md` for result notes only, plus generated build/cache output if commands create it.
- `Context`: `AGENT-TICKETS.md`, `jb/docs/PRISM-pipeline-core.md`, core project files once they exist.
- `Task`: Prove core startup/config validation and a minimal end-to-end job through all stage names.
- `Acceptance criteria`: Core smoke command passes and verifies stage order exactly: Imported > Classified > Matched > Ordered > Renamed > Generated > Transformed > Exported.
- `Runtime prompt`: You are the Core Verification agent for PRISM. Do not edit production code. Run the core build/smoke commands after T-300. Verify config startup and a minimal job emitting all definitive stage names in order. Finish with commands run, pass/fail result, and blockers.

### T-320 Excel Module Foundation

- `Status`: Done
- `Orchestrator note`: Marked Done on 2026-06-12. Files stay inside `jb/src/core/Excel` (entry point `ModelBuilder.cs`), no `Pipeline.cs` integration as required, already committed in `c04dec0`, and compile clean as part of the T-210 solution build.
- `Runtime agent`: Singer (`019eadeb-1e1d-7253-8fad-51f394afaec1`)
- `Agent type`: worker
- `Runtime profile`: `P1-feature-worker`
- `Owner`: Excel module agent
- `Write scope`: `jb/src/core/Excel`, `jb/docs/PRISM-excel.md` only if documentation must be clarified.
- `Context`: `jb/docs/PRISM-index.md`, `jb/docs/PRISM-excel.md`, `jb/docs/PRISM-models.md`, `jb/src/core/Excel`
- `Task`: Build the Excel module foundation in the existing `jb/src/core/Excel` folder. Implement the Internal Excel Model flow, header detection, primary-key validation, duplicate row/column handling, and `FamilyRecord` mapping according to docs. Do not integrate into `Pipeline.cs` yet.
- `Acceptance criteria`: Excel module code is internally coherent, respects existing folder structure, exposes a clear entry point for later Imported-stage integration, and includes a local smoke/test path where possible.
- `Review note`: Singer completed the Excel foundation inside `jb/src/core/Excel` only. Entry point is `ModelBuilder.BuildFromExcelFiles(...)`. Reported smoke compile/test passed in a temporary `net10.0` project with result `records=2; diagnostics=2; invalidKey=True; mergedFamily=True; conflict=True`. No pipeline/API/workbench/docs integration was touched.
- `Build-boundary note`: `T-050` links `FamilyRecord` into `Prism.Core.Contracts` because image/input contract types expose family candidates. Excel itself remains owned by `Prism.Core` until Imported-stage integration.
- `Runtime prompt`: You are the Excel Module Foundation agent for PRISM. You are not alone in the codebase; do not revert others' edits. Read `jb/docs/PRISM-index.md`, then Excel/model docs and the existing `jb/src/core/Excel` files. Work only in `jb/src/core/Excel` unless a focused Excel doc clarification is required. Build the Excel module foundation in the existing folder structure: header detection, primary key validation, duplicate handling, IEM construction, and FamilyRecord mapping. Do not edit `Pipeline.cs` or integrate with other stages. Finish with changed files, smoke/test command if available, and blockers.

### T-330 Zip Module Foundation

- `Status`: Done
- `Orchestrator note`: Marked Done on 2026-06-12. Files stay inside `jb/src/core/Zip` (entry point `ZipHandler.cs`), no `Pipeline.cs` integration as required, already committed in `c04dec0`, and compile clean as part of the T-210 solution build.
- `Runtime agent`: Averroes (`019eadeb-6892-72f0-ac90-2f07ca1c83b2`)
- `Agent type`: worker
- `Runtime profile`: `P1-feature-worker`
- `Owner`: Zip module agent
- `Write scope`: `jb/src/core/Zip`, zip-related IO docs only if documentation must be clarified.
- `Context`: `jb/docs/PRISM-index.md`, `jb/docs/PRISM-io-import.md`, `jb/docs/PRISM-api.md`, `jb/docs/PRISM-models.md`, `jb/src/core/Zip`
- `Task`: Build the Zip module foundation in the existing `jb/src/core/Zip` folder. Implement zip extraction policy, member triage, encrypted/corrupt member KO classification, and fixed output layout constants according to docs. Do not integrate into `Pipeline.cs` yet.
- `Acceptance criteria`: Zip module code is internally coherent, respects existing folder structure, exposes a clear entry point for later Imported/Exported-stage integration, and includes a local smoke/test path where possible.
- `Review note`: Averroes completed the Zip foundation inside `jb/src/core/Zip` only. Entry point is `ZipHandler.ExtractProcessableMembers(...)`. Reported isolated classlib smoke build passed with `dotnet build .tmp\\zip-smoke-t330\\ZipSmoke.csproj --no-restore`; `git diff --check -- jb/src/core/Zip` passed. `ZipLayout.json` was deleted because docs say `OK`, `KO`, and `manifest.json` are fixed constants, not configurable.
- `Runtime prompt`: You are the Zip Module Foundation agent for PRISM. You are not alone in the codebase; do not revert others' edits. Read `jb/docs/PRISM-index.md`, then IO/import, API, and model docs plus the existing `jb/src/core/Zip` files. Work only in `jb/src/core/Zip` unless a focused zip doc clarification is required. Build the Zip module foundation in the existing folder structure: extraction policy, member triage, encrypted/corrupt member KO classification, and fixed OK/KO/manifest layout constants. Do not edit `Pipeline.cs` or integrate with other stages. Finish with changed files, smoke/test command if available, and blockers.

### T-400 Imported Stage

- `Status`: Ready
- `Hold note`: M3 passed on 2026-06-12, so this ticket is now eligible. ON HOLD by user decision — the orchestrator paused the wave after M3; do not start T-400 (or any M4 stage) until the user gives the go-ahead.
- `Agent type`: worker
- `Runtime profile`: `P1-feature-worker`
- `Owner`: Imported stage agent
- `Write scope`: import-related files under `jb/src/core`, import docs only if clarification is required.
- `Context`: `jb/docs/PRISM-index.md`, `jb/docs/PRISM-io-import.md`, `jb/docs/PRISM-excel.md`, `jb/docs/PRISM-models.md`
- `Task`: Implement import normalization, Excel intake, zip/local/remote input handling, and import KO behavior according to docs.
- `Acceptance criteria`: Imported stage is tested and proven before Classified begins.
- `Runtime prompt`: You are the Imported Stage agent for PRISM. You are not alone in the codebase; do not revert others' edits. Read `jb/docs/PRISM-index.md`, then IO/import, Excel, and model docs. Work only on import-related core files. Implement and test Imported stage behavior. Do not start classification work. Finish with changed files and stage test command.

### T-500 Classified Stage

- `Status`: Blocked by T-400
- `Agent type`: worker
- `Runtime profile`: `P4-critical-architecture`
- `Owner`: Classified stage agent
- `Write scope`: classification-related files under `jb/src/core`, classification docs/todos only if clarification is required.
- `Context`: `jb/docs/PRISM-index.md`, `jb/docs/PRISM-classify.md`, `jb/docs/PRISM-models.md`, `jb/src/core/Images/Classify/jbtodo.md`
- `Task`: Implement visual dedupe, temporary CLIP boundary through `ImageClassifier.cs`, ImageFeature storage, and selected/candidate ImageNGP storage.
- `Acceptance criteria`: Classified stage is tested and proven before Matched begins.
- `Prework note`: `T-050` added `Prism.Core.Images.Classify.csproj` and moved the classifier boundary to `jb/src/core/Images/Classify/ImageClassifier.cs` so the classification slice builds separately. Actual classification behavior remains unimplemented.
- `Runtime prompt`: You are the Classified Stage agent for PRISM. You are not alone in the codebase; do not revert others' edits. Read `jb/docs/PRISM-index.md`, then classification/model docs and classification todo. Work only on classification-related core files. Implement and test Classified stage behavior through `ImageClassifier.cs`. Do not start matching work. Finish with changed files and stage test command.

### T-600 Matched Stage

- `Status`: Blocked by T-500
- `Agent type`: worker
- `Runtime profile`: `P1-feature-worker`
- `Owner`: Matched stage agent
- `Write scope`: matching-related files under `jb/src/core`, match docs only if clarification is required.
- `Context`: `jb/docs/PRISM-index.md`, `jb/docs/PRISM-match.md`, `jb/docs/PRISM-models.md`
- `Task`: Implement matcher aggregation, evidence records, and FamilyID resolution.
- `Acceptance criteria`: Matched stage is tested and proven before Ordered begins.
- `Runtime prompt`: You are the Matched Stage agent for PRISM. You are not alone in the codebase; do not revert others' edits. Read `jb/docs/PRISM-index.md`, then match/model docs. Work only on matching-related core files. Implement and test Matched stage behavior. Do not start ordering work. Finish with changed files and stage test command.

### T-700 Ordered Stage

- `Status`: Blocked by T-600
- `Agent type`: worker
- `Runtime profile`: `P1-feature-worker`
- `Owner`: Ordered stage agent
- `Write scope`: ordering-related files under `jb/src/core`, order docs/config only if clarification is required.
- `Context`: `jb/docs/PRISM-index.md`, `jb/docs/PRISM-order-rename.md`, `jb/docs/PRISM-match.md`, `jb/docs/PRISM-models.md`
- `Task`: Implement ImageNGP/DetOrder ordering and ordering evidence.
- `Acceptance criteria`: Ordered stage is tested and proven before Renamed begins.
- `Runtime prompt`: You are the Ordered Stage agent for PRISM. You are not alone in the codebase; do not revert others' edits. Read `jb/docs/PRISM-index.md`, then order/rename, match, and model docs. Work only on ordering-related core files. Implement and test Ordered stage behavior. Do not start renaming work. Finish with changed files and stage test command.

### T-800 Renamed Stage

- `Status`: Blocked by T-700
- `Agent type`: worker
- `Runtime profile`: `P1-feature-worker`
- `Owner`: Renamed stage agent
- `Write scope`: rename/export-name-related files under `jb/src/core`, order/rename docs only if clarification is required.
- `Context`: `jb/docs/PRISM-index.md`, `jb/docs/PRISM-order-rename.md`, `jb/docs/PRISM-models.md`
- `Task`: Implement output filename stems, `_det` suffixes, unmatched naming, and collision handling.
- `Acceptance criteria`: Renamed stage is tested and proven before Generated begins.
- `Runtime prompt`: You are the Renamed Stage agent for PRISM. You are not alone in the codebase; do not revert others' edits. Read `jb/docs/PRISM-index.md`, then order/rename and model docs. Work only on renaming-related core files. Implement and test Renamed stage behavior. Do not start generation work. Finish with changed files and stage test command.

### T-900 Generated Stage

- `Status`: Blocked by T-800
- `Agent type`: worker
- `Runtime profile`: `P1-feature-worker`
- `Owner`: Generated stage agent
- `Write scope`: generation-related files under `jb/src/core`, transform/generate docs only if clarification is required.
- `Context`: `jb/docs/PRISM-index.md`, `jb/docs/PRISM-transform-generate.md`, `jb/docs/PRISM-models.md`
- `Task`: Implement generation decision shell and generated-record flow. Local generation may remain gated when dependencies are not ready.
- `Acceptance criteria`: Generated stage is tested and proven before Transformed begins.
- `Runtime prompt`: You are the Generated Stage agent for PRISM. You are not alone in the codebase; do not revert others' edits. Read `jb/docs/PRISM-index.md`, then transform/generate and model docs. Work only on generation-related core files. Implement and test the Generated stage decision shell. Do not start transformation work. Finish with changed files and stage test command.

### T-1000 Transformed Stage

- `Status`: Blocked by T-900
- `Agent type`: worker
- `Runtime profile`: `P4-critical-architecture`
- `Owner`: Transformed stage agent
- `Write scope`: transform-related files under `jb/src/core`, transform docs/todos only if clarification is required.
- `Context`: `jb/docs/PRISM-index.md`, `jb/docs/PRISM-transform-generate.md`, `jb/docs/PRISM-classify.md`, `jb/docs/PRISM-models.md`, `jb/src/core/Images/Transform/jbtodo.md`
- `Task`: Implement transform policy, problem-image path, crop/fill/resize decisions, and `ImageTransformationResult`.
- `Acceptance criteria`: Transformed stage is tested and proven before Exported begins.
- `Prework note`: `T-050` added `Prism.Core.Images.Transform.csproj` so the transform slice builds separately. The slice currently compiles only the transform boundary and shared transform contracts; concrete transform behavior remains unimplemented.
- `Runtime prompt`: You are the Transformed Stage agent for PRISM. You are not alone in the codebase; do not revert others' edits. Read `jb/docs/PRISM-index.md`, then transform/generate, classify, and model docs plus transform todo. Work only on transform-related core files. Implement and test Transformed stage behavior. Do not start export work. Finish with changed files and stage test command.

### T-1100 Exported Stage

- `Status`: Blocked by T-1000
- `Agent type`: worker
- `Runtime profile`: `P1-feature-worker`
- `Owner`: Exported stage agent
- `Write scope`: export-related files under `jb/src/core`, API/result docs only if clarification is required.
- `Context`: `jb/docs/PRISM-index.md`, `jb/docs/PRISM-api.md`, `jb/docs/PRISM-pipeline-core.md`, `jb/docs/PRISM-models.md`
- `Task`: Implement zip/json output and `manifest.json` export.
- `Acceptance criteria`: Exported stage is tested and proven, and full pipeline smoke passes through all definitive stages.
- `Runtime prompt`: You are the Exported Stage agent for PRISM. You are not alone in the codebase; do not revert others' edits. Read `jb/docs/PRISM-index.md`, then API, pipeline, and model docs. Work only on export-related core files. Implement and test Exported stage zip/json output and `manifest.json`. Finish with changed files and full pipeline smoke command.

### T-999 Frozen Fixture Watch

- `Status`: Blocked
- `Agent type`: orchestrator
- `Runtime profile`: `P0-orchestrator`
- `Owner`: Main Codex thread
- `Write scope`: none until thawed
- `Context`: `jb/src/jbtodo.md`, `AGENTFEEDBACK.md`
- `Task`: Keep the `jb/Testing` fixture folder structure todo frozen.
- `Acceptance criteria`: No agent implements or rewrites fixture folder structure until the user explicitly thaws the todo.
- `Runtime prompt`: No sub-agent should receive this ticket until the user explicitly thaws `jb/src/jbtodo.md`.

## Verification Rules

- After ticket-board setup, confirm this file exists and includes all milestone tickets.
- After project/solution setup, run `dotnet build jb/src/PRISM.sln`, separate builds for each project, API/WPF run smoke checks, and web `npm run typecheck` + `npm run build`.
- After each milestone, run the relevant local start, build, or smoke command and record the result in this file.
- After todo/doc sync work, run `rg --files -g 'jbtodo.md' jb/src` and `rg -n "^- \\[ \\]" jb/src`.
- Before advancing milestones, run `git status --short` and confirm agent edits stayed inside assigned ownership.
