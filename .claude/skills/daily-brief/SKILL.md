---
name: daily-brief
description: Regenerate daily-brief.md from current repo state, commit as "Daily brief - dd/mm/yy", push to main
user-invocable: true
---
Regenerate the daily brief. This normally runs unattended on a schedule — never ask questions; make the best call and flag assumptions inside the brief itself.

Steps:

1. **Sync first**: `rtk pull --rebase origin main` — concurrent sessions push to main; always brief from the tip. If the pull fails (conflict), skip the brief and report the failure instead of briefing from stale state.

2. **Capture the previous brief** before overwriting (`git show HEAD:daily-brief.md`) so the Changed section reports real deltas, not a re-listing of standing facts.

3. **Gather state**:
   - `jb/ticketboard/AGENT-TICKETS.md` — open tickets and status changes
   - `rtk git log --oneline <last-brief-commit>..HEAD` — everything landed since the previous "Daily brief - " commit
   - All open `jbtodo.md` blocks (the /todos skill's source data)
   - `jb/ticketboard/AGENTFEEDBACK.md` if changed since the last brief

4. **Overwrite `daily-brief.md`** at the repo root:
   - Heading is always exactly `# Daily Brief` — never add a date to it.
   - House sections: `##### Changed`, `##### Todo updates`, `##### Next steps`.
   - Changed = what actually moved since the last brief (commits, restructures, ticket transitions), with commit/file anchors.
   - Todo updates = which open decisions moved, which are unimprovable without user input and why.
   - Next steps = short, concrete, ordered by leverage; things the user should run or decide, not vague intentions.

5. **Commit and push directly to main** — no feature branch, no approval needed. Stage ONLY the brief (never `git add .`):
   `rtk git add daily-brief.md && rtk git commit -m "Daily brief - dd/mm/yy" && rtk git push origin main`
   Date format is exactly `dd/mm/yy` (11 July 2026 → `Daily brief - 11/07/26`).
