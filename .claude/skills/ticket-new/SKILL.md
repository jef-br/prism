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

   <What and why, in 2–4 lines. Reference the milestone (M5–M11) if the work belongs to one. Explicit
   scope boundary if there is any risk of scope creep.>

   **Problem(s):**
   - <one line per distinct problem>

   **Decision(s):** <omit this section entirely if there are none>
   - <one line per decision needed or made — mark made ones `(decided: <what>, <date>)`>

   **Test(s):** <omit if there are none yet>
   - <one line per test/measurement to run or already run — mark run ones `(run: <result>, <date>)`>

   **Reviewer(s):** <omit if not applicable>
   - <who/what reviews this before Done>

   **Files:** <known touch points, comma-separated>
   ```

   Keep the `### ` heading — /ticket-finish appends this body straight into the archive unchanged. No trailing `---` separator; the file boundary is the separator now.

   Defaults: Status `Ready`; Profile `P1-feature-worker` for implementation, `P3-scout` for read-only investigation, `P2-verifier` for run-and-report, `P4-critical-architecture` for cross-cutting contracts/pipeline changes (full table in the Runtime Profiles section of `jb/ticketboard/AGENT-TICKETS.md`). Use Status `Blocked` + `**Blocked-by:**` when gated on a milestone or another ticket.

   **No narrative prose outside these four lists.** A ticket's total step count is the sum of every
   bullet across all four sections — `#Problem(s) + #Decision(s) + #Test(s) + #Reviewer(s)`. Example:
   2 problems, 1 decision, 2 tests, 1 reviewer = 6 steps. Never restate that count as a fixed number in
   the intro prose (something like "two calibration gaps" goes stale the moment a bullet is added
   anywhere and nobody remembers to update the count) — the count is always just "add up the bullets."
   Findings and history belong to the bullet they explain, not a new paragraph — see "Updating an
   existing ticket" below.

3. **Add one row** to the `## Board` table in `jb/ticketboard/AGENT-TICKETS.md`, sorted ascending by T-number:

   `| [T-XXXX](T-XXXX.md) | <Status> | <short title> | <do this next> |`

   Keep the link inline in the row. A reader must be able to click the ticket ID straight from the table — do not move link targets into a reference-definition list below it.

   The "Do this next" cell is a single verb-first sentence saying the action, not the problem — the rules and a worked example/counter-example are in that file's "How to write the Do this next line" section. Follow them.

4. **Show the drafted ticket** to the user in your response. If scope, profile, or blocking status was guessed rather than stated, say so explicitly.

5. **Commit by pathspec** (other sessions may have unrelated changes in the worktree — never `git add .`):
   `rtk git add jb/ticketboard/T-XXXX.md jb/ticketboard/AGENT-TICKETS.md && rtk git commit -m "New ticket: T-XXXX · Title"` (use the actual ticket ID and title).

## Updating an existing ticket

This isn't just for creation — every session that later touches a ticket follows this too: edit the
matching bullet in place, don't append a new paragraph elsewhere in the file.

- A problem gets investigated → append the finding to *that same* `Problem(s)` bullet, or convert it
  into a `Decision(s)` bullet if the finding produces a choice to make.
- A decision gets made → edit that bullet to `(decided: <value>, <date>)` with a one-line reason. Don't
  leave the old "needs a decision" wording standing next to the new one.
- A test gets run → edit that bullet to `(run: <result>, <date>)`. If the result raises a *new* problem,
  add a new `Problem(s)` bullet — don't bury it inside the test bullet's own text.
- **Never mention the same problem/decision/test under two different headings or two different
  bullets.** If you're about to write a heading that already exists as a bullet elsewhere in the file,
  you're duplicating it — extend the existing bullet instead.
- Before writing anything, skim every existing bullet across all four lists once. The goal is that
  nobody ever has to re-read a ticket's full history to find "what's actually still open" — that list is
  exactly the `Problem(s)`/`Decision(s)` bullets that don't yet say `(decided...)` / aren't closed.
