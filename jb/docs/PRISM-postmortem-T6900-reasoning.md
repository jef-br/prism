# Post-mortem: how T-5200/T-6900 spent three sessions on a bug that did not exist

**Date:** 2026-08-11. **Subject:** `X SMASHEDLEMON45` (1774 images) was believed to hang in matching
and to match poorly. Neither was true. It completes in ~36 minutes and matches at its arithmetic
ceiling (1725/1774 correct, **zero wrong**). Two tickets, one reverted timeout feature, one reverted
instrumentation pass, and one shipped-but-unrelated algorithm rewrite were spent getting there.

This file is about **the reasoning**, not the code. The technical facts live in [[T-6900]] and [[T-6910]].

---

## The five errors, in the order they compounded

### 1. A timeout was read as evidence about the program
The very first observation was: *the job did not finish in 45 minutes, therefore it hangs.* A client
timeout is a statement about **the client's patience**, not about the server's behaviour. It is
equally consistent with "infinite loop" and "needs 46 minutes."

Nothing distinguished those two until someone measured the actual work. The job needed ~36 minutes;
later runs used **8- and 10-minute** timeouts, and each fresh timeout was logged as a fresh
confirmation of the hang. It was a fresh confirmation of the timeout.

> **Rule.** A timeout is a null result. Before calling anything a hang, get one number: how much work
> is there, and how fast does the machine do that kind of work? Here it was two measurements —
> 293 ms/megapixel and 13,130 megapixels — and it took about ten minutes to obtain.

### 2. "Sustained CPU" was treated as a symptom when it is the normal state of working software
[[T-5200]] recorded ~3 cores of sustained CPU plus GC churn as the hang's "live signature." Busy CPU
is what computation looks like. It cannot discriminate between useful work and a spinning loop —
**both** peg cores.

The 3-core figure was even quantitatively meaningful in the other direction: it is exactly the
effective parallelism of the chunked analysis loop. The number that was filed as evidence of
pathology was a measurement of the system working as designed.

> **Rule.** "It is using CPU" is not evidence of a defect. The discriminating question is *whether the
> amount of CPU is proportional to the amount of work* — which requires knowing the work, which
> requires measuring it.

### 3. Process of elimination ran over an assumed-complete list
[[T-5200]] reasoned: Brackets 1, 2, 2-Intersect, FilenameToCell and SubstringRescue are all O(n) and
cannot take minutes; `FindLooseRelation` is the only O(unmatched × matched) pass; therefore it is the
culprit. Each step is correct. The conclusion is still wrong, because the candidate list contained
only the matching waterfall — and the real cost was in the **pre-match** pipeline, which was never on
the list.

Elimination proves "the cause is the last item standing" only if the list is exhaustive. An unstated
scope boundary silently redefines "everything" as "everything I looked at."

> **Rule.** Before eliminating, state where the boundary of the search is and why the cause must be
> inside it. If that justification cannot be written down, the elimination is unsound.

### 4. A plausible mechanism was promoted to a diagnosis without a measurement connecting it
`SiblingPropagator`'s unindexed O(unmatched × matched) scan is a real inefficiency, and the fix built
for it was genuine, tested work that still stands. But *being a real problem* and *being this
problem* are different claims. The scan was never shown to run at all on this dataset.

It does not. The waterfall never reaches `SiblingPropagator` here — Brackets 1 and 2 resolve 1732 of
1774 images, so the unmatched pool that reaches it is 42 images, and the "O(n²) blow-up" is 42 × 1732.

The same pattern repeated at the next level down. `BuildProfile` discarding 1-3 digit tokens
(`\d{1,3}`) was hand-traced, found real, and carried into [[T-6900]] as the leading explanation for
poor matching on `26000_775-725_1_B2C.jpg`. Also a real quirk. Also irrelevant — that code never runs
on these images, and Bracket 2's tokenized concatenation already consumes those exact 3-digit tokens.

> **Rule.** A mechanism that *could* explain the symptom is a hypothesis. It becomes a diagnosis only
> when you show it executes on the failing input, with the magnitude required. "Is this code even
> reached?" is one instrumented run, and it outranks any amount of reading.

### 5. The controls that should have falsified the theory were built too small to do so
Two 5-image control jobs were run — one real slice, one synthetic and deliberately 100% unmatchable.
Both completed in under 25 seconds, and this was recorded as "the hang is volume-dependent."

That reading takes the theory as given. The other reading was available from the same data: **cost
scales with volume because cost is per-image work.** 5 images × ~2.6 s ≈ 13 s, which is what was
observed. The control did not distinguish the hypotheses because both predict the same result at n=5
— and no one asked what each hypothesis predicted before running it.

> **Rule.** A control is only informative if the competing hypotheses predict *different* outcomes.
> Write down both predictions before running it. If they agree, the experiment cannot inform you, and
> the effort should go elsewhere.

---

## The meta-error: the premise was inherited, never re-derived

Each session started from the prior session's conclusion, and the conclusion hardened as it was
restated. By [[T-6900]] the framing was "the bottleneck is upstream **in `MatchingService.cs`**,
between ingest and `ImageMatcher.Run`" — correct, but expressed with a confidence that the evidence
(zero log lines, plus elimination) did not support, and it named two suspects, one of which
(`FindDuplicates`) is provably trivial and could have been cleared by reading 30 lines.

Meanwhile the claim that mattered most — *does this dataset actually match badly?* — was **never
measured at all** across three sessions. It was inferred from the hang, which was inferred from a
timeout. When finally measured it was 97.2% correct with zero wrong matches, i.e. there was no
matching problem to solve. The entire investigation was downstream of a number nobody had.

> **Rule.** Re-derive the headline claim from raw data at the start of each session, especially when
> it arrives as settled. Cheap re-measurement beats inherited certainty. (This repo already had this
> rule — "re-measure before designing" — and it was not applied because the premise did not look like
> a measurement, it looked like a fact.)

---

## What actually broke the deadlock

One harness, ~90 lines, that runs the **real** `ModelBuilder` and the **real** `ImageMatcher` over the
1774 filenames with no image decoding. **0.9 seconds** per run versus 36 minutes end-to-end. It
answered "does matching work?" (yes), "which bracket does the work?" (1 and 2), and "what is the
ceiling?" (97.2%) — in one run, and predicted every figure the live pipeline later produced exactly.

The generalisable move: **find the part of the pipeline that actually decides the question, and run
only that.** Matching reads filenames and Excel cells; it never reads pixels. All 36 minutes were
pixel work irrelevant to the question being asked. The investigation had been paying full pipeline
cost for a filename-only answer, which is also why iteration was slow enough that guessing felt
cheaper than measuring — the loop that makes error #4 attractive.

## Checklist for the next "it hangs" report

1. **How much work is there, and how fast is that kind of work?** Two numbers, before any theory.
2. **Is the suspect code reached at all?** One instrumented run beats any reading.
3. **What is the search boundary, and why must the cause be inside it?** Write it down.
4. **What does each hypothesis predict for this control?** If the predictions agree, do not run it.
5. **Has the headline claim itself been measured, or only inherited?** Measure it first.
6. **Can the deciding question be answered without the expensive part?** Usually yes, and usually 1000× faster.

See also: [[T-6900]] (root cause + matching measurement), [[T-6910]] (the one real defect this
uncovered: full-resolution analysis running twice, second pass single-threaded).
