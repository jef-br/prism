---
name: check-stage
description: Run the reviewer checklist against the current stage implementation
user-invocable: true
---
Review {{stage_name}} stage against the PRISM reviewer checklist:
[ ] Stage class delegates all logic — no inline processing in Pipeline.cs
[ ] Result type is a dedicated record in its own file
[ ] All public/internal methods have XML doc comments
[ ] Config is a typed object loaded from JSON (not constructor parameters)
[ ] IDisposable implemented if any external resources are held
[ ] jbtodo.md decisions are resolved or explicitly deferred
Report pass/fail per item. Block merge if any item fails.
