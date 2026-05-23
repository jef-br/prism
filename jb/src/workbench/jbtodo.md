# Workbench Todo

- [ ] Define progress event subscription: say how each workbench receives stage updates from API or direct core calls.
  - Impact:
    - Project progress: High - Progress wiring is necessary for usable long-running image batches.
    - Effect on other TODOs: Unblocks - It connects API progress streaming, WPF direct invocation, and web client behavior.
  - Industry standard:
    Large pipeline UIs consume structured progress events with a job ID, stage, current item, counts, severity, and timestamps, using transport-specific adapters rather than custom UI-only messages.
  - Recommended solution:
    Map API job progress to web subscriptions and direct core progress events to WPF, while preserving the same event fields and display order in both.
  - Answer:
