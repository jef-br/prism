# CLAUDE.md
This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Session start
On the first prompt of every session, read these files before doing anything else:
1. `AGENT-TICKETS.md` — best record of current project/solution work
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
There is no automated test suite yet. For now, validation is done by running the pipeline.

## Architecture

PRISM is a C#/.NET image processing pipeline with a dual-frontend workbench (web + WPF).

**Solution:** `jb/src/PRISM.sln` — 7 projects:
- `Prism.Core.Contracts` — model records
- `Prism.Core` — main pipeline facade + all submodules
- `Prism.Core.Images.Classify` — ONNX/CLIP classification
- `Prism.Core.Images.Transform` — image transformation
- `Prism.Api` — ASP.NET Core 10 minimal API
- `Prism.Workbench.Wpf` — .NET 8 WPF desktop
- Web workbench is npm-based (`jb/src/workbench/web/`), not in `.sln`

### Pipeline (stage order is immutable)
```
Imported → Classified → Matched → Ordered → Renamed → Generated → Transformed → Exported
```

`Prism.cs` is the facade. It contains management code only — no inline logic. Each stage delegates to a dedicated class in a subfolder of `jb/src/core/`.

### Core modules (`jb/src/core/`)
| Folder | Responsibility |
|---|---|
| `Excel/` | Parses Excel input, builds Internal Excel Model (IEM), deduplicates rows/columns |
| `IO/` | Import (multipart, ZIP, URL, stream), export (ZIP or JSON manifest) |
| `Images/Classify/` | ONNX runtime + CLIP model → ImageNGP feature tags per image |
| `Images/Match/` | Three-strategy waterfall (NumericMatcher 55%, StringMatcher 15%, ImageLabelingMatcher 15%, semantic 15%) → resolves FamilyID |
| `Images/Order/` | Orders images within a FamilyID → assigns `_det` indices |
| `Images/Transform/` | Applies visual transformations per ImageNGP state |
| `ImageNGP/` | Feature taxonomy (borders, human, head_visible, orientation, type_of_shot) |
| `Models/` | All C# record definitions (`ImageRecord_INPUT/LAMBDA/OUTPUT/GENERATED`, `FamilyRecord`, `BatchManifest`) |
| `Pipeline/` | `PipelineProgressEvent` — progress tracking via SSE |

### API (`jb/src/api/`)
ASP.NET Core 10 minimal API. Routes:
- `GET /PRISM/health`
- `GET /PRISM/config`
- `POST /PRISM/process` — multipart job ingress
- `GET /PRISM/jobs/{jobID}/progress` — SSE stream
- `GET /PRISM/jobs/{jobID}/result` — JSON or ZIP

### Workbench
Both web and WPF are **decorators over `Prism.cs`** — they provide visibility into pipeline stages, manifests, and progress. No hidden pipeline behavior exists in either workbench. WPF can call `Prism.cs` in-process; web calls the API.

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

Every parameter lives in a JSON config file placed next to the code that uses it. No magic values inline. Key config files:

| File | Location |
|---|---|
| `Prism_Config.json` | `jb/src/core/` |
| `ExcelConfig.json` | `jb/src/core/Excel/` |
| `MatchingConfig.json` | `jb/src/core/Images/Match/` |
| `ImageNGP.json` / `ImageRoles.json` | `jb/src/core/ImageNGP/` |
| `ClipPrompts.json` | `jb/src/core/Images/Classify/` |
| `DetOrderRules.json` | `jb/src/core/Images/Order/` |
| `HostRules.json` | `jb/src/core/IO/cfg/` |
| `TranslationDictionary.json` | `jb/src/core/Images/Match/Translate/` |

On API startup, `PrismApiConfiguration.Load()` validates all config and model assets. Missing config or model files **fail fast and loud** — never silently.

## Code style (C#)

- **One type per file.** Every class, record, enum, interface, struct, and delegate lives in its own `.cs` file named after the type (e.g. `ImageRole` → `ImageRole.cs`). Never define a second type inside an existing file.
- **Readable over brief.** Main flow reads like a recipe: `Initialize()` sets up resources, `Process()` / `Run()` expresses the workflow, named helper methods perform each step.
- Helper methods are defined below the method that calls them within the same class.
- **XML doc comments** (`/// <summary>`) on every public and internal method.
- Typed config object per subfolder (e.g. `Classify_Config`). No scattered constructor parameters.
- Every external resource (`InferenceSession`, `Mat`) is initialized in a dedicated `Initialize()` method, released in `Dispose()`, and held by a class that implements `IDisposable`.
- Processing lifecycle: validate → initialize → `try/catch/finally` pipeline → release → return structured result object.
- ONNX: name every tensor input/output with a string constant. State expected input shape and normalization in a comment above tensor construction. One method per preprocessing step.
- OpenCV: every `Mat` has a name reflecting its state. State color space (BGR/RGB) at every image boundary. Release intermediate `Mat` objects with `using` or explicit `.Dispose()`.
- K&R braces: opening brace on same line as declaration/statement
- Method parameters on a single line, never split across lines
- No XML doc comments on methods; class-level summary only
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
# RTK (Rust Token Killer) - Token-Optimized Commands

## Golden Rule

**Always prefix commands with `rtk`**. If RTK has a dedicated filter, it uses it. If not, it passes through unchanged. This means RTK is always safe to use.

**Important**: Even in command chains with `&&`, use `rtk`:
```bash
# ❌ Wrong
git add . && git commit -m "msg" && git push

# ✅ Correct
rtk git add . && rtk git commit -m "msg" && rtk git push
```

## RTK Commands by Workflow

### Build & Compile (80-90% savings)
```bash
rtk cargo build         # Cargo build output
rtk cargo check         # Cargo check output
rtk cargo clippy        # Clippy warnings grouped by file (80%)
rtk tsc                 # TypeScript errors grouped by file/code (83%)
rtk lint                # ESLint/Biome violations grouped (84%)
rtk prettier --check    # Files needing format only (70%)
rtk next build          # Next.js build with route metrics (87%)
```

### Test (60-99% savings)
```bash
rtk cargo test          # Cargo test failures only (90%)
rtk go test             # Go test failures only (90%)
rtk jest                # Jest failures only (99.5%)
rtk vitest              # Vitest failures only (99.5%)
rtk playwright test     # Playwright failures only (94%)
rtk pytest              # Python test failures only (90%)
rtk rake test           # Ruby test failures only (90%)
rtk rspec               # RSpec test failures only (60%)
rtk test <cmd>          # Generic test wrapper - failures only
```

### Git (59-80% savings)
```bash
rtk git status          # Compact status
rtk git log             # Compact log (works with all git flags)
rtk git diff            # Compact diff (80%)
rtk git show            # Compact show (80%)
rtk git add             # Ultra-compact confirmations (59%)
rtk git commit          # Ultra-compact confirmations (59%)
rtk git push            # Ultra-compact confirmations
rtk git pull            # Ultra-compact confirmations
rtk git branch          # Compact branch list
rtk git fetch           # Compact fetch
rtk git stash           # Compact stash
rtk git worktree        # Compact worktree
```

Note: Git passthrough works for ALL subcommands, even those not explicitly listed.

### GitHub (26-87% savings)
```bash
rtk gh pr view <num>    # Compact PR view (87%)
rtk gh pr checks        # Compact PR checks (79%)
rtk gh run list         # Compact workflow runs (82%)
rtk gh issue list       # Compact issue list (80%)
rtk gh api              # Compact API responses (26%)
```

### JavaScript/TypeScript Tooling (70-90% savings)
```bash
rtk pnpm list           # Compact dependency tree (70%)
rtk pnpm outdated       # Compact outdated packages (80%)
rtk pnpm install        # Compact install output (90%)
rtk npm run <script>    # Compact npm script output
rtk npx <cmd>           # Compact npx command output
rtk prisma              # Prisma without ASCII art (88%)
```

### Files & Search (60-75% savings)
```bash
rtk ls <path>           # Tree format, compact (65%)
rtk read <file>         # Code reading with filtering (60%)
rtk grep <pattern>      # Search grouped by file (75%). Format flags (-c, -l, -L, -o, -Z) run raw.
rtk find <pattern>      # Find grouped by directory (70%)
```

### Analysis & Debug (70-90% savings)
```bash
rtk err <cmd>           # Filter errors only from any command
rtk log <file>          # Deduplicated logs with counts
rtk json <file>         # JSON structure without values
rtk deps                # Dependency overview
rtk env                 # Environment variables compact
rtk summary <cmd>       # Smart summary of command output
rtk diff                # Ultra-compact diffs
```

### Infrastructure (85% savings)
```bash
rtk docker ps           # Compact container list
rtk docker images       # Compact image list
rtk docker logs <c>     # Deduplicated logs
rtk kubectl get         # Compact resource list
rtk kubectl logs        # Deduplicated pod logs
```

### Network (65-70% savings)
```bash
rtk curl <url>          # Compact HTTP responses (70%)
rtk wget <url>          # Compact download output (65%)
```

### Meta Commands
```bash
rtk gain                # View token savings statistics
rtk gain --history      # View command history with savings
rtk discover            # Analyze Claude Code sessions for missed RTK usage
rtk proxy <cmd>         # Run command without filtering (for debugging)
rtk init                # Add RTK instructions to CLAUDE.md
rtk init --global       # Add RTK to ~/.claude/CLAUDE.md
```

## Token Savings Overview

| Category | Commands | Typical Savings |
|----------|----------|-----------------|
| Tests | vitest, playwright, cargo test | 90-99% |
| Build | next, tsc, lint, prettier | 70-87% |
| Git | status, log, diff, add, commit | 59-80% |
| GitHub | gh pr, gh run, gh issue | 26-87% |
| Package Managers | pnpm, npm, npx | 70-90% |
| Files | ls, read, grep, find | 60-75% |
| Infrastructure | docker, kubectl | 85% |
| Network | curl, wget | 65-70% |

Overall average: **60-90% token reduction** on common development operations.
<!-- /rtk-instructions -->

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
