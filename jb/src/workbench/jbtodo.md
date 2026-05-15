# Workbench Todo

- [ ] Define shared web and WPF behavior: list the pipeline views both workbenches must show identically.
  - Impact:
    - Project progress: High - Shared behavior defines the user-facing contract for observing and validating the pipeline.
    - Effect on other TODOs: Blocks - It drives parity requirements, progress visualization, diagnostic snapshots, and section data shapes.
  - Industry standard:
    Operational workbenches for data pipelines present the same job state, inputs, decisions, outputs, and failures across clients so support staff do not get different interpretations of the same batch.
  - Recommended solution:
    Require both workbenches to show import status, Excel model summary, image collection, matcher evidence, classification traits, transform decisions, KO records, output preview, and the same job-parameter configuration surface.
  - Answer:

- [ ] Define progress event subscription: say how each workbench receives stage updates from API or direct core calls.
  - Impact:
    - Project progress: High - Progress wiring is necessary for usable long-running image batches.
    - Effect on other TODOs: Unblocks - It connects API progress streaming, WPF direct invocation, and web client behavior.
  - Industry standard:
    Large pipeline UIs consume structured progress events with a job ID, stage, current item, counts, severity, and timestamps, using transport-specific adapters rather than custom UI-only messages.
  - Recommended solution:
    Map API job progress to web subscriptions and direct core progress events to WPF, while preserving the same event fields and display order in both.
  - Answer:

- [ ] Define diagnostic snapshot display: say how intermediate images, matcher evidence, and transform decisions are shown.
  - Impact:
    - Project progress: High - Diagnostics are essential for explaining KO decisions and verifying automated image edits.
    - Effect on other TODOs: Unblocks - It depends on model evidence fields and feeds web/WPF diagnostic views.
  - Industry standard:
    Image processing dashboards preserve intermediate artifacts and decision evidence for audit and debugging, but store them as bounded snapshots to avoid unbounded memory or disk growth.
  - Recommended solution:
    Show per-image snapshots for normalized input, matching evidence, classification traits, crop/fill decisions, and output, with links to manifest rows rather than hidden UI summaries.
  - Answer:

- [ ] Define no-hidden-behavior rule: say how workbench proves it is showing raw pipeline decisions without simplifying them.
  - Impact:
    - Project progress: Medium - It improves trust and support quality but relies on core evidence already being emitted.
    - Effect on other TODOs: Influences - It shapes matcher evidence retention, transform result references, and manifest row projection.
  - Industry standard:
    Review tools for automated decision pipelines distinguish raw engine facts from UI interpretation and make evidence traceable to the source stage.
  - Recommended solution:
    Label displayed values by source stage, render raw reason codes and scores, and allow friendly text only as an additional display layer.
  - Answer:

- [ ] Define allowed web and WPF differences: state which differences are allowed because web uploads while WPF can use local files.
  - Impact:
    - Project progress: Medium - Platform differences matter, but they should not change core pipeline semantics.
    - Effect on other TODOs: Influences - It affects upload behavior, local file selection, direct invocation, and parity requirements.
  - Industry standard:
    Multi-client pipeline tools keep job semantics identical while allowing transport-specific differences such as local paths, uploads, authentication, and download handling.
  - Recommended solution:
    Allow differences only at input selection and transport: web uses uploads/URLs through API, WPF may pass local descriptors directly, and both must expose the same `PrismProcessingParameters` controls in one location with binary parameters grouped together.
  - Answer:
