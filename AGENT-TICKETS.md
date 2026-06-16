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

### T-500 Classified Stage

- `Status`: Done
- `Agent type`: worker
- `Runtime profile`: `P4-critical-architecture`
- `Owner`: Classified stage agent
- `Write scope`: classification-related files under `jb/src/core`, classification docs/todos only if clarification is required.
- `Context`: `jb/docs/PRISM-index.md`, `jb/docs/PRISM-classify.md`, `jb/docs/PRISM-models.md`, `jb/docs/ImageNGP/imagePhenotypes.md`, `jb/docs/ImageNGP/ImageFeatures.md`, `jb/docs/ImageNGP/PRODUCTTYPES.MD`, `jb/src/core/Images/Classify/jbtodo.md`
- `Task`: Implement visual dedupe, temporary CLIP boundary through `ImageClassifier.cs`, ImageFeature storage, and selected/candidate ImageNGP storage. Phenotype taxonomy is finalized: 26 phenotypes in `imagePhenotypes.md`. Phenotype assignment is always a hard assignment — no soft probability vectors.
- `Acceptance criteria`: Classified stage is tested and proven before Matched begins.
- `Prework note`: `T-050` added `Prism.Core.Images.Classify.csproj` and moved the classifier boundary to `jb/src/core/Images/Classify/ImageClassifier.cs` so the classification slice builds separately. Actual classification behavior remains unimplemented.
- `Runtime prompt`: You are the Classified Stage agent for PRISM. You are not alone in the codebase; do not revert others' edits. Read `jb/docs/PRISM-index.md`, then classification/model docs, ImageNGP taxonomy docs, and classification todo. Work only on classification-related core files. Implement and test Classified stage behavior through `ImageClassifier.cs`. Phenotype assignment is always a hard assignment. Do not start matching work. Finish with changed files and stage test command.

### T-600 Matched Stage

- `Status`: Done
- `Agent type`: worker
- `Runtime profile`: `P1-feature-worker`
- `Owner`: Matched stage agent
- `Write scope`: matching-related files under `jb/src/core`, match docs only if clarification is required.
- `Context`: `jb/docs/PRISM-index.md`, `jb/docs/PRISM-match.md`, `jb/docs/PRISM-models.md`
- `Task`: Implement matcher aggregation, evidence records, and FamilyID resolution.
- `Acceptance criteria`: Matched stage is tested and proven before Ordered begins.
- `Runtime prompt`: You are the Matched Stage agent for PRISM. You are not alone in the codebase; do not revert others' edits. Read `jb/docs/PRISM-index.md`, then match/model docs. Work only on matching-related core files. Implement and test Matched stage behavior. Do not start ordering work. Finish with changed files and stage test command.

### T-700 Ordered Stage

- `Status`: Done
- `Agent type`: worker
- `Runtime profile`: `P1-feature-worker`
- `Owner`: Ordered stage agent
- `Write scope`: ordering-related files under `jb/src/core`, order docs/config only if clarification is required.
- `Context`: `jb/docs/PRISM-index.md`, `jb/docs/PRISM-order-rename.md`, `jb/docs/PRISM-match.md`, `jb/docs/PRISM-models.md`, `jb/docs/ImageNGP/PRODUCTTYPES.MD`, `jb/docs/ImageNGP/imagePhenotypes.md`
- `Task`: Implement ImageNGP/DetOrder ordering and ordering evidence. Key architecture decisions: (1) phenotype assignment is always a hard assignment — no soft probability vectors; (2) `closeup-image` is the single close-up phenotype (merged detail-*); (3) `illustration-technical-drawing` always gets the last configured det slot; (4) det0 fallback orientation order: FRONT → SIDE → DIAGONAL; (5) per-product-type DetOrderRules are the authoritative slot specification — see `jb/docs/ImageNGP/PRODUCTTYPES.MD`.
- `Acceptance criteria`: Ordered stage is tested and proven before Renamed begins.
- `Runtime prompt`: You are the Ordered Stage agent for PRISM. You are not alone in the codebase; do not revert others' edits. Read `jb/docs/PRISM-index.md`, then order/rename, match, model docs, and the ImageNGP taxonomy docs (PRODUCTTYPES.MD, imagePhenotypes.md). Work only on ordering-related core files. Implement and test Ordered stage behavior. Do not start renaming work. Finish with changed files and stage test command.

### T-800 Renamed Stage

- `Status`: Ready
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

### T-9999 Frozen Fixture Watch

