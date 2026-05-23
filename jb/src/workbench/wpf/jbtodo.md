# WPF Workbench Todo

- [ ] Define WPF project layout: choose where windows, view models, controls, service calls, and styling live.
  - Impact:
    - Project progress: Low - Layout improves maintainability after the behavior and contracts are known.
    - Effect on other TODOs: Independent - It does not materially change pipeline, model, or API decisions.
  - Industry standard:
    WPF applications usually separate views, view models, reusable controls, services, and styling resources so UI logic stays testable.
  - Recommended solution:
    Use folders for `Views`, `ViewModels`, `Controls`, `Services`, and `Styles`, with the Prism core adapter isolated in services.
  - Answer:
