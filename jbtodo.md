



-----

# T-3300 independent branch review — findings (raised 2026-07-16)

Independent reviewer pass over the whole `t3300-distributed-seam` branch (distributed-services seam).
Verdict was "request changes". Each finding below states its cause, its effect, and its consequence.
Status per finding is kept current.

## R1 · A hung remote Upscale host can freeze a Transform job forever

**Answer:** FIXED on branch t3300-distributed-seam (review-fixes commit)

- **Cause:** The branch gave every service HTTP client an infinite transport timeout (on purpose — a
  stage legitimately runs for many minutes, and the old 100-second default killed healthy runs). The
  safety story became "the job's cancellation token stops a stuck call instead". But one call path never
  received that token: `ImagePreProcessor.cs` calls `remoteUpscale.UpscaleAsync(..., CancellationToken.None)`,
  and neither `Preprocess` nor the `Parallel.ForEach` in `TransformService` passes the job's token down.
- **Effect:** If the remote Upscale host stops responding mid-request (crash, network drop, GPU wedge),
  the Transform worker thread waits forever. Nothing can interrupt it — not the job timeout, not job
  cancellation.
- **Consequence:** One dead Upscale host permanently eats a Transform job and its worker threads. Before
  this branch the hang was at least capped at 100 seconds; now it is unbounded. This is a regression on
  exactly the failure the distributed seam exists to survive.

## R2 · The new HTTP failure paths have zero test coverage

**Answer:** FIXED on branch t3300-distributed-seam (review-fixes commit)

- **Cause:** All 14 new ServiceHost roundtrip tests exercise the happy path only. No test makes a client
  face a dead host, an error status, or a cancelled token.
- **Effect:** The behavior of each `Http*Service` client under failure (what exception, how fast, does
  cancellation work) is unverified. R1 survived precisely because no test ever cancelled a remote call.
- **Consequence:** Future changes to the clients or their timeout/cancellation behavior can silently
  break distributed error handling; the suite would stay green while a production job hangs or dies with
  a misleading error.

## R3 · CLAUDE.md still says the solution has 8 projects

**Answer:** FIXED on branch t3300-distributed-seam (review-fixes commit)

- **Cause:** The test split added 5 projects to `PRISM.sln` (4 per-service test projects +
  `Prism.Tests.Shared`), and the CLAUDE.md Tests section was updated — but its Architecture section
  still lists "8 projects".
- **Effect:** Every future session reads CLAUDE.md at start and learns a wrong solution layout.
- **Consequence:** Agents reason from a stale map — e.g. they won't know `Prism.Tests.Shared` exists and
  may re-create shared fixtures or miss the per-service test projects entirely.

## R4 · All five test projects write their CI results to the same file name

**Answer:** FIXED on branch t3300-distributed-seam (review-fixes commit)

- **Cause:** `ci.yml` now runs `dotnet test` on the whole solution but still passes one fixed logger
  file name (`core-tests.trx`) for every project.
- **Effect:** Five projects race to write the same .trx file in the same results folder; later writers
  overwrite earlier ones.
- **Consequence:** When CI fails, the uploaded test-results artifact may only contain one project's
  results — the one that happens to have written last — making failures in the other four projects
  invisible in the artifact even though the build correctly went red.

## R5 · Stale service host ports fail slow instead of loud (non-blocking)

**Answer:** FIXED on branch t3300-distributed-seam (review-fixes commit) — all five ports pre-checked

- **Cause:** `Invoke-CiPipelineDistributed.ps1` pre-checks only the API port (5100) for a leftover
  occupant. The four service ports (5101–5104) are not pre-checked.
- **Effect:** A leftover process on a service port makes the new host fail to bind; the script only
  notices after its 240-second health-wait times out.
- **Consequence:** A stale process turns a 2-second "port busy, stop it first" failure into a 4-minute
  wait with a less obvious error — annoying on the runner, worse when debugging locally.

## R6 · AGENTFEEDBACK.md still carries the old T-3600 service list (non-blocking)

**Answer:** FIXED on branch t3300-distributed-seam (review-fixes commit)

- **Cause:** The branch corrected the docs (Matching may run as its own co-located host), but
  AGENTFEEDBACK.md's T-3600 note still says only Transform/Generate/Upscale may split.
- **Effect:** The reload memory contradicts the doc it summarizes.
- **Consequence:** A future session trusting AGENTFEEDBACK.md may "fix" correct code or docs back to the
  outdated model.

## R7 · Remote upscale call blocks worker threads (sync-over-async, non-blocking)

**Answer:** OPEN — deferred until after R1

- **Cause:** Inside Transform's parallel loop, the remote upscale HTTP call is awaited synchronously
  (`GetAwaiter().GetResult()`); the loop is sync so a truly async call needs a bigger refactor
  (`Parallel.ForEachAsync`).
- **Effect:** No deadlock (verified — no synchronization context there), but up to processor-count
  thread-pool threads sit blocked on network I/O while remote upscales run.
- **Consequence:** Under many concurrent jobs, blocked threads reduce throughput for everything else on
  the thread pool. Correctness is unaffected; this is a scalability cost. Natural moment to fix: when R1
  threads the cancellation token through the same chain.

## R8 · ServiceHost GPU roundtrip tests flaked on the test client's 100-second timeout

**Answer:** ROOT CAUSE FOUND, FIXED — same bug as R1's production finding, in the test harness

- **Cause:** `WebApplicationFactory.CreateClient()` returns an HttpClient with the default 100-second
  timeout. Inside `Prism.Core.Tests`, the ServiceHost collection runs in parallel with
  `PipelineIntegrationTests`, whose pipeline work holds the shared Real-ESRGAN session lock. The
  remote-upscale routing test then queues behind it; when its roundtrip crossed 100 seconds, the test's
  own client aborted the in-flight request (trx pinned it: `RemoteUpscaleRoutingTests`, 2m25s).
- **Effect:** Intermittent single-test failures with TestHost abort stacks ("Error while copying content
  to a stream", `ResponseBodyReaderStream.CheckAborted`) in full-suite runs only — standalone runs
  finish under 100s and stay green. Three occurrences across three full runs; identical mechanism to the
  production defect the review found (R1's infinite-timeout rationale), just in the test client.
- **Consequence / fix:** The fixture's `Client` now sets `Timeout = Timeout.InfiniteTimeSpan`, mirroring
  the production `ServiceHttp.CreateClient`. `-m:1` stays in `ci.yml` anyway: serialized projects give
  deterministic timing and per-project trx that already proved its worth pinning this. If any abort
  recurs after this fix, log it here — it would be a genuinely new problem.
