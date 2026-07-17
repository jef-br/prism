---
name: ticket-finish
description: Mark a ticket as Done, move it to the archive, and commit
user-invocable: true
---
The user wants to mark the ticket identified by $ARGUMENTS as Done.

Steps:

1. **Find the ticket**: Read `AGENT-TICKETS.md` at the repo root. Find the `### T-XXXX · Title` block matching the keyword or T-number in $ARGUMENTS. Show the user the ticket title and its current Status line — confirm before changing anything.

2. **Review gate** (applies to `P1-feature-worker` and `P4-critical-architecture` tickets only; P0/P2/P3 skip this step): the ticket block must contain a `**Review:** Approve (YYYY-MM-DD)` line issued by the reviewer agent. If it is missing or says `Request Changes`, STOP — do not mark Done. Offer to spawn the reviewer agent on the ticket's diff; only after an Approve verdict is recorded on the ticket block may this skill proceed. This gate is what makes Done unreachable without review — do not waive it because the change "looks trivial".

3. **Implementation gate** — verify the work actually landed in git history (guards against the T-3500 incident of 2026-07-17, where a ticket was archived Done while its implementation existed only in an uncommitted agent worktree):
   - Find candidate implementation commits: `rtk git log --all --oneline --grep="T-XXXX"`.
   - Discard commits that only touch `AGENT-TICKETS.md`/`AGENT-TICKETS-ARCHIVE.md` (ticket-board bookkeeping is not implementation) — check with `rtk git show --stat <hash>`.
   - At least one remaining commit must be reachable from the current branch AND touch at least one file on the ticket's `**Files:**` line. The Files list is a superset of what may change — one genuine hit is enough; all-files-changed is not required.
   - If no commit references the T-number, fall back to `rtk git log --oneline -3 -- <path>` per named file and ask the user to identify which commit(s) carry this ticket's work.
   - If neither route produces a commit, STOP — do not mark Done. The implementation likely never landed (still sitting in a worktree, another branch, or nowhere). Report exactly what was searched.
   - Exception: a ticket closed as a pure decision with no file changes (e.g. "measured, not worth it" recorded on the ticket block itself) may pass this gate only if the user explicitly confirms that nothing was supposed to land.

4. **Mark as Done and archive**: Change the `**Status:**` value to `Done` and append the date: `**Status:** Done (YYYY-MM-DD)`. Then MOVE the entire ticket block (heading through its trailing `---` separator) out of `AGENT-TICKETS.md` and append it to `AGENT-TICKETS-ARCHIVE.md` at the repo root. If the archive file does not exist, create it with the header:

   ```
   # PRISM Agent Tickets — Archive

   Done tickets, moved here by /ticket-finish to keep AGENT-TICKETS.md (read every session start) lean.
   Newest at the top.
   ```

   Insert the archived ticket directly under that header (newest first). `AGENT-TICKETS.md` keeps open tickets only.

5. **Commit by pathspec** (other sessions may have unrelated changes in the worktree — never `git add .`):
   `rtk git add AGENT-TICKETS.md AGENT-TICKETS-ARCHIVE.md && rtk git commit -m "Close ticket: T-XXXX · Title"` (use the actual ticket ID and title).