- `Status`: Blocked
- `Agent type`: orchestrator
- `Runtime profile`: `P0-orchestrator`
- `Owner`: Main Codex thread
- `Write scope`: none until thawed
- `Context`: `jb/src/jbtodo.md`, `AGENTFEEDBACK.md`
- `Task`: Keep the `jb/Testing` fixture folder structure todo frozen.
- `Acceptance criteria`: No agent implements or rewrites fixture folder structure until the user explicitly thaws the todo.
- `Runtime prompt`: No sub-agent should receive this ticket until the user explicitly thaws `jb/src/jbtodo.md`.

## Archive — Completed Tickets

All tickets below reached `Done`. Retained here for context; do not re-open or re-implement.

| Ticket | Title | Done date | Key outcome |
|---|---|---|---|
| T-000 | Ticket Board Setup | 2026-06-09 | `AGENT-TICKETS.md` created with milestone gates, team rules, and ticket format. |
| T-050 | Build Boundaries & Solution Setup | 2026-06-10 | `PRISM.sln` with 7 projects (Core.Contracts, Core, Classify, Transform, Api, Wpf, web). M0 gate passed. |
| T-100 | Workbench Bootstrap | 2026-06-09 | Web workbench scaffold: upload, route placeholders, API client. M1 gate passed. |
| T-110 | Workbench Smoke Test | 2026-06-09 | HTTP 200 at `localhost:3000`; all required states confirmed by inspection. |
| T-150 | Integration Test Fixtures | 2026-06-12 | xUnit project + 4 integration tests using SPACINI29/TINY; 8-stage route order asserted. |
| T-200 | API Online End-to-End | 2026-06-12 | Health, config, process, SSE progress, result routes wired to core. M2 gate passed. |
| T-210 | API Smoke Test | 2026-06-12 | All routes verified; BatchManifest with 8 stages in order returned for minimal job. |
| T-300 | Core Backend Shell | 2026-06-12 | `Prism.cs` facade + `Pipeline.cs` 8-stage boundary; config fail-fast; minimal job smoke passed. M3 gate passed. |
| T-310 | Core Smoke Test | 2026-06-12 | 8-stage order verified; fail-fast confirmed by inspection. |
| T-320 | Excel Module Foundation | 2026-06-12 | `ModelBuilder.cs` entry point; IEM, header detection, dedup, FamilyRecord mapping. |
| T-330 | Zip Module Foundation | 2026-06-12 | `ZipHandler.ExtractProcessableMembers`; extraction policy, encrypted/corrupt KO, fixed OK/KO layout. |
| T-400 | Imported Stage | 2026-06-12 | `Importer.cs` fully implemented; `ImageRecord_INPUT` with provenance fields; API ingress spills uploads to `%TEMP%/prism/{jobID}/` so `TempFilePath` is set before enqueue. `SixLabors.ImageSharp` upgraded from 3.1.5 → 3.1.12 (Apache 2.0, CVEs resolved) on 2026-06-15. |
| T-500 | Classified Stage | 2026-06-15 | Visual dedup, ImageFeatureAnalyzer, CLIP classifier, phenotype assignment; 61/61 tests green. |
| T-600 | Matched Stage | 2026-06-16 | ImageMatcher waterfall (brackets 1–3 + label evidence); NumericMatcher (TCD), StringMatcher, ImageLabelingMatcher; MatchEvidence on ImageRecord_LAMBDA; 61/61 tests green. |
| T-700 | Ordered Stage | 2026-06-16 | DetOrderConfig + ImageOrderer; 18-product-type DetOrderRules.json from PRODUCTTYPES.MD; phenotype qualification, deterministic tie-breaking (NGP confidence → filename hint → source index); overflow assignment; OrderEvidence on ImageRecord_LAMBDA; 72/72 tests green. |
| T-workbench-polish | Web Workbench Result & Route Display | 2026-06-15 | JSON manifest display; ZIP download button; job status badge; stage name heading; conditional field rendering. |

---

## Verification Rules

- After ticket-board setup, confirm this file exists and includes all milestone tickets.
- After project/solution setup, run `dotnet build jb/src/PRISM.sln`, separate builds for each project, API/WPF run smoke checks, and web `npm run typecheck` + `npm run build`.
- After each milestone, run the relevant local start, build, or smoke command and record the result in this file.
- After todo/doc sync work, run `rg --files -g 'jbtodo.md' jb/src` and `rg -n "^- \\[ \\]" jb/src`.
- Before advancing milestones, run `git status --short` and confirm agent edits stayed inside assigned ownership.
