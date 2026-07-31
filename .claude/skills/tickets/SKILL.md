---
name: tickets
description: List all open tickets from the jb/ticketboard board, grouped by status
user-invocable: true
---
Read the `## Board` table in `jb/ticketboard/AGENT-TICKETS.md`. That table already holds every open ticket with its status and next action — do NOT open the individual `jb/ticketboard/T-XXXX.md` files, that is the whole point of the board.

Group and display tickets in this order:

**Active / In Review** — Status is Active or Review
**Ready** — Status is Ready
**Blocked** — Status is Blocked

For each ticket, show one line:
`T-XXXX · Title [Status] — <do this next, from the board's last column>`

Done tickets live in `jb/ticketboard/AGENT-TICKETS-ARCHIVE.md` (moved there by /ticket-finish) — do not read the archive unless the user asks about done/closed tickets.

End with a summary line:
`X active/review, Y ready, Z blocked · done tickets: jb/ticketboard/AGENT-TICKETS-ARCHIVE.md`

If the board does not exist or has no rows, say so in one line.
