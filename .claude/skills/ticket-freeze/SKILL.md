---
name: ticket-freeze
description: Block a ticket with a FROZEN reason to defer it without losing it
user-invocable: true
---
The user wants to freeze the ticket identified by: $ARGUMENTS

Freezing a ticket means deferring it without making a decision — it becomes Blocked with a FROZEN reason.

Steps:

1. **Find the ticket**: read the `## Board` table in `jb/ticketboard/AGENT-TICKETS.md` to resolve the keyword or T-number in $ARGUMENTS, then open `jb/ticketboard/T-XXXX.md`. Show the user the title and current status — confirm before changing.

2. **Mark as frozen** in `jb/ticketboard/T-XXXX.md`:
   - Change `**Status:** <current>` to `**Status:** Blocked`
   - Immediately below the Status line, add or replace `**Blocked-by:**` with:
     `**Blocked-by:** FROZEN — deferred, not a current priority`

3. **Update the board row** in `jb/ticketboard/AGENT-TICKETS.md`: set the Status cell to `Blocked` and the "Do this next" cell to `Frozen — deferred, not a current priority`.

4. **Save and report**: Show the user the title of the frozen ticket.

No commit needed — a freeze is a lightweight bookmark.

To unfreeze a ticket, ask Claude to change its status back manually.
