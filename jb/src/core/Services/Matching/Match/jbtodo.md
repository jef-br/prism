# jbtodo — Match

## StringMatcher has no typo tolerance, but the docs say it should

- [ ] StringMatcher edit-distance gap: `jb/docs/PRISM-match.md` says string
  matching tolerates small spelling differences ("edit distance"), especially
  for categorical columns (color, material, product type) — spelling mistakes
  should be penalized less than a serial-number mismatch. The actual code
  (`StringMatcher.cs`) only does exact token matching through an inverted
  index (`GetOrBuildTokenIndex`). No edit-distance/fuzzy logic exists
  anywhere in the Match folder. Which one is correct: does the code need
  typo tolerance, or does the doc overstate what StringMatcher does?
- Impact:
  - Medium - filenames with small typos or regional spelling ("gray" vs
    "grey", "hoody" vs "hoodie") never match categorical Excel values today.
    Those images either fall through to Bracket 4 (if CLIP evidence exists)
    or get KO'd, when the documented design intended them to match directly
    in Bracket 3.
  - Effect on other TODOs: none currently.
- Industry standard:
  Fuzzy string matching usually allows a small, bounded number of character
  edits (insert/delete/substitute) between two words before treating them as
  different. This is called "edit distance." Short words require an exact
  match; longer words tolerate one or two small differences, scaled to word
  length so a 3-letter word doesn't accidentally match a completely
  different 3-letter word.
