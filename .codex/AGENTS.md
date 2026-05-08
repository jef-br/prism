Above all, maintain existing project folder structure.
For each file created, ask in what (sub)folder it belongs unless it is 100% clear from the prompt or filename. 

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
    -  `PrepareInputTensor()` --> name too vague, Use Convert/Prepare<X>to<Y>for<Z> (example: ConvertImagesToBase64ForClassification)
    -  `RunInference()` --> name too vague, Be specific about what is being infered. Create helper method per inference process.
    -  `ParseOutputTensor()` --> name too vague, describe what actually happens. (example: ParseTensorToBoundingBox, or ParseXyzToClassificationTags)
    -  ...
- Name every tensor input and output with a string constant or config value —
  never scatter raw `"input"` / `"output"` strings across the codebase.
- State the expected input shape and data type in a comment above any tensor construction, including the normalization mean/std values if applicable.
- Keep preprocessing (resize, normalize, channel order, HWC→CHW) in explicit, named steps — one method per transform — rather than a single dense expression.
- Store model paths, input names, output names, confidence thresholds, and label maps in one central config object.
- Use `DEBUG` gates (a config flag or `#if DEBUG`) for diagnostic logging of tensor shapes, raw scores, and intermediate Mat states.

## OpenCV / image processing style
- Declare every `Mat` with a descriptive name that reflects its state:
  `originalImage`, `resizedForModel`, `normalizedFloat`, `classificationOverlay`.
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
- 