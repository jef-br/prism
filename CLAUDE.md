# CLAUDE.md
This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Session start
On the first prompt of every session, read these files before doing anything else:
1. `AGENT-TICKETS.md` — open tickets only (Done tickets live in `AGENT-TICKETS-ARCHIVE.md`; read that only when history is needed)
2. `jb/docs/PRISM-index.md` — source of truth; maps tasks to documentation files
3. `AGENTFEEDBACK.md` — static reload memory: project constraints, config locations, behavioral gotchas. Not a ticket board.

## Commands

### Backend (.NET)
```
dotnet build jb/src/PRISM.sln
dotnet run --project jb/src/api/Prism.Api.csproj
```

### Web workbench (Next.js)
```
cd jb/src/workbench/web
npm run dev          # localhost:3000
npm run build
npm run typecheck    # tsc --noEmit
```

### Tests (xUnit, split per public service, per-service suites by namespace)
```
dotnet test jb/src/PRISM.sln                                                         # everything, every project, incl. pipeline integration
dotnet test jb/src/tests/Prism.Services.Matching.Tests/Prism.Services.Matching.Tests.csproj   # one project in isolation
dotnet test jb/src/PRISM.sln --filter "FullyQualifiedName~PrismCoreTests.<Suite>"    # one service suite, any project
dotnet test jb/src/PRISM.sln --filter "FullyQualifiedName!~PipelineIntegrationTests" # unit tests only
```
Five projects under `jb/src/tests/`, split along public-service boundaries (T-3300): `Prism.Services.Matching.Tests` (`Match`, `Order`, `Classify`, `Analyzers`), `Prism.Services.Generate.Tests` (`Generate`), `Prism.Services.Transform.Tests` (`Transform`), `Prism.Services.Upscale.Tests` (`Upscale`), and `Prism.Core.Tests` for everything not a separately-deployable service (`Ingest`, `Excel`, `Export`, `Rename`, `ImageNGP`, `Services`, `ServiceHost`, root namespace `PrismCoreTests` = `PipelineIntegrationTests`). `Prism.Tests.Shared` is a non-test classlib holding `PipelineFixture`, referenced by both `Prism.Core.Tests` and `Prism.Services.Matching.Tests` (MatchLite and SubjectEdgeDetector real-image tests need it too). Namespaces are unchanged by the split, so `--filter "FullyQualifiedName~PrismCoreTests.<Suite>"` still works regardless of which project a suite now lives in. See `jb/docs/PRISM-testing.md`. End-to-end validation additionally runs via `pwsh test/ci/Invoke-CiPipeline.ps1`.

## Architecture

PRISM is a C#/.NET image processing pipeline with a web workbench.

**Solution:** `jb/src/PRISM.sln` — 13 projects:
- `Prism.Core.Contracts` (`core/Models/`) — model records
- `Prism.Core` — pipeline orchestrator + all `Services/`/`lib/` submodules
- `Prism.Services.Matching.Classify` (`core/Services/Matching/Classify/`) — ONNX/CLIP engine
- `Prism.Services.Transform` (`core/Services/Transform/Engine/`) — transform engine
- `Prism.Services.Upscale` (`core/Services/Upscale/Engine/`) — Real-ESRGAN GPU upscaler
- `Prism.Api` (`api/`) — ASP.NET Core 10 minimal API
- `Prism.ServiceHost` (`services/`) — standalone per-service HTTP host for the public services (`PRISM_SERVICE=matching|generate|transform|upscale`); ingest is core and always runs in-process
- `Prism.Core.Tests` + `Prism.Services.{Matching,Generate,Transform,Upscale}.Tests` (`tests/`) — xUnit suites split along public-service boundaries (T-3300)
- `Prism.Tests.Shared` (`tests/`) — fixture classlib (`PipelineFixture`), not a test project

Not in the `.sln`: the npm-based web workbench (`jb/src/workbench/web/`).

### Pipeline (stage order is immutable)
```
Imported → Classified → Matched → Ordered → Renamed → Generated → Transformed → Exported
```

`Pipeline.cs` is the facade. It contains management code only — no inline logic. Each stage delegates to a dedicated class in a subfolder of `jb/src/core/`.

### Core modules (`jb/src/core/`)
`jb/src/core/` is split into **`Services/`** (deployable services, namespace `Prism.Services.*`) and **`lib/`** (support libraries, not services, namespace `Prism.Lib.*`), plus the monolith orchestrator + shared types. Each `Services/<X>/` is self-contained (feature code + service wrapper); a separable-assembly engine lives in a `Services/<X>/Engine/` subfolder.