- Recommended solution:
  First decide which side is wrong — the doc or the code. If the code should
  gain typo tolerance: add a bounded edit-distance check (distance ≤ 1,
  matching NumericMatcher's own `maxDistance: 1` production value) only for
  categorical column tokens (not descriptive/mixed free text, to avoid false
  matches on long sentences). Reuse the Levenshtein helper that already
  exists in `jb/src/core/lib/Excel/ModelBuilder.cs` (used today for header
  detection) instead of writing a new one. If the doc is wrong instead:
  update `jb/docs/PRISM-match.md` to describe the real exact-match-only
  behavior and drop the edit-distance claim.
- Answer: (observed 2026-07-13, non-final — doc-vs-code call still yours)
  - Contradiction confirmed both ways: grep finds zero Levenshtein/fuzzy/edit-distance
    logic anywhere in `Services/Matching/Match/` (only this todo mentions it), while
    `jb/docs/PRISM-match.md:76` does promise it — "string matching tolerates edit
    distance — for categorical columns it is less penalized." So the doc overstates a
    capability the code never had; this is a real doc↔code mismatch, not a
    misreading.
  - Correction to the recommendation above: `NumericMatcher`'s production
    `maxDistance` is **1.478** (`MatchingConfig.json`), not `1`, and it is a **TCD**
    (numeric magnitude tolerance on concatenated digit tokens), not a character edit
    distance — `PRISM-match.md:49` says so explicitly ("Uses TCD, not classical
    Levenshtein typo tolerance"). So don't cite `maxDistance` as a precedent for a
    Levenshtein ≤ 1 threshold; it is neither the value (1 vs 1.478) nor the kind
    (numeric distance vs char edits) claimed.
  - The Levenshtein helper does exist: `ComputeLevenshteinDistance` at
    `lib/Excel/ModelBuilder.cs:928` (header detection gates it on token length ≥ 4,
    distance ≤ 1). But it lives in `Prism.Lib.Excel`; calling it from
    `Prism.Services.Matching` adds a Matching→Excel project reference the restructure
    otherwise keeps apart — so "reuse" is really copy-the-~35-line-helper vs. take the
    dependency, a tradeoff to weigh, not a free reuse.

## Substring rescue scans the whole digit index per rescue token — is this a real slowdown?

- [ ] TryMatchBySubstringRescue perf: `NumericMatcher.TryMatchBySubstringRescue`
  checks every entry in the digit index for every leftover filename token on
  every still-unmatched image, using a plain "does this string contain that
  substring" check instead of an index lookup. On paper this is
  `O(unmatched images × rescue tokens × index size)`, and
  `indexDigitRunsAllColumns=true` in production makes the index large (every
  digit run of every column of every family). Nobody has actually measured
  whether this shows up in real processing time.
- Impact:
  - Needs a measurement before a level can be assigned. The realistic heavy
    load is ~2,500 images per batch (`jb/docs/PRISM-overview.md`: "Heavy
    daily avg: ~10k images + 2 Excel → ~4 batches of 2500"). This rescue pass
    only runs for images still unmatched after Brackets 1–5, which should
    normally be a small minority of a batch, not all 2,500 — so the honest
    answer today is "probably fine, but unverified," not "definitely a
    hotspot."
  - Effect on other TODOs: none.
- Industry standard:
  For "which of these many strings contains this substring" lookups, the
  standard approach is a specialized index (suffix array, suffix automaton,
  or n-gram/trigram index) that answers the question in roughly constant
  time per query, instead of checking every candidate string one at a time.
- Recommended solution:
  Measure before changing anything: add a `Stopwatch` around the
  `TryMatchBySubstringRescue` index scan (or use existing pipeline timing if
  one exists) and run it against a representative CiMini or full-scale batch
  to see what fraction of total matching time this step actually takes. If
  it's negligible, close this todo with "measured, not worth it" and make no
  code change. If it's measurably slow, build a proper substring index (e.g.
  an n-gram map built once per `RunSubstringRescue` call) instead of the
  current brute-force scan — but only after the measurement justifies the
  added complexity.
- Answer: (observed 2026-07-13, non-final — still needs the Stopwatch run)
  - Two config facts tighten the worst-case before any measurement: production
    `minSubstringRescueLength = 7` (`MatchingConfig.json`), and the pass is
    self-disabling — `NumericMatcher.cs:433` returns early when that is ≤ 0. So the
    "rescue tokens" factor is not "every leftover filename token" but only the
    filename's digit tokens of length ≥ 7 (`NumericMatcher.cs:443`), typically 0–2
    per image, which shrinks the `O(unmatched × rescue tokens × index size)` product
    further than the header framing implied.
  - Verdict unchanged: still `probably fine, unverified`. The honest close needs a
    Stopwatch around the `.Contains` scan on a CiMini/full batch — the config bounds
    argue it is negligible but do not prove it.

## Bracket 4's totalImageTokens count is approximate — does that ever change a matching decision?

- [ ] SemanticMatcher.totalImageTokens: in `SemanticMatcher.TryMatch`,
  `totalImageTokens` is calculated as (number of distinct filename tokens
  that matched something) + (number of candidate families that were scored)
  — the code's own comment calls this "rough total; precision here is
  cosmetic". This number is then used to compute `stringSignal`, which is
  one third of the combined score checked against `SemanticThreshold` to
  decide whether an image gets matched in Bracket 4.
  - What the number is trying to measure: "out of all the meaningful words
    in this filename, how many did we actually match against the winning
    family?" A higher fraction should mean a more confident match.
  - Example where it's roughly fine: filename `product_blue_hoodie_2024.jpg`
    matches "blue" and "hoodie" against 1 remaining candidate family.
    `totalImageTokens = 2 (matched) + 1 (candidate count) = 3`.
    `stringSignal = 2/3 ≈ 0.67`. Not the filename's true token count (which
    is really 4: product/blue/hoodie/2024), but in a plausible range.
  - Counter-example where it's likely misleading: same filename, but this
    time 5 candidate families reach the scoring step before narrowing to 1
    winner. `totalImageTokens = 2 (same matched tokens) + 5 (candidate count)
    = 7`. `stringSignal = 2/7 ≈ 0.29` — much lower, even though the matched
    evidence (2 tokens) and the filename itself did not change at all. The
    score moved only because more families happened to be in the candidate
    pool that round — which has nothing to do with how well this filename
    actually matches the winning family.
- Impact:
  - Unknown until checked — could be Low (score differences never large
    enough to flip an accept/reject decision near `SemanticThreshold`) or
    Medium (near-threshold images could flip between matched and KO'd
    depending on how many other candidates happened to be in the pool that
    round, which is an unrelated coincidence).
  - Effect on other TODOs: none.
- Industry standard:
  A "match confidence" ratio should be `(matched tokens) / (total real
  tokens in the input)` — it should not mix in an unrelated count like "how
  many other candidates were being compared in this pass."
- Recommended solution:
  Replace `totalImageTokens` with an actual count of meaningful filename
  tokens — the same tokenization `StringMatcher` already does to extract
  tokens from a filename — independent of how many candidate families
  happened to be in the pool. This makes `stringSignal` reflect only "how
  much of this filename did we actually explain," which is what the
  threshold check is supposed to measure. Needs a before/after comparison on
  a labeled set (or at least CiMini) to confirm accept/reject decisions
  don't shift in an unwanted way before rolling this out.
- Answer: (observed 2026-07-13, non-final — code re-verified, product call still open)
  - Description matches current code verbatim after the restructure:
    `SemanticMatcher.cs:94-95` builds `totalImageTokens =
    stringEvidence.Select(FilenameToken).Distinct(OrdinalIgnoreCase).Count() +
    scored.Count`, and `:120-121` forms `stringSignal = min(1, stringEvidence.Count /
    totalImageTokens)`. So the counter-example is real: `scored.Count` (candidate
    families reaching the scoring step) inflates the denominator with a quantity
    independent of the filename, dragging `stringSignal` — one third of the
    `SemanticThreshold` check — down purely on pool size.
  - Nothing here is resolvable from code alone. Whether it ever flips an
    accept/reject near `SemanticThreshold` is the empirical question the
    recommendation already frames; the fix (count real filename tokens instead) is
    clear but needs the labeled/CiMini before-after before rolling.
