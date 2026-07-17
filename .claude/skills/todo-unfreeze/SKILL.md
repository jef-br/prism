---
name: todo-unfreeze
description: Unfreeze a todo by clearing its FROZEN marker, making it active again
user-invocable: true
---
The user wants to unfreeze the todo identified by: $ARGUMENTS

Steps:

1. **Find the todo**: Search all `jbtodo.md` files under `jb/` for a `- [ ]` block whose title matches the keyword(s) in $ARGUMENTS AND whose `Answer:` field contains `FROZEN` or `FROZEN TODO`. Show the user the title you found before changing anything.

2. **Clear the frozen marker**: Set the `Answer:` field back to empty — change `Answer: FROZEN` or `Answer: FROZEN TODO` back to just `Answer:` with nothing after it.

3. **Save the file** and confirm: "Todo '<title>' is now active."

No commit needed.
