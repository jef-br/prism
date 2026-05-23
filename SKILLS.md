# Prism Todo Workflow

Use this workflow when creating, discussing, refining, or finalizing Prism `jbtodo.md` items.

## Source Of Truth

- Read `PRISM-information.md` before working on Prism todos.
- Treat folder-local `jbtodo.md` files as the source of truth for unresolved todos.
- Treat `PRISM-information.md` as the source of truth for accepted completed todo decisions.
- Use `AGENTFEEDBACK.md`, `AGENTS.md`, code, configs, comments, and nearby docs as supporting repo knowledge.
- Do not edit `AGENTS.md` unless the user explicitly grants permission for that specific edit.

## Creating Todos

Before creating a todo, analyze the goal using all relevant repo knowledge. Search nearby code, configs, comments, existing todos, `PRISM-information.md`, `AGENTFEEDBACK.md`, and applicable project docs before asking the user questions.

Ask questions until the todo phrasing is clear enough to remove unintended ambiguity. Stay close to the user's original phrasing and scope. Do not expand the todo into adjacent feature work unless the repo context proves that extra scope is required for a valid decision.

Create each todo in the nearest owning folder-local `jbtodo.md` file using this shape:

```markdown
- [ ] <TodoTitle>: <Brief explanation of what needs to be decided or defined.>
  - Impact:
    - Project progress: <Low|Medium|High> - <What answering this todo unlocks or advances for the project.>
    - Effect on other TODOs: <Blocks|Unblocks|Influences|None> - <Which todo areas, implementation stages, or decisions this answer affects.>
  - Industry standard:
    <State the industry-standard or best-practice approach for this type of problem in plain language.>
  - Recommended solution:
    <State Codex's recommended way to solve this todo, grounded in Prism repo context and avoiding scope creep.>
  - Answer:
```

`Impact` must describe the progress unlocked by answering the todo and the effect on other todos. `Industry standard` must describe the best-practice approach for this category of problem. `Recommended solution` must be Codex's recommendation for solving the todo.

Leave `Answer` empty when Codex creates or refines a todo. The `Answer` section is user-owned.

## Working On Todos

Use repo knowledge to answer the user's questions and unblock the todo. When Codex has a proposed answer, write or refine it under `Recommended solution`, not under `Answer`.

Keep recommendations practical, implementation-aware, and scoped to the todo. If repo facts conflict, perform a deeper check of impacted files and related todos before recommending a change.

Frozen todos remain open. A todo is frozen only when its `Answer` section contains `FROZEN TODO` or `FROZEN`. Treat that marker as "not ready to answer", not as an answer.

## Finalizing Todos

Do not finalize a todo unless the user explicitly accepts the decision and the todo has a user-owned `Answer` value. Codex must not author new content inside `Answer` unless the user has explicitly said to do so.

When finalizing an accepted todo:

- Remove `Impact`, `Industry standard`, and `Recommended solution`.
- Preserve the accepted `Answer`.
- Move the completed todo decision into `PRISM-information.md`.
- Remove the completed block from the folder-local `jbtodo.md`.
- Delete the local `jbtodo.md` file if it has no open todos left.
- Add optional process notes to `AGENTFEEDBACK.md` only when they help future reloads or avoid repeated work.

Before finalizing, verify the accepted answer is complete, feasible, valid, and consistent with existing Prism knowledge. If it is incomplete or contradictory, leave the todo open and refine the todo or recommendation instead of moving it.
