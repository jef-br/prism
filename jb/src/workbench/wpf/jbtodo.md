# WPF Workbench Todo

- [ ] Define direct core invocation: say how WPF calls the core library without API upload and download.
  - Impact:
    - Project progress: High - Direct invocation is the main architectural difference between WPF and web.
    - Effect on other TODOs: Blocks - It drives local file selection, progress subscription, resource lifecycle, and parity rules.
  - Industry standard:
    Desktop tools that run batch pipelines in-process pass typed job descriptors to the core engine and receive progress/results through the same contract used by service adapters.
  - Recommended solution:
    Have WPF construct the same `PrismJobRequest` shape as the API, pass local input descriptors directly, expose all `PrismProcessingParameters` in one job-request UI location with binary parameters grouped together, and subscribe to the shared progress event stream.
  - Answer:

- [ ] Define progress visualization behavior: say how WPF displays the same stages and evidence as the web workbench.
  - Impact:
    - Project progress: High - Stage visualization is required for long-running local batches and support review.
    - Effect on other TODOs: Unblocks - It relies on pipeline progress events and shared workbench behavior.
  - Industry standard:
    Desktop batch UIs show stable pipeline stages, per-stage counts, current item, warnings, failures, and completion state without inventing UI-only stages.
  - Recommended solution:
    Render the canonical pipeline stages from core progress events with identical labels and evidence groupings to the web workbench.
  - Answer:

- [ ] Define diagnostic snapshot display: say how WPF shows intermediate images and matcher or transform decisions.
  - Impact:
    - Project progress: High - Snapshot display makes desktop validation useful for image quality and matching diagnostics.
    - Effect on other TODOs: Influences - It consumes model diagnostics, manifest rows, and transform evidence.
  - Industry standard:
    Rich clients display bounded intermediate artifacts with source-stage labels and avoid keeping full unbounded image histories in memory.
  - Recommended solution:
    Show thumbnails and expandable details for normalized image, matcher evidence, classification traits, transform result, KO reason, and final output.
  - Answer:

- [ ] Define parity requirements with web: list what must behave identically between WPF and web workbench.
  - Impact:
    - Project progress: High - Parity prevents two workbenches from becoming separate products with conflicting decisions.
    - Effect on other TODOs: Unblocks - It constrains direct core invocation, web API client behavior, shared views, and diagnostics.
  - Industry standard:
    Multiple frontends for the same processing engine share contracts, fixtures, and acceptance criteria so pipeline behavior is testable independent of UI shell.
  - Recommended solution:
    Require identical input validation semantics, job-parameter availability, stage order, evidence display, output preview, KO grouping, and manifest interpretation.
  - Answer:

- [ ] Define local file selection behavior: say how users choose files, folders, zips, and Excel documents locally.
  - Impact:
    - Project progress: Medium - File selection is important for usability but follows importer input policy.
    - Effect on other TODOs: Influences - It maps to path, directory, zip, and Excel input handling.
  - Industry standard:
    Desktop ingestion tools let users select files and folders, then preview accepted/rejected inputs before running expensive batch work.
  - Recommended solution:
    Support multi-select files and folders, classify selected paths before processing, and display validation results using the same KO reason model as API uploads.
  - Answer:

- [ ] Define WPF project layout: choose where windows, view models, controls, service calls, and styling live.
  - Impact:
    - Project progress: Low - Layout improves maintainability after the behavior and contracts are known.
    - Effect on other TODOs: Independent - It does not materially change pipeline, model, or API decisions.
  - Industry standard:
    WPF applications usually separate views, view models, reusable controls, services, and styling resources so UI logic stays testable.
  - Recommended solution:
    Use folders for `Views`, `ViewModels`, `Controls`, `Services`, and `Styles`, with the Prism core adapter isolated in services.
  - Answer:
