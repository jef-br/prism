---
name: new-pipeline-stage
description: Scaffold a new PRISM pipeline stage following all conventions
user-invocable: true
---
Create a new PRISM pipeline stage named {{stage_name}}. Steps:
1. Add the stage enum value to PipelineStageNames.cs
2. Add the shell method in StageShells.cs following the existing pattern
3. Add the facade call in Pipeline.cs (delegate only — no inline logic)
4. Create jb/src/core/[StageName]/ with the stage class and jbtodo.md
5. Create the result record type (one type per file rule)
6. Add the stage to AGENT-TICKETS.md as a new ticket
