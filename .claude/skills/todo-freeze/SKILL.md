---
name: todo-freeze
description: Freeze a todo by setting Answer: FROZEN to defer it without losing it
user-invocable: true
---
The user wants to freeze the todo identified by: $ARGUMENTS

A frozen todo stays open and stays in the list — it is marked "not ready to answer right now" by placing FROZEN in its Answer field.

Steps:

1. **Find the todo**: Search all `jbtodo.md` files under `jb/` for a `- [ ]` block whose title matches the keyword(s) in $ARGUMENTS. Show the user the title you found and confirm before changing anything.

2. **Mark as frozen**: In that block, set the `Answer:` field to `FROZEN`. If the Answer line currently reads `Answer:` with nothing after it, change it to `Answer: FROZEN`. If it already has content, replace just the value with `FROZEN`.

3. **Save the file** and show the user the title of the frozen todo.

No commit is needed — a freeze is a lightweight "not now" marker, not a permanent decision.

To unfreeze later, use `/todo-unfreeze <keyword>` or `/todo-thaw <keyword>`.
