



-----

## Docs must use current, project-accurate vocabulary (raised 2026-07-14)

**Answer:** OPEN

Sweep `jb/docs/` (and `CLAUDE.md`, `AGENTFEEDBACK.md`, folder-local docs) for stale spelling of terms,
type names, field names, attributes, config keys, and file paths. Docs drift behind the code every time
a rename lands — recent examples: `ImageTransformationResult`/ITR (deleted, T-4550),
`PrismConfigLocator`/`ConfigCache` (deleted, T-4560), `Images/Upscale/...` paths (now
`Services/Upscale/Engine/...`), `Prism.Core.Images.*` assembly names (still stale, T-3700).

**Special attention to PRISM-specific vocabulary** — FamilyID, `_det#`, ImageNGP, ImageRole,
DetOrderRules, IEM, KO, Batch, phenotype, LAMBDA/INPUT/OUTPUT records. These are the terms a reader
cannot guess or infer, so a stale one is worse than a stale ordinary word: it silently teaches the wrong
model of the system.

**In case of ambiguity: do not guess.** Investigate the code, clarify what the term actually denotes
today, and *propose* a fix — do not quietly rewrite docs to match whatever the code happens to do, since
sometimes the doc is right and the code is the bug (see [[T-2830]], where the doc's `_det0` convention was
the correct one).

**Also update the PRISM dictionary** (`jb/docs/GLOSSARY.md`) as part of the same pass: retire dead
abbreviations, add ones that entered the vocabulary without being recorded, and make sure every
abbreviation still resolves to a type or concept that exists.
