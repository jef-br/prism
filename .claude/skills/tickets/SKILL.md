---
name: tickets
description: List all tickets in AGENT-TICKETS.md grouped by status
user-invocable: true
---
Read `AGENT-TICKETS.md` at the repo root. Extract every ticket (each starts with `### T-XXXX · Title` and has a `**Status:**` line).

Group and display tickets in this order:

**Active / In Review** — Status is Active or Review
**Ready** — Status is Ready
**Blocked** — Status is Blocked; include the `Blocked-by:` reason in parentheses

For each ticket in these groups, show one line:
`T-XXXX · Title [Status]`

Done tickets live in `AGENT-TICKETS-ARCHIVE.md` (moved there by /ticket-finish) — do not read the archive unless the user asks about done/closed tickets.

End with a summary line:
`X active/review, Y ready, Z blocked · done tickets: AGENT-TICKETS-ARCHIVE.md`

If AGENT-TICKETS.md does not exist or has no tickets, say so in one line.
