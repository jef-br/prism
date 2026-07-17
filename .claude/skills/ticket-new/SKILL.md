---
name: ticket-new
description: Create a new ticket in AGENT-TICKETS.md with the next free T-number and the house format
user-invocable: true
---
Create a new ticket for: $ARGUMENTS

Steps:

1. **Pick the T-number**: Read `AGENT-TICKETS.md` AND `AGENT-TICKETS-ARCHIVE.md` fresh (other sessions add tickets mid-session — never assume you know the highest number). Find the highest existing `T-XXXX` across both files.
   - Standalone ticket → next free hundred above the highest (highest T-4100 → new T-4200).
   - Follow-up to an existing ticket T-XXXX → T-XXXX+10 (the T-2820/T-2830 pattern from T-2800), skipping to the next free +10 if taken.

2. **Draft the block** in the house format (see existing tickets for reference):

   ```
   ### T-XXXX · <Short imperative title>
   **Status:** Ready | **Profile:** <profile>
   **Blocked-by:** <ticket or milestone gate — only when Status is Blocked>
   **Found by:** [[T-YYYY]] <origin investigation — optional>

   <What and why, in 2–6 lines. Concrete acceptance criteria. Explicit scope boundary if there is any risk of scope creep. Reference the milestone (M5–M11) if the work belongs to one.>

   **Files:** <known touch points, comma-separated>

   ---
   ```

   Defaults: Status `Ready`; Profile `P1-feature-worker` for implementation, `P3-scout` for read-only investigation, `P2-verifier` for run-and-report, `P4-critical-architecture` for cross-cutting contracts/pipeline changes (full table in the Runtime Profiles section of AGENT-TICKETS.md). Use Status `Blocked` + `**Blocked-by:**` when gated on a milestone or another ticket.

3. **Insert** the block under `## Tickets` in `AGENT-TICKETS.md`, keeping tickets sorted ascending by T-number, each terminated by its `---` separator.

4. **Show the drafted ticket** to the user in your response. If scope, profile, or blocking status was guessed rather than stated, say so explicitly.

5. **Commit by pathspec** (other sessions may have unrelated changes in the worktree — never `git add .`):
   `rtk git add AGENT-TICKETS.md && rtk git commit -m "New ticket: T-XXXX · Title"` (use the actual ticket ID and title).
