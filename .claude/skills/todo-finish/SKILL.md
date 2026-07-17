---
name: todo-finish
description: Close a todo — runs acceptance gate, writes to jb/docs/, removes block, commits
user-invocable: true
---
The user wants to close the todo identified by: $ARGUMENTS

Follow these steps exactly — do NOT skip the acceptance gate.

---

**Step 1 — Find and confirm the todo**

Ask the user what todo he wants to target if $ARGUMENTS is empty.
if $ARGUMENTS is not empty, Search all `jbtodo.md` files under `jb/` for a `- [ ]` block whose title matches the keyword(s) in $ARGUMENTS. Show the user the full block and ask them to confirm it is the right one before continuing.

---

**Step 2 — Get the answer**

Check the `Answer:` field in the block.
- If `Answer:` is blank or says `FROZEN`, ask the user: "What decision should be recorded for this todo?" Wait for their answer before continuing.
- If `Answer:` has content, use that as the decision.

---

**Step 3 — Acceptance gate (do this before touching any file)**

Before closing, verify ALL four criteria are met. If any fail, stop and explain — do not close the todo.

1. **Pertains to the question** — does the answer actually address what the todo asked? A good answer to the wrong question is not acceptable.
2. **Complete, no gaps** — no "TBD", no deferred sub-decisions, no vague placeholders. Every part of the todo's question has a concrete answer.
3. **No contradictions** — cross-check the answer against existing docs in `jb/docs/`, the codebase, and any related todos. If anything conflicts, surface the conflict and stop.
4. **Implementation can proceed without assumptions** — a developer reading only the answer and the docs could implement it without needing to invent anything.

If a criterion fails: explain what is missing or contradictory, then stop.

---

**Step 4 — Write to docs**

Find the appropriate file in `jb/docs/` (read `jb/docs/PRISM-index.md` first to choose the right one). Append a concise close-out entry: the todo title, the accepted decision, and any implementation notes. Follow the style of existing entries in that file. Update `jb/docs/PRISM-index.md` if the decision creates a new documentation surface.

---

**Step 5 — Remove from jbtodo.md**

Delete the entire block — from the `- [ ]` line through its closing `-------` separator (inclusive). If the block is at the end of the file with no closing `-------`, remove it through end of file.

---

**Step 6 — Delete if empty**

After removal, if the `jbtodo.md` file has no remaining `- [ ]` items, delete the file entirely.

---

**Step 7 — Commit**

Stage the modified/deleted files and commit:
`Close todo: <first line of the todo title>`
