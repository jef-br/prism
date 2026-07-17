---
name: prism-claude-monitored-test
description: Start PRISM (core+API+web) if needed, monitor all logs, and report live on job activity
user-invocable: true
---
Stand up the full PRISM stack for hands-on testing, watch it, and narrate what happens while the user
drives the workbench from their browser. Core runs in-process inside the API, so this is two
processes: the API (Kestrel, :5000) and the Next.js web workbench (:3000).

Work the three phases in order. Use the repo root as the working directory.

## Phase 1 — Start if not yet started (idempotent)

Detect what is already running before launching anything:
- API up? `curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/PRISM/health` → `200` = already up, skip.
- Web up? `curl -s -o /dev/null -w "%{http_code}" http://localhost:3000/` → `200` = already up, skip.

Start the API only if down (Bash tool, `run_in_background: true`):
```
export ASPNETCORE_URLS="http://localhost:5000" && export ASPNETCORE_ENVIRONMENT="Development" && dotnet run --project jb/src/api/Prism.Api.csproj > /tmp/prism-api.log 2>&1
```
Then wait for readiness with a single-notification background Bash command (`run_in_background: true`):
```
until grep -qE "Now listening on|Application started|error CS|PrismConfigurationException" /tmp/prism-api.log; do sleep 1; done; tail -8 /tmp/prism-api.log
```

Start the web workbench only if down (Bash tool, `run_in_background: true`):
```
cd jb/src/workbench/web && npm run dev > /tmp/prism-web.log 2>&1
```
Readiness wait (`run_in_background: true`):
```
until grep -qE "Ready in|Error|EADDRINUSE" /tmp/prism-web.log; do sleep 1; done; tail -8 /tmp/prism-web.log
```

Verify both, then report the URLs to the user:
- `curl -s http://localhost:5000/PRISM/health` — expect `ConfigReady:true` and `RequiredModelAssetsReady:true`.
- `curl -s -o /dev/null -w "%{http_code}" http://localhost:3000/` — expect `200`.
- Tell the user: **web workbench → http://localhost:3000** (start here), **API → http://localhost:5000**.

If the API build fails because of a file lock on the WPF project, ignore it — only the API project is
needed; it builds independently. If `:3000` returns non-200, check `/tmp/prism-web.log` for a missing
`node_modules` (run `npm install` in the web dir) or a port clash.

## Phase 2 — Monitor everything (persistent)

Arm one persistent `Monitor` over both logs, filtered to job lifecycle + errors and excluding Next.js
HMR recompile noise:
```
tail -n0 -F /tmp/prism-api.log /tmp/prism-web.log 2>&1 | grep -E --line-buffered "PRISM/process|/PRISM/jobs/|Running|Completed|job reached|Failed|KO_|Exception|Unhandled|fail:|warn:|error CS|EADDRINUSE|⨯|Error:" | grep -v --line-buffered "Compiled"
```
Use `persistent: true`, `timeout_ms: 3600000`, and a specific description like "PRISM job lifecycle +
errors". If the monitor gets auto-stopped for being too chatty, re-arm with a tighter filter.

## Phase 3 — Report live on what happens

As monitor events arrive, translate raw log lines into plain language for the user. The user drives
interaction from the browser; you narrate and correlate against the logs:
- **Job accepted** — `POST /PRISM/process - 202` → report the JobID.
- **413 on POST** — request body exceeded the limit; the configured ceiling is `Input.MAXIMUM_REQUEST_SIZE`.
- **Progress** — `GET /jobs/{id}/progress` opens (SSE); when it finishes with `200 text/event-stream`
  the job reached a terminal state.
- **Result fetched** — `GET /jobs/{id}/result - 200` with `application/zip` (or JSON) means the
  workbench loaded the result; report format + byte size.
- **Trouble** — any `Failed`, `Exception`, `KO_`, `fail:` line → surface it immediately with the cause.

On completion, optionally summarize the manifest:
- ZIP: `curl -s http://localhost:5000/PRISM/jobs/{id}/result -o /tmp/prism-result.zip`, then
  `unzip -l /tmp/prism-result.zip` for entries and `unzip -p /tmp/prism-result.zip manifest.json`
  parsed as **utf-8-sig** (the manifest has a BOM) for `Summary` + per-image rows.
- JSON: parse the envelope's `Manifest.Summary` and `ImageRows`.

## Facts (known gotchas — bake these into your narration)

- API = `:5000`, web = `:3000`. Core runs **in-process inside the API** — there is no separate core
  process. `.env.local` already points the web app at `:5000`; CORS allows `:3000`.
- The browser address bar can only GET API endpoints on **:5000** (`/PRISM/health`, `/PRISM/config`,
  `/PRISM/jobs/{id}/result`). Hitting those paths on `:3000` 404s — that's the web origin, not the API.
- Job results are **RAM-only**, retained per `JobRetentionPeriodInHours` (default 24h) and lost on API
  restart. There is no on-disk persistence.
- `MaxConcurrentJobs = 1` — a second job queues rather than running in parallel.
- Process roles: `run_in_background` for the two servers; `until grep` background Bash for one-shot
  readiness; `Monitor` (persistent) for the ongoing watch.

## Optional argument

If invoked with a dataset hint (e.g. `test/datasets/TinyTest`), name it as the suggested batch for the user to
submit; otherwise just stand up + watch. Lightest known-good batch: `test/datasets/TinyTest` +
`jb\test/datasets/TinyTest\tiny-test.xlsx` (7 images + 1 zip containing 3 images, ~70 MB total).

## Teardown (when the user says stop)

- `TaskStop` the monitor task.
- Stop the web dev background task (`TaskStop` its task id).
- Kill the API by PID on :5000: `netstat -ano | grep ":5000 " | grep LISTENING` → `taskkill //PID <pid> //F`.
- Confirm both ports are free.
