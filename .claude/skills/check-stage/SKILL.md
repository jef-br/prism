---
name: check-stage
description: Run the reviewer checklist against the current stage implementation
user-invocable: true
---
Review {{stage_name}} stage against the PRISM reviewer checklist:
[ ] Stage class delegates all logic — no inline processing in Pipeline.cs
[ ] Result type is a dedicated record in its own file under Models/
[ ] No XML doc comments on methods or properties — class-level `///<summary>` only
[ ] Config is a typed object loaded from JSON via ConfigLoader, every property `required` (no in-code defaults. ever.)
[ ] Config is validated in the stage constructor, with PrismConfigurationException thrown for invalid config
[ ] Stage constructor is `public` and takes a single `Config` parameter
[ ] Stage class is `sealed`
[ ] IDisposable implemented if any external resources are held
[ ] jbtodo.md decisions are resolved or explicitly deferred

Report pass/fail per item. Block merge if any item fails.
