---
name: todos
description: List all open todos across all jbtodo.md files
user-invocable: true
---
Find all `jbtodo.md` files under `jb/` in this repo. Read each file and extract every `- [ ]` checkbox item.

For each block, check whether its `Answer:` field contains the word `FROZEN` or `FROZEN TODO`. If it does, the item is frozen.

Output all items grouped by file path (bold header). Under each file list every open `- [ ]` item by its title line (first line of the block only). Append `[FROZEN]` after the title for frozen items. Do not skip frozen items — show them inline with active ones.

Omit files that have no open items at all.

At the end, print a one-line summary:
`X total (Y frozen) across Z files`

If there are no open todos anywhere, say so in one line.
