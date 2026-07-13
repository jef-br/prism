# PRISM — Image Ordering & Rename
*Abbreviations: `GLOSSARY.md`*

## `ImageOrderer.cs`

Orders images associated to a single FID using IFs, derived INGPs, DO rules, and match information.

---

## `_det` Suffix Rules

- Always **zero-based** (`_det0`, `_det1`, `_det2`, …).
- Order gaps **allowed** when missing det positions can be filled by generation.
- After generation/renaming, remaining gaps are **closed** per the gap policy below.

### Gap policy (`DET-ORDER-GAPS-ALLOWED`)

Gap closing is controlled by the boolean `DET-ORDER-GAPS-ALLOWED` in `Prism_Config.json`:

- `true` → det indices are kept exactly as the Order stage assigned them; gaps (e.g. an empty `_det0` when a family has only a SIDE image) are preserved.
- `false` → each family's det indices are compacted to be contiguous from `0`.

The Order stage itself is **unchanged** by this policy: the phenotype/DO chain plays out fully and each image is assigned to its best-ranked slot (a SIDE image stays in its `det2` slot, leaving `det0` empty when no FRONT/DIAGONAL image exists). Gap handling is a **separate export-time pass**, not an ordering change.

Compaction rules when `false`:
- Applies to the **entire non-KO ("OK") collection** — all families, all product types.
- For each family, take its images in ascending current `DetOrder` and reassign contiguous `0..n-1`.
- **Only closes gaps — never reorders.** The relative det order assigned by the Order stage is preserved exactly under all circumstances.

Implementation placement:
- Handled during **export**, as the first step of `Exporter.Run` (before output-record and manifest building, both of which read `DetOrder`). `DetOrderConfig` and `ImageOrderer` are untouched; the flag reaches `Exporter.Run` via `ExportRequest`.
- Compaction only renumbers `DetOrder`. `ImageRecord_Base.NewName` is computed (`{Family}_det{DetOrder}.jpg`), so the output filename, manifest row, and `ImageRecord_OUTPUT.DetOrder` follow automatically — no filename string rewriting.
- Safe against rename collisions: `ImageRenamer.HasDetCollision` runs upstream, and a monotonic gap-closing renumber of already-distinct indices cannot introduce a collision.

---

## Ordering Model

`IF → INGP → DO position`

- IF: one measured attribute from CLIP or purpose-built analyzer.
- INGP: phenotype label derived from a combination of IFs.
- DO: PT-specific priority list mapping INGPs to `_det` positions.

Ordering happens inside one matched FID group. Multiple INGPs can qualify for one DO position; one INGP can qualify for multiple DO positions.

**Step 1 — DO candidate assignment:**
For each image, build all qualifying INGP/DO combinations. Final placement:
- Earlier DO position wins first (`_det0` > `_det1` > …).
- Within same DO position: INGP priority rank for that DO wins.

**Step 2 — Filename ordering hints:**
Scan original filename for order indicators: keyword tokens (`front`, `side`, `back`, `a`, `1`, `det0`, …), numerical suffix (`_1.jpg`), alphabetical suffix (`_A.jpg`), alphanumerical suffix (`_A1.jpg`).

Rules:
- Filename hints support or break ties **only after** INGP qualification and DO eligibility are established.
- Cannot define an INGP, cannot assign `_det#` directly, cannot override DO eligibility.
- `DetOrderKeywordStems.json` = ordering hints only.
- Preserve token source, position, original text, normalized text, purpose as matching/ordering evidence.

**Step 3 — Classification and analyzer evidence:**
IF from CLIP and analyzers derive INGPs:
- Front orientation feature → front-facing INGPs can qualify.
- Human, head, PT, background, edge-intersection, detail features distinguish INGPs.
- Classification labels do not assign `_det#` directly.

**Step 4 — Deterministic tie-breaking:**
When multiple images in one Family compete for same DO position and INGP priority rank, break ties:
1. Selected INGP confidence (evidence-count-based for tie-breaks; individual ML confidence not yet used as weighting).
2. Compatible filename ordering hint.
3. Filename ordinal — a total order that does not depend on list position, so upstream reordering cannot change the outcome (T-2820).
4. Stable import/source index (first image opened = source index assigned during import). Reached only when two images carry the same filename — a ZIP holding `folderA/img.jpg` and `folderB/img.jpg` keeps only the leaf name.

**Which tie-breaker the evidence names.** DO assignment evidence records the winning tie-breaker in `OrderEvidence.TieBreakerWon` (`ngp-confidence` | `filename-hint` | `filename-ordinal` | `source-index` | `none`). The winner is compared **only against its closest rival** — the next still-unassigned candidate at the same DO position and INGP rank — never against every competitor in the family:

- Comparing against *any* competitor names the wrong level. A far-behind image that lost on INGP confidence would make the evidence report `ngp-confidence` even when the winner actually beat its real rival on the filename hint, hiding the signal that decided the close call.
- An image that already holds an **earlier** DO position is not a rival for a later one — it left the race when it was assigned. A position nothing else was still contesting reports `none`, as does one whose only remaining contenders sit at a worse INGP rank — rank is part of the ranking, not a tie-breaker, so no tie-breaker fired.
- The comparison levels mirror the candidate sort exactly, so the recorded tie-breaker can never contradict the order the sort produced.

Overflow images (no qualifying INGP) never run this chain; they report how their position was derived instead: `overflow-filename-hint` or `overflow-natural-order`.

---

## Output Filename Rules

- Stem = matched FR FID. Source filenames/display labels do not become the stem.
- All output images use `.jpg` extension.
- Valid ordering guarantees unique final filenames: `FID_det#.jpg` per FID group.
- Output names reserved before export (covers final filenames, zip paths, JSON artifact paths).
- Collision → KO the whole affected FID/family with `RENAME_COLLISION` or `export-path-collision`; keep original filenames as provenance; emit safe manifest evidence; continue rest of batch.

## Output Filename Sanitization

Allowlist: `A-Z a-z 0-9 . _ -`. Replace everything else with `_`. Collapse whitespace runs and repeated `_`; trim leading/trailing whitespace, `_`, `.`. Empty sanitized basename → KO (no fallback). Reject: path separators (`/ \ :`), names ending in space or period, `.` / `..`, Windows reserved device names (`CON`, `PRN`, `AUX`, `NUL`, `COM1–COM9`, `LPT1–LPT9`, case-insensitive, with or without extension).

---

## Unmatched Image Naming

Unmatched images → KO records.
- Keep original filename as safe provenance in `manifest.json`.
- No OK FID-based output filename.
- Excluded from OK output.
- KO export placement governed by zip/layout policy.
