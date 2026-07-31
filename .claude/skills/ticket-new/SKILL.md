---
name: ticket-new
description: Create a new ticket file in jb/ticketboard/ and add its row to the board
user-invocable: true
---
Create a new ticket for: $ARGUMENTS

The board lives in `jb/ticketboard/`. `AGENT-TICKETS.md` there is a table of contents only — ticket
bodies live in `jb/ticketboard/T-XXXX.md`, one file per ticket.

Steps:

1. **Pick the T-number**: list `jb/ticketboard/T-*.md` and read `jb/ticketboard/AGENT-TICKETS-ARCHIVE.md` fresh (other sessions add tickets mid-session — never assume you know the highest number). Find the highest existing `T-XXXX` across both.
   - Standalone ticket → next free hundred above the highest (highest T-4100 → new T-4200).
   - Follow-up to an existing ticket T-XXXX → T-XXXX+10 (the T-2820/T-2830 pattern from T-2800), skipping to the next free +10 if taken.

2. **Write `jb/ticketboard/T-XXXX.md`** in the house format:

   ```
   ### T-XXXX · <Short imperative title>
   **Status:** Ready | **Profile:** <profile>
   **Blocked-by:** <ticket or milestone gate — only when Status is Blocked>
   **Found by:** [[T-YYYY]] <origin investigation — optional>

   <What and why, in 2–6 lines. Concrete acceptance criteria. Explicit scope boundary if there is any risk of scope creep. Reference the milestone (M5–M11) if the work belongs to one.>

   **Files:** <known touch points, comma-separated>
   ```

   Keep the `### ` heading — /ticket-finish appends this body straight into the archive unchanged. No trailing `---` separator; the file boundary is the separator now.

   Defaults: Status `Ready`; Profile `P1-feature-worker` for implementation, `P3-scout` for read-only investigation, `P2-verifier` for run-and-report, `P4-critical-architecture` for cross-cutting contracts/pipeline changes (full table in the Runtime Profiles section of `jb/ticketboard/AGENT-TICKETS.md`). Use Status `Blocked` + `**Blocked-by:**` when gated on a milestone or another ticket.

3. **Add one row** to the `## Board` table in `jb/ticketboard/AGENT-TICKETS.md`, sorted ascending by T-number:

   `| [T-XXXX](T-XXXX.md) | <Status> | <short title> | <do this next> |`

   Keep the link inline in the row. A reader must be able to click the ticket ID straight from the table — do not move link targets into a reference-definition list below it.

   The "Do this next" cell is a single verb-first sentence saying the action, not the problem — the rules and a worked example/counter-example are in that file's "How to write the Do this next line" section. Follow them.

4. **Show the drafted ticket** to the user in your response. If scope, profile, or blocking status was guessed rather than stated, say so explicitly.

5. **Commit by pathspec** (other sessions may have unrelated changes in the worktree — never `git add .`):
   `rtk git add jb/ticketboard/T-XXXX.md jb/ticketboard/AGENT-TICKETS.md && rtk git commit -m "New ticket: T-XXXX · Title"` (use the actual ticket ID and title).
