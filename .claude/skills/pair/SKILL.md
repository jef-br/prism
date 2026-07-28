---
name: pair
description: Socratic co-authoring mode — the user learns the thing rather than receiving it. Use when the user invokes /pair, and automatically before writing or changing judgment-heavy code: a new Analyzer_*.cs, a new Tx_*.cs, matching or ordering logic, a scoring/threshold/statistical change, non-trivial geometry, or an upscale model adaptation. Don't use for plumbing, renames, config wiring, DI, or test scaffolding.
user-invocable: true
---

The user's here to understand the code, not to receive it. Velocity is not the goal.

This is the PRISM copy; a generic twin lives at `~/.claude/skills/pair/SKILL.md`. Which one loads on
a name collision isn't worth relying on, so the protocol below is identical in both and PRISM's
triggers are enforced by the `pair-guard` hook rather than by this description. Keep the two in sync.

## When this applies

Judgment-heavy work — where a wrong decision still compiles, still runs, and still produces
plausible-looking output. Plumbing fails loudly; judgment fails silently. That's the whole test.

The `pair-guard` PreToolUse hook blocks writes to the high-stakes paths until Phase 3 clears.
If a write was just blocked, you are already in this protocol — start at Phase 1, not Phase 4.

## Phase 1 — Take the input, write nothing

The user brings a topic, or pseudo-code, or a rough direction. Read whatever source you need to
understand the problem. Write no code: no edit, no scaffold, not a stub, not "just the signature".

## Phase 2 — Review it out loud

- Restate the problem concretely in your own words. If it's numeric or geometric, use a worked
  example with real numbers (a 3000x2000 source, a 0.62 confidence) — never "the input". Add counter examples in cases where you suspect ambiguity might be possible.
- Name the one constraint that actually decides the shape of the solution. Most designs have
  exactly one. Find it and say it plainly.
- Name at least one alternative you are rejecting, and why it loses. "Why it loses" means a
  specific input where it gives the wrong answer, not a vague tradeoff.
- If the user's pseudo-code has a hole, state it as cause -> effect -> consequence in simple
  language: what the code does, what that produces, what that costs downstream.

Never soften a real flaw to be agreeable, and don't manufacture one to look rigorous. If their
approach is simply right, say so in one line and move to Phase 3.

## Phase 3 — Consensus gate

Don't write code until the user signals they are with you. Consensus means they understood the
reasoning, not that they got bored — if their reply is only "ok", ask the one question whose answer
proves the idea landed, then wait.

On agreement, append the target file's name to `.claude/.pair-consent` (one per line). That file is
what unblocks the guard hook; it's cleared automatically at the start of every session.

If the user says "just do it" or otherwise waives the gate, honour it — write the consent line and
proceed, but keep the Phase 4 commentary.

## Phase 4 — Implement one segment at a time

A segment is one coherent unit of meaning: a method, a loop body, a tensor setup, a config class.
Not a file, and not the whole change.

After each segment, in a few sentences: what it does, why this shape rather than the obvious
alternative, and how it behaves at the edges — empty input, a single item, the degenerate case.

Then ask exactly one probing question and stop. Wait for the answer before the next segment.

**The question rule: ask only a question whose wrong answer changes what you write next.** If every
possible answer leads you to write the same code, it's a quiz the user has to humor — cut it and
move on. "If the source is already wider than the target, what should the gate do?" earns its place
because the answer picks the branch. "Does that make sense?" does not.

If an answer reveals a gap, don't simply correct it. Re-explain from the angle they missed, then
re-ask differently. A gap found here is cheaper than one found in the output images.
