# CLAUDE.md
This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Session start
On the first prompt of every session, read these files before doing anything else:
1. `AGENT-TICKETS.md` — best record of current project/solution work
2. `jb/docs/PRISM-index.md` — source of truth; maps tasks to documentation files
3. `AGENTFEEDBACK.md` — agent reload memory, open work index, current decisions

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
| `DetOrderRules.json` | `jb/src/core/Images/Order/` |
| `HostRules.json` | `jb/src/core/IO/cfg/` |
| `TranslationConfig.json` | `jb/src/core/Images/Match/Translate/` |

On API startup, `PrismApiConfiguration.Load()` validates all config and model assets. Missing config or model files **fail fast and loud** — never silently.

## Code style (C#)

- **Readable over brief.** Main flow reads like a recipe: `Initialize()` sets up resources, `Process()` / `Run()` expresses the workflow, named helper methods perform each step.
- Helper methods are defined below the method that calls them within the same class.
- **XML doc comments** (`/// <summary>`) on every public and internal method.
- Typed config object per subfolder (e.g. `Classify_Config`). No scattered constructor parameters.
- Every external resource (`InferenceSession`, `Mat`) is initialized in a dedicated `Initialize()` method, released in `Dispose()`, and held by a class that implements `IDisposable`.
- Processing lifecycle: validate → initialize → `try/catch/finally` pipeline → release → return structured result object.
- ONNX: name every tensor input/output with a string constant. State expected input shape and normalization in a comment above tensor construction. One method per preprocessing step.
- OpenCV: every `Mat` has a name reflecting its state. State color space (BGR/RGB) at every image boundary. Release intermediate `Mat` objects with `using` or explicit `.Dispose()`.

## Documentation

All accepted project knowledge is in `jb/docs/`. The index at `jb/docs/PRISM-index.md` maps tasks to the relevant doc file — always use it to load only what the current task needs rather than loading everything.

`AGENTFEEDBACK.md` tracks the current open-work index (3 non-empty `jbtodo.md` files, 13 open todos as of last sync). One frozen todo exists at `jb/src/` (fixture folder structure) — keep it frozen until the user explicitly thaws it.

Folder-local `jbtodo.md` files hold unresolved decisions. Once a todo answer is accepted, its decision moves to `jb/docs/` and the todo block is removed. See `AGENTS.md` for the full todo lifecycle protocol.