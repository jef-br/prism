---
name: ticket-finish
description: Mark a ticket as Done, move it to the archive, and commit
user-invocable: true
---
The user wants to mark the ticket identified by $ARGUMENTS as Done.

The board lives in `jb/ticketboard/`: `AGENT-TICKETS.md` is a table of contents, ticket bodies are in
`jb/ticketboard/T-XXXX.md`, done tickets are in `jb/ticketboard/AGENT-TICKETS-ARCHIVE.md`.

Steps:

1. **Find the ticket**: read the `## Board` table in `jb/ticketboard/AGENT-TICKETS.md` to resolve the keyword or T-number in $ARGUMENTS to a T-number, then read `jb/ticketboard/T-XXXX.md`. Show the user the ticket title and its current Status line — confirm before changing anything.

2. **Review gate** (applies to `P1-feature-worker` and `P4-critical-architecture` tickets only; P0/P2/P3 skip this step): the ticket file must contain a `**Review:** Approve (YYYY-MM-DD)` line issued by the reviewer agent. If it is missing or says `Request Changes`, STOP — do not mark Done. Offer to spawn the reviewer agent on the ticket's diff; only after an Approve verdict is recorded in the ticket file may this skill proceed. This gate is what makes Done unreachable without review — do not waive it because the change "looks trivial".

3. **Implementation gate** — verify the work actually landed in git history (guards against the T-3500 incident of 2026-07-17, where a ticket was archived Done while its implementation existed only in an uncommitted agent worktree):
   - Find candidate implementation commits: `rtk git log --all --oneline --grep="T-XXXX"`.
   - Discard commits that only touch files under `jb/ticketboard/` (ticket-board bookkeeping is not implementation) — check with `rtk git show --stat <hash>`.
   - At least one remaining commit must be reachable from the current branch AND touch at least one file on the ticket's `**Files:**` line. The Files list is a superset of what may change — one genuine hit is enough; all-files-changed is not required.
   - If no commit references the T-number, fall back to `rtk git log --oneline -3 -- <path>` per named file and ask the user to identify which commit(s) carry this ticket's work.
   - If neither route produces a commit, STOP — do not mark Done. The implementation likely never landed (still sitting in a worktree, another branch, or nowhere). Report exactly what was searched.
   - Exception: a ticket closed as a pure decision with no file changes (e.g. "measured, not worth it" recorded in the ticket file itself) may pass this gate only if the user explicitly confirms that nothing was supposed to land.

4. **Archive**: in `jb/ticketboard/T-XXXX.md`, change the `**Status:**` value to `Done` and append the date: `**Status:** Done (YYYY-MM-DD)`. Then append the whole file body — starting at its `### T-XXXX · Title` heading — directly under the header of `jb/ticketboard/AGENT-TICKETS-ARCHIVE.md` (newest first), followed by a `---` separator line. Then **delete `jb/ticketboard/T-XXXX.md`** (`rtk git rm`).

5. **Remove the board row**: delete the ticket's row from the `## Board` table in `jb/ticketboard/AGENT-TICKETS.md`, and its `[T-XXXX]: T-XXXX.md` link definition from the list below the table. The board holds open tickets only.

6. **Commit by pathspec** (other sessions may have unrelated changes in the worktree — never `git add .`):
   `rtk git add jb/ticketboard/AGENT-TICKETS.md jb/ticketboard/AGENT-TICKETS-ARCHIVE.md jb/ticketboard/T-XXXX.md && rtk git commit -m "Close ticket: T-XXXX · Title"` (use the actual ticket ID and title).