| Folder | Namespace | Responsibility |
|---|---|---|
| `Services/Matching/` (`Match/`, `Order/`, `Classify/`, `Analyzers/`) | `Prism.Services.Matching` | Waterfall matcher → FamilyID, `_det` ordering, ONNX/CLIP classification, YOLO + per-feature analyzers |
| `Services/Transform/` (+ `Engine/`) | `Prism.Services.Transform` | Applies visual transformations per ImageNGP state |
| `Services/Generate/` | `Prism.Services.Generate` | Synthetic image generation |
| `Services/Upscale/` (+ `Engine/`) | `Prism.Services.Upscale` | Real-ESRGAN GPU upscaling |
| `lib/Excel/` | `Prism.Lib.Excel` | Parses Excel input, builds IEM, dedupes rows/columns |
| `lib/Ingress/` | `Prism.Lib.Ingress` | Import (multipart, ZIP, URL, stream, fetchers) |
| `lib/Export/` | `Prism.Lib.Export` | Export (ZIP or JSON manifest) + manifest models |
| `lib/Zip/` | `Prism.Lib.Zip` | ZIP central-directory reader, member triage |
| `lib/ImageNGP/` | `Prism.Lib.ImageNGP` | Feature taxonomy (borders, human, head_visible, orientation, type_of_shot) |
| `Services/` (root) + `Services/Http/` | `Prism.Core` | Service composition glue: interfaces, HTTP clients, `PipelineServiceFactory`, Ingest wrapper |
| `Models/` | `Prism.Contracts` | All C# record definitions (`ImageRecord_*`, `FamilyIDRecord`, `BatchManifest`) — the `Prism.Core.Contracts` assembly |
| `Pipeline/` | `Prism.Core` / `Prism.Contracts` | `PipelineProgressEvent`, stage names — progress via SSE |

Namespace shim: `GlobalUsings.cs` (or `<Using>` items in the explicit-include sub-projects) keeps call sites free of per-file `using` churn. Contract types are always `Prism.Contracts` regardless of source folder.

### API (`jb/src/api/`)
ASP.NET Core 10 minimal API. Routes:
- `GET /PRISM/health`
- `GET /PRISM/config`
- `POST /PRISM/process` — multipart job ingress
- `GET /PRISM/jobs/{jobID}/progress` — SSE stream
- `GET /PRISM/jobs/{jobID}/result` — JSON or ZIP

### Workbench
The web workbench is a **decorator over `Prism.cs`** — it provides visibility into pipeline stages, manifests, and progress. No hidden pipeline behavior exists in the workbench. The web workbench calls the API.

## Domain vocabulary

| Term | Meaning |
|---|---|
| **FamilyID** | Primary product identifier; becomes the output filename stem |
| **`_det#`** | Zero-based image ordering suffix within a FamilyID (e.g. `_det0`, `_det1`) |
| **ImageNGP** | Canonical measured semantic image state (borders, human, head_visible, orientation, type_of_shot) |
| **ImageRole** | Configured label for a required ImageFeature-state permutation |
| **DetOrderRules** | Per-product-type det-slot → ordered ImageRole preference list |
| **IEM** | Internal Excel Model — collated, deduplicated worksheet data |
| **KO** | Failed/rejected item recorded in the manifest; does not stop the job |
| **Batch** | Complete input set: images + Excel files in any combination of form |

## Configuration-driven design

Every parameter lives in a JSON config file placed next to the code that uses it. No magic values inline.

**No shadow defaults, anywhere (core rule, 2026-07-12, broadened repo-wide 2026-07-17):** every PRISM config class carries **no in-code property initializers** — every property is declared `required` and loads from its JSON file with required-member enforcement. A missing or misspelled key fails loud at load time (as `PrismConfigurationException`), never silently falls back. Applies to every config class in the repo, not just Transform/Analyzers (`transform_Config.json`, `analyzer_Config.json`, `MatchingConfig.json`, and beyond). Existing config classes not yet converted (`ExcelConfig`, `PrismConfiguration`, `TranslationConfig`, `HostRules`, `ProductTypeMap`, `ImageNGP`, `ImageRoles`, `DetOrderRules`, `ClipPrompts`, etc.) are legacy debt pending a dedicated retrofit ticket — new or touched config code must follow this rule regardless. Same rule extends to constructor parameters that thread config-sourced tuning values through code: no C#-level default values on them either — every call site (production and test) must supply them explicitly, so a missing config value is a compile error or a load-time exception, never a silent fallback.

Key config files:

All runtime config JSON is centralized in `jb/src/core/config/` and copied to output via `Prism.Core.csproj` `Content` items:

| File | Location |
|---|---|
| `Prism_Config.json` (incl. `Models`: CLIP/YOLO/Upscale paths) | `jb/src/core/config/` |
| `ExcelConfig.json`, `MatchingConfig.json`, `TranslationDictionary.json` | `jb/src/core/config/` |
| `ImageNGP.json`, `ImageRoles.json`, `ClipPrompts.json` | `jb/src/core/config/` |
| `DetOrderRules.json`, `DetOrderKeywordStems.json` | `jb/src/core/config/` |
| `HostRules.json`, `analyzer_Config.json`, `ProductTypeMap.json` | `jb/src/core/config/` |

Model assets (not copied to every bin) resolve via `ModelAssetLocator.Find` against the source tree: CLIP at `Services/Matching/Classify/ONNX/`, YOLO at `Services/Matching/Analyzers/ONNX/`, Real-ESRGAN at `Services/Upscale/Engine/ONNX/`.

All config resolves through `ConfigLoader` (`RequireFile` / `Section<T>` / `Root<T>`, namespace `Prism.Config`). `PrismConfigLocator` and `ConfigCache` are deleted (T-4560) — do not reintroduce a config cache; see `jb/docs/PRISM-pipeline-core.md`. Every config failure throws `PrismConfigurationException`.

On API startup, `PrismApiConfiguration.Load()` validates all config and model assets. Missing config or model files **fail fast and loud** — never silently.

## Code style (C#)

- **One type per file.** Every class, record, enum, interface, struct, and delegate lives in its own `.cs` file named after the type (e.g. `ImageRole` → `ImageRole.cs`). Never define a second type inside an existing file.
- **Readable over brief.** Main flow reads like a recipe: `Initialize()` sets up resources, `Process()` / `Run()` expresses the workflow, named helper methods perform each step.
- Helper methods are defined below the method that calls them within the same class.
- Typed config object per subfolder (e.g. `Classify_Config`). No scattered constructor parameters.
- Every external resource (`InferenceSession`, `Mat`) is initialized in a dedicated `Initialize()` method, released in `Dispose()`, and held by a class that implements `IDisposable`.
- Processing lifecycle: validate → initialize → `try/catch/finally` pipeline → release → return structured result object.
- ONNX: name every tensor input/output with a string constant. State expected input shape and normalization in a comment above tensor construction. One method per preprocessing step.
- OpenCV: every `Mat` has a name reflecting its state. State color space (BGR/RGB) at every image boundary. Release intermediate `Mat` objects with `using` or explicit `.Dispose()`.
- K&R braces: opening brace on same line as declaration/statement
- Method parameters on a single line, never split across lines
- **Comments: class-level `/// <summary>` only — no XML doc comments on methods or properties.** Inline comments only for constraints the code cannot express (ONNX quirks, empirical thresholds, why a workaround exists). Goal: token-lean files that a human can still read and understand.
- No defensive null-coalescing on internal/known-non-null values
- Collapse boolean conditions: prefer `!= 1` over separate `== 0` / `> 1` checks
- Short, practical variable names (fnTokens, famID, me, tei)
- Closing braces on one line: `}   }   }`
- No `Try` prefix on methods unless returning bool with out param

## Library documentation (context7)

Before writing or editing code that calls any of these libraries, query context7 for current API docs:
- **ImageSharp** — `SixLabors.ImageSharp` (resolve: `sixlabors/imagesharp`)
- **ONNX Runtime** — `Microsoft.ML.OnnxRuntime` (resolve: `microsoft/onnxruntime`)
- **ASP.NET Core** — when using minimal API patterns (resolve: `dotnet/aspnetcore`)

Use `resolve-library-id` first, then `get-library-docs` with the relevant topic (e.g., `image resizing`, `InferenceSession`, `minimal api routing`).

## Documentation

All accepted project knowledge is in `jb/docs/`. The index at `jb/docs/PRISM-index.md` maps tasks to the relevant doc file — always use it to load only what the current task needs rather than loading everything.

Folder-local `jbtodo.md` files hold unresolved decisions. Once a todo answer is accepted, its decision moves to `jb/docs/` and the todo block is removed. See `AGENTS.md` for the full todo lifecycle protocol.

<!-- rtk-instructions v2 -->
# RTK (Rust Token Killer)

Token-optimizing CLI proxy — filters command output before it reaches the model (60-90% savings).
**Prefix every shell command with `rtk`**, including inside `&&` chains. Commands without a dedicated
filter pass through unchanged, so `rtk` is always safe. Filters are trusted in this repo (`rtk trust` done).
Meta commands: `rtk gain` (savings stats), `rtk discover` (missed opportunities), `rtk proxy <cmd>` (bypass filter).
<!-- /rtk-instructions -->

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).