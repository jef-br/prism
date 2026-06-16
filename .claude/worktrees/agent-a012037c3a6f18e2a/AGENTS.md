Above all, respect existing project folder structure and any structural/naming conventions that can be inferred from patterns you detect.
For each file created, ask in what (sub)folder it belongs unless it is 100% clear from the prompt or filename. 

## First prompt of the day
- Before starting on the first prompt you receive in a day, read:
  1. AGENT-TICKETS.md (best record of project/solution work)
  2. jb/docs/PRISM-index.md (source of truth)
  3. AGENTFEEDBACK.md (useful reload memory)


## General style
- Prefer verbose, readable code over brevity.
- Make the main flow read like a recipe: `Initialize()` sets up the environment,
  `Run()` or `Process()` expresses the workflow, and named helper methods perform each step.
- Extract each meaningful logical step into a named helper method, even when used once.
- Define helper methods below the method that calls them, within the same class.
- Avoid clever one-liners and dense LINQ chains unless the surrounding code already uses that pattern.
- Use explicit, descriptive variable names for business concepts, image-processing steps,
  model inputs/outputs, tensor shapes, threshold values, and file paths.
- Keep comments practical: explain intent, workflow steps, ONNX quirks, empirical threshold
  values, preprocessing requirements, and why a workaround exists.
- Every method should have XML doc comments (`/// <summary>`) explaining its purpose,
  parameters, and return value when relevant.

## Parameters, properties, configuration
- Regardless of technology/language, any and all parameters need to be configurable.
- They cannot be directly inside a line of code but should be retrieved from a json file.
- The json file containing the parameter should sit close to the file containing the code where the parameter is needed.
- On server startup, Prism.cs should build a configuration object that loads all ..._config.jsons

## C# style
- Target .NET 8+. Use modern C# syntax: `record` types for immutable config, primary
  constructors where appropriate, collection expressions, and pattern matching.
- Follow a class-per-responsibility pattern:
  - **One class owns the processing pipeline (`Prism.cs`).**
  - Supporting concerns live in dedicated classes inside subfolders of `core`: `Excel`, `IO`,`Images`, `Models`.
  - Stateless helper logic goes in `static` methods on those classes, or in a `*Extensions` class when extending a framework type.
- Initialize all external resources (ONNX session, OpenCV allocations) in a dedicated `Initialize()` method called before `Run()`, not inline in the workflow.
- Implement `IDisposable` on any class that owns an `InferenceSession`, `Mat`, or other unmanaged resource. Always release them in `Dispose()`.
- Propagate configuration through a typed config object per deepest subfolder of `core`(e.g. `Classify_config.json`) rather than scattered constructor parameters or magic values.
- Preserve a consistent processing lifecycle:
  - validate inputs and config,
  - initialize resources,
  - run the pipeline inside `try/catch/finally`,
  - release resources in `finally` or via `using`,
  - return a structured result object rather than `void`.


## ONNX / ML style
- Wrap `InferenceSession` creation and tensor preparation in dedicated helper methods
    -  `LoadModel()` --> 
    -  `PrepareInputTensor()` --> name too vague, Use Convert/Prepare<X>to<Y>for<Z> (example: ConvertImagesToBase64ForImageLabeling)
    -  `RunInference()` --> name too vague, Be specific about what is being infered. Create helper method per inference process.
    -  `ParseOutputTensor()` --> name too vague, describe what actually happens. (example: ParseTensorToBoundingBox, or ParseXyzToImageLabelingTags)
    -  ...
- Name every tensor input and output with a string constant or config value —
  never scatter raw `"input"` / `"output"` strings across the codebase.
- State the expected input shape and data type in a comment above any tensor construction, including the normalization mean/std values if applicable.
- Keep preprocessing (resize, normalize, channel order, HWC→CHW) in explicit, named steps — one method per transform — rather than a single dense expression.
- Store model paths, input names, output names, confidence thresholds, and label maps in one central config object.
- Use `DEBUG` gates (a config flag or `#if DEBUG`) for diagnostic logging of tensor shapes, raw scores, and intermediate Mat states.

## OpenCV / image processing style
- Declare every `Mat` with a descriptive name that reflects its state:
  `originalImage`, `resizedForModel`, `normalizedFloat`, `ImageLabelingOverlay`.
- Release intermediate `Mat` objects promptly with `using` or explicit `.Dispose()` calls — document why any Mat must outlive its immediate scope.
- Wrap raw OpenCV calls (`Cv2.Resize`, `Cv2.CvtColor`, color normalization) in named helper methods even when the call is a one-liner, so the pipeline reads as a sequence of named steps.
- State the expected color space (BGR, RGB, grayscale) in a comment at every point where images cross a boundary (loaded from disk, passed to model, saved to output). OpenCV loads as BGR by default — make this explicit.
- Store format strings, interpolation flags, border modes, and channel counts in constants or config rather than inline literals.
- Prefer straightforward defensive checks over abstraction: verify that `Mat` is not empty, file paths exist, Excel rows are non-null, and tensor outputs have the expected shape before relying on them.

## Library / API surface style
- The public API should be a small, stable surface: one or two entry-point classes with clear `Process(...)` methods. Implementation details are `internal`.
- Accept file paths and streams as inputs — avoid forcing callers to pre-parse Excel or pre-extract zips.
- Return rich result objects (`ProcessingResult`, `ClassifiedImage`) rather than raw strings or `bool`. Include both success data and failure details.
- Keep vendored or third-party wrapper files isolated; do not rewrite their style unless explicitly asked.

## PRISM - AGENT INSTRUCTIONS
* Read `docs/prism/PRISM-index.md` first. It maps tasks to files.
* Only load the files relevant to your current task — do not load all files upfront.
* Documentation lives in `jb\docs`. Start with `jb\docs\PRISM-index.md`.
* It contains a task-to-file map — use it to load only what the current task needs.


  ### Key rules
  - Pipeline stage order is fixed: Imported > Classified > Matched > Ordered > Renamed > Generated > Transformed > Exported
  - Scoring logic must be readable by a 10-year-old
  - `Prism.cs` contains management code only — no inline logic
  - Missing config or model files fail fast and loud (never silently)


## How to handle Todos  
  - `jbtodo.md` files are temporary working notes for unresolved or not-yet-integrated decisions.
  - Accepted project knowledge belongs in `jb\docs`, with `jb\docs\PRISM-index.md` used as the documentation map.
  - **Todos follow this pattern**:

```
  - [ ] <TodoTitle>: <Brief explanation of what needs to be done> (example: state which top-level folder owns API notes, core pipeline notes, workbench notes, shared docs, and test fixtures.)
  - Impact:
    - <Level of Impact on the project (Low|High)> - <One sentence explaining why this is so>
    - Effect on other TODOs: <List all consequences>
  - Industry standard:
    <Describe the industry standard / best practice solution for this todo in one or two sentences. Avoid terminology and jargon as much as possible in favor of using plain english.>
  - Recommended solution:
    <Describe your recommended solution. Avoid scope creep. Do not add new functionality unless explicitly requested or indispensible for working code. Assume my input towards you is not exhaustive. For example, if I were to tell you "we need multi-language support for 5 languages (De, Es,En,Fr,It)"... Implement a solution without external dependencies that uses industry standard practices for multi-language support. Do not implement 4 or 6 languages, or try to "keep it simple for now".>
  - Answer: <followed by nothing. I will manually write an answer here for you to read in my next prompt, or I will ask you to go over every todo with me.>
  ```
  - **Finished todos / closing a todo**:
    - A satisfactory answer is complete + valid + feasible + does not contradict previous knowledge.
    - If the answer is not satisfactory:
         1. Do not close the todo.
         2. If inconsistency or contradiction is suspected, make a second in-depth check by looking closely at impacted files and related todos to verify.
         3. If inconsistency or contradiction is confirmed, rephrase the todo:
              - Incorporate the answers valid parts
              - use information from the in-depth second check to refine the new todo
              - The new todo should pertain to addressing the inconsistency/contradiction
              - Update the impact, industry standard, and recommended solution sections accordingly
              - Set the answer to empty.
              - 
    - If the answer is satisfactory:
      - Move the accepted decision to the most appropriate `.md` file inside `jb\docs`.
      - Update `jb\docs\PRISM-index.md` if the decision changes the task-to-file map or adds a new documentation surface.
      - Remove the entire todo from the `jbtodo.md` once the answer is fully integrated in the documentation.
      - Do not keep a header-only `jbtodo.md` after closing the last todo unless the user specifically asks for that in the current task.
      - If the `jbtodo.md` file has no remaining open todos after close-out, delete that `jbtodo.md` file.

  - **Frozen todos**:
    - A todo is frozen only when its answer section contains **FROZEN TODO** or **FROZEN**.
    - A frozen todo stays open. Treat the marker as "not ready to answer", not as an answer.
    - The marker means the user cannot resolve the topic right now because the subject is too difficult, the needed information is missing, or the project currently has too much noise around that decision.
    - Do not make thawing a frozen todo the main goal.
    - If a formerly frozen todo later receives an answer, probe it more deeply than a normal answer before accepting it:
      - check impacted files and related todos;
      - ask targeted follow-up questions when consequences are unclear;
      - only accept the answer when you can explain its consequences back to the user for confirmation or refinement.
    - Frozen todos all thaw at the same time only when every remaining open todo is frozen. When that happens, clearly notify the user and remove the frozen markers.
