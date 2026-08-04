# JBComplete — the full matching + phenotype case set

**Never delete this folder.** It is the only place several of these cases exist.

97 images (63 MB, all committed to git) + `Brackets-Complete.xlsx` + `3 images.zip`, plus two authored
ground-truth files. It was assembled to close the two holes that block progress: the matcher cases
[[T-3800]] needs, and the phenotype cases `../CiGolden/README.md` lists as "no image in this repo fits".

This file is an inventory written after reading every image and every Excel cell, and after checking
what the loader code actually does with them. Where it says *intent*, that is inferred from the
filename/Excel pairing, not stated anywhere.

---

## 1. The Excel — which sheets actually produce families

`Brackets-Complete.xlsx` has four sheets. `ModelBuilder` processes **every** sheet of a workbook, and
`ExcelConfig.json` requires a FamilyID to be **8 characters, all digits**.

| Sheet | Rows | Produces families? |
|---|---|---|
| `JB-generated` | 32 | **Yes** — 32 valid 8-digit FamilyIDs. This is the real product table. |
| `JB-matched` | 3 | **Yes** — 3 families (`88884444`, `88884445`, `90861025`). |
| `Products` | 29 | **No.** FamilyIDs are `TW001`, `GS001`, `BC001`… — 5–6 chars, not numeric, so `IsValidFamilyID` rejects every row. |
| `AGEC LAW` | 0 data rows | No — headers only. |

**The `Products` sheet is agent-generated and is not authoritative.** It was produced on request as a
set of worked examples, and it contains hallucinated detail — image filenames that were never created,
a `WebsiteImageLink` pointing at a file that does not exist. Its FamilyIDs are also invalid, so it
contributes nothing to a run. Treat every claim on it as a hypothesis.

It is still worth keeping. Its `Notes` column describes matcher behaviour in more detail than anything
else in the repo, and several of the cases it sketches are genuinely untested. §6 sorts them into
what is worth building, what is already covered by real data here, and what is noise.

**Two deliberate cross-sheet collisions in the valid rows:**

- `4471-2290` is family `98884444` on `JB-generated` **and** `88884444` on `JB-matched`. Same for
  `4471-2340` → `98884445` / `88884445`. Any image named `4471-2290-*` therefore points at two
  families.
- `90861025` appears on both sheets with the same ID — the collation case (merge into one record),
  not a conflict.
- Inside `JB-generated`, EAN `5401209028218` is on **both** `98884444` and `98884445`. A non-unique
  EAN cannot be used as an `OrphanRowJoiner` key.

---

## 2. Image → family cross-reference

Grouped by what each cluster exercises. "Ref" = the Excel `Reference` value the filename points at.

### 2.1 Matching — reference resolution

| Images | Family | What it tests |
|---|---|---|
| `4471-2290-B/D/F` | `98884444` **and** `88884444` | Reference present under two FamilyIDs across two sheets → ambiguity. |
| `4471-2340-B/D/F` | `98884445` / `88884445` | Same collision, second colourway (blue vs lime). |
| `OMB-E166-BV_1..4` | `98636303` VICKY | Exact reference hit, brown shoulder bag. Control case. |
| `OMB-E181-CVW_1..6` | `98636312` ZOLA | Exact reference hit, black woven bucket bag. Control case. |
| `OMB-E129-TGV_1..4` | Excel has `OMB-E198-TGV` (`98636325` KENDALINI) | **Near-miss reference** — digits transposed (129 vs 198) — but colour (green), type (crossbody/belt bag) and brand all agree. Does the waterfall accept on attribute agreement or hold the reference mismatch? |
| `OMB-E180-BV_1..6` | **no row exists** | Reference that resolves to nothing, while a same-colour same-type family (`OMB-E166-BV`, brown bag) sits right there to be stolen. Must stay unmatched. |
| `AY_FFK0230_83035_01..06` | `98226706` (Ref `FFK0230-83035`) | Reference written with **underscores** instead of the Excel's hyphen, plus an `AY_` prefix. Also carries `FW001_b/A/c/d/f` tokens with mixed case. |
| `C153KB460011_*` (3) | `99147525` Short Cedric | Exact reference (minus the `-090` suffix). |
| `C153KU420009_*` (3) | `99147533` Jeans Kendall | Exact reference. |
| `2426834-7558_*` (4) | `98768768` FILA sneaker | Exact reference. |

### 2.2 Matching — no reference in the filename

| Images | Family | What it tests |
|---|---|---|
| `100267_1..7` | `91337133` (Ref `falamintje_267`) | **Filename-in-cell.** `100267` appears nowhere in the reference; the only link is the marketing-description text `http://thisisnotanurlbutitlookslike.one/100267_1.jpg, …`. That host is fake — this is text bait for `StringMatcher`, **not** a URL-import case. Don't point a fetcher at it. |
| `133726012.jpg` | `98008135` (Ref `80082133726012`) **and** `98008136` (Ref `8008132133726012`) | Filename digits sit inside **two** references → substring ambiguity, must reject. Labels are `Madeuparrurah` / `ExoticFake` — deliberately junk. |
| `8712345678901`, `-2`, `-3` | `99985014` (EAN `87186798712345678901002387`) | Filename digits hidden inside an EAN. Intended as an ambiguity case via `Products` rows BC001/BC002 — **but those rows are dropped**, so today only one candidate survives and it accepts. |
| `87186790_1/2` | `99984905` (EAN `8718679018555`) **and** `99985047` (EAN `8718679021272`) | `87186790` is a prefix of both EANs → two candidates, and the picture (cream shirt-jacket + wide trousers) matches **neither** garment. Must reject. Largest images in the set at 3543×5314. |
| `TH12_0N3_M47CH32_70_R3D_36R37.jpg` / `…_AS_4_FR0N7.jpg` | `98226808` Polo shirt (Color `True Red-Egret`) — *intent* | Leetspeak filenames ("this one matches to red egret" / "as a front") that tokenise to nothing usable. Pure visual + Excel-colour match. Second file is the front, first the back → also a det-order case. |
| `T_SHIRT_EGRET_DETAIL.jpg` | `98226808` Polo — *intent* | **Trap:** the filename says `T_SHIRT`, which points at `98226972` (Round-neck T-Shirt, same `True Red-Egret` colour); the picture is unmistakably a polo (collar, placket buttons, FILA patch). Filename evidence vs image evidence, in direct opposition. |
| `IMG_4432.png` | none | Blue Vingino jeans on transparent. `IMG` is a configured camera prefix, so the filename is meaningless; no jeans family exists (`99147533` "Jeans Kendall" is the sand pair). Expected KO, not a forced guess. |
| `triggered-mistery.jpg` | `91234321` (Triggered T-shirt Lemon Crème) — *intent* | Filename says nothing. The **print on the garment reads "LEMON"**, and the cream colour is what separates it from the black and pink rows. |
| `triggered-tshirt-main.jpg` | `98091311` (Pink) — *intent* | `triggered` + `tshirt` match all three Triggered rows equally; only the pink colour breaks the tie. |
| `Pareo Exotica`, `Pareo_exotica_F1/F2` | `94613033` | Space in filename; carried over from CiMini. |

`98226972` (Round-neck T-Shirt) has **no image at all** — an Excel family that never receives one.

### 2.3 Matching — folder names

Three subfolders sit at the same depth, so `FolderNameEnricher`'s `minPerItemSiblings: 2` gate passes.

The two folders **deliberately both contain `1.jpg` and `2.jpg`**. That is the point: PRISM has to
judge a file by its full path before deciding anything about it, including whether it is a duplicate.
Two files called `1.jpg` in different folders are two different images.

Measured by running the real `FolderNameEnricher` against the real `JB-generated` rows
(scratch probe, 2026-08-04 — results below are observed, not reasoned):

| Folder | Files | Alias assigned | Why |
|---|---|---|---|
| `26182-Denim-801/` | `1.jpg`, `2.jpg`, `a (1).jpg` | **yes** — `26182-Denim-801 1.jpg` | Folder tokens `26182` and `denim` are both in `99985047`'s vocabulary (Ref `26182-801`, Label `Denim-26182-801`). |
| `foldercontainsID99984905/` | `1.jpg`…`4.jpg` | **no** | `MeaningfulTokens` keeps a mixed letter+digit run whole and `continue`s past the letter↔digit split, so the folder yields exactly one token: `foldercontainsid99984905`. The probe confirms `99984905` **is** in the Excel vocabulary, so the failure is entirely on the folder-token side — it should yield `foldercontains` + `99984905`. |
| `99984901/` | `99984901_det0/det1.jpg` | no — correct | The filenames already carry the ID, so the enricher skips them by design. `99984901` is **not in the Excel at all**; olive printed blouse, front + back. Unmatched-family case. |

Both the missing split and the fact that no folder path survives upload at all are [[T-5020]].

### 2.4 Import surface

| Item | Notes |
|---|---|
| `3 images.zip` | 3 members (`24211511_96_A`, `23231096_35_A`, `24211511_86_A` → families `90861076`, `90861071`, `90861075`), all ≥923×1200 so clear of the 570 px floor. Downscaled copies of CiMini's zip members. |
| `2021_3024_46_A - Copy.jpg`, `2021_3041_65_A - Copy.jpg`, `4471-2290-F - Copy.jpg` | **Three files named "Copy"; one of them is.** `4471-2290-F - Copy.jpg` is byte-identical to its namesake → expect `VISUAL_DUPLICATE`. The other two are tighter detail crops of the same shoot (torso/belt, hip/hand) at different pixel dimensions. So "is this a copy?" cannot be answered from the name, and cannot be answered from any single property either — it needs the visual hash, the pixel dimensions and the byte length weighed together. This trio is the case that proves it. |
| `100267_6  - BW001_c.jpg` | **Two spaces** before the dash. |
| `C153KB460011_Cedric__City_Grey_DETAIL.png` | **Double underscore**. |
| `26182-Denim-801/a (1).jpg` | Space + parentheses in the filename. |
| `87186790_1/2.jpg` | 3543×5314 — exercises the resize path hard. |
| 13 PNGs | Real alpha (`RGBA`, min alpha 0). See §3. |

### 2.5 Conflicting evidence, on purpose

- **Vingino colours are swapped.** `99147525` (Short Cedric) carries Color `Twill sand` but its images
  are **grey** shorts; `99147533` (Jeans Kendall) carries Color `Gray` but its images are **sand/tan**
  trousers. The filenames agree with the pictures (`_City_Grey_`, `_Twill sand_`) and disagree with the
  Excel. Reference number should win over colour — this proves whether it does.
- **`99218810` and `99218793` are identical in Excel** — same Label ("White socks - tennis"), same NGP,
  same Brand, same Type. Only the FamilyID differs, and both images are near-identical white crew
  socks. Nothing but the filename can separate them.
- **`_det` numbering doesn't start at 0.** `99218793` has only `_det1`; `99218810` has only `_det1`.
  Tests whether rename renumbers to `_det0`.

---

## 3. Phenotype coverage — what this adds over CiGolden

CiGolden's README lists six cases nothing in the repo covered. JBComplete closes four of them.

| CiGolden gap | Status | Images |
|---|---|---|
| 1. `diagonal-packshot` on a **plain solid** background | **Closed** | `OMB-E180-BV_4`, `OMB-E181-CVW_4`, `OMB-E166-BV_3` — three-quarter angle, whole product, plain light-grey sweep with margin. |
| 2. `interior-shot` (shot down into the compartment) | **Closed, five ways** | `OMB-E129-TGV_4`, `OMB-E166-BV_4`, `OMB-E180-BV_5`, `OMB-E181-CVW_5`, `OMB-E181-CVW_6`. Striped linings, clearly the subject. |
| 3. `illustration-technical-drawing` | **Still open** | `100267_6` (dimension callouts, 39.5 cm / Ø25 cm) and `100267_7` (4-panel feature collage) are the closest, but both are marketing composites built on photographs — not black-line-on-white sketches. |
| 4. `front-packshot` on white for a **second product type** | **Closed** | Bags: `OMB-E181-CVW_3`, `OMB-E180-BV_3`, `OMB-E166-BV_2`. Shoe: `2426834-7558_side-packshot_shoe`. |
| 5. Low-contrast **white-on-white** ([[T-4948]]) | **Closed** | `99218809_det0` (3 no-show socks flat), `99218809_det1`, `99218810_det1`, `99218793_det1` — white socks on a near-white sweep. This is the contrast-floor case. |
| 6. Hard-shadow vs soft-shadow **pair** ([[T-4945]]) | **Still open** | No controlled twin of one product shot both ways. The alpha PNGs have a glow, not a shadow; the OMB bags are all soft. |

**Overall: 20 of the 21 phenotypes in `ImageRoles.json` now have at least one real positive case.**
Only `illustration-technical-drawing` has none. Newly covered beyond CiGolden's list:

- **`ghost-side`** — `C153KB460011_Cedric__City_Grey_DETAIL.png` and
  `C153KU420009_Kendall_Twill sand_DETAIL1.png`. Both are named "DETAIL" but show the *whole* garment
  from the side on an invisible mannequin. These are the only `ghost-side` images in the repo.
- **`on-model-with-accessories`** — the three scarves in `3 images.zip`. The hero product (a scarf) is
  worn over a sweater and trousers, so several products are in shot.
- **`lifestyle-hero`** (as opposed to `lifestyle-context`) — `2426834-7558_FW001_e`, a man centred in
  an Italian doorway with the product clearly prominent.
- `ghost-front` / `ghost-back` on a garment with real edge contrast — `triggered_ghost-front.jpg`,
  `triggered_ghost_back.jpg` (black tee, white background). CiGolden only had beige-on-transparent.
- `back-on-model-partial` — the rule gap CiGolden flagged as "NORULE" —
  `triggered_black-tshirt-back-americain.jpg`, `foldercontainsID99984905/2.jpg`.
- `bottom-packshot` / `top-packshot` / `side-packshot` with FamilyID-bearing names —
  `2426834-7558_bottom-packshot_sole` / `_top-packshot_overhead` / `_side-packshot_shoe`.

### Two rule collisions this set exposes

Stated as measurements, not as defects. A collision where one rule's `required` block subsumes
another's is **acceptable** when there is a concrete, evidenced case that a named future signal will
separate them — a specified analyzer, or a feature that is measurable and not yet measured. It is not
acceptable on the assumption that something will turn up. Establishing which of these two is which is
[[T-5040]]'s job, not this file's.

- **`ghost-*` and `*-packshot` are not separable by the rules.** Their `required` blocks are identical
  except that ghost accepts `clipping-path = true` *in addition to* `background-type = SOLIDCOLOR`.
  So any garment on a solid background satisfies both, and `front-packshot` (index 7) wins over
  `ghost-front` (index 13) purely on list order. The ground truth in `expected-phenotype.json` labels
  by the domain meaning — ghost when the garment holds a worn 3D shape, packshot when it is flat or a
  rigid non-garment — so those rows disagree with the engine today. Separating them needs a signal for
  *does the garment hold a worn 3D shape*, which PRISM does not currently measure. [[T-5030]] removes
  the alpha path entirely, which changes what `clipping-path` can even mean here.
- **`on-model-with-accessories` sits after `front-on-model-partial`.** All three scarf images satisfy
  both, so the earlier rule wins and `on-model-with-accessories` may be unreachable in practice. This
  phenotype has no identified consumer in `DetOrderRules.json` — see [[T-5040]].

### Alpha is a soft glow, not a clipping path

All six `AY_FFK0230_83035_*` PNGs and both Vingino sets have a **feathered halo**: alpha ramps from 0 to
255 over a wide band around the product, not a hard 0/255 edge. Anything that reads
`clipping-path = true` from "has an alpha channel", or derives a subject box from "alpha > 0"
([[T-4960]]), will get a box noticeably larger than the product. `IMG_4432.png` behaves the same way.

### Filename keywords will silently decide the answer for most of these images

**The mechanism.** `Analyzer_FilenameEvidence` looks for whole tokens like `front`, `back`, `side`,
`top`, `bottom`, `diagonal`, `angle` in the filename. When it finds one it writes `hero-orientation`
directly, at **0.75 confidence**. The highest `hero-orientation` confidence CLIP has ever produced on
this repo's data is **0.582**. Since the higher-confidence value wins, a filename token always beats
the picture.

**The consequence for measurement.** `hero-orientation` is a required feature on 12 of the 21
phenotype rules — every `*-packshot`, every `ghost-*`, and the front/back on-model rules. So for any
image whose filename contains one of those words, the phenotype is decided by the filename before the
model is consulted. Running a phenotype accuracy measurement across JBComplete unchanged therefore
produces a number that mostly describes how well the filenames were typed, and it will look good.
That is the trap: a high score reads as "the model works" when the model was never asked.

**Which JBComplete files are affected.**

- **Will fire:** `2426834-7558_bottom-packshot_sole`, `_side-packshot_shoe`, `_top-packshot_overhead`,
  `C153KU420009_..._BACK`, `C153KU420009_..._FRONT`, `triggered_black-tshirt-front-*`,
  `triggered_black-tshirt-back-*`, `triggered_ghost-front`, `triggered_ghost_back`,
  `TH12_..._AS_4_FR0N7` (no — `FR0N7` is leetspeak and tokenises to nothing; it does **not** fire).
- **Will not fire:** `C153KB460011_..._BAC` and `C153KB460011_..._FRON` are **truncated on purpose**.
  `BAC` is not the whole token `back`; `FRON` is not `front`. These two are the deliberate control
  cases where orientation has to come from the model. **Do not correct the spelling** — that would
  destroy the only clean pair in the set.

**What to do about it.** Pick one of these and state in the result which you picked:

1. Copy the images to a scratch folder under FamilyID-only names with every orientation word stripped,
   and measure there. Highest fidelity, and the goldens still key on the original names so you have to
   map back.
2. Measure only the subset whose filenames contain no orientation token, and report the subset size
   alongside the score. Cheaper, but the subset is small and skewed toward the alpha PNGs.
3. Temporarily disable `Analyzer_FilenameEvidence`'s orientation output and measure the whole set.
   Cleanest signal, but it is a code change so it cannot be part of a routine CI run.

---

## 4. Ground truth

Two files. **Both were written by hand from what each image should resolve to. Neither was recorded
from a pipeline run.**

That is the opposite of how CiMini's goldens were made. CiMini's were produced by running the
pipeline, eyeballing that the output looked right, and saving it — so CiMini's goldens describe what
PRISM *does*, and a CiMini failure means something regressed. These files describe what PRISM
*should* do, derived from the Excel rows and the pictures. Some of the entries describe behaviour
PRISM does not produce yet, deliberately, so **these files are expected to fail on day one** and the
failures are the work list.

The practical consequence: `Invoke-CiPipeline.ps1 -Capture` would overwrite these with whatever the
pipeline currently emits, converting a specification into a snapshot of the bugs. **Never run
`-Capture` against this dataset.** Fix the pipeline, or change an entry deliberately with a reason.

### 4.1 `expected-match.json` — 90 entries

Same schema as CiMini's (`SourceReference` → `FamilyId`), plus an optional `Note`. `Invoke-CiPipeline.ps1`
reads only the first two fields, so the notes are free. `FamilyId: null` means the image **must not**
match — 20 of the 90 entries are deliberate rejections. 70 entries expect a match, across 24 families.

Ten sources are deliberately **not** listed:

- `4471-2290-F - Copy.jpg` — expected `VISUAL_DUPLICATE`, which the CI script strips from the result
  before comparing. Listing it would fail as "MISSING source".
- The nine subfolder images — unreachable, see §4.2.

### 4.2 The folder cases are held out of the match golden — [[T-5020]]

Three independent causes, any one of them sufficient on its own. All three are [[T-5020]].

1. **The runner flattens paths.** `Get-PrismJobInputFiles` de-duplicates by *leaf filename*
   (`$seen.Add($file.Name)`), and `Submit-PrismJob` uploads with `[Path]::GetFileName($path)` and packs
   ZIP entries with `GetFileName($img)` too. So `26182-Denim-801/1.jpg` and
   `foldercontainsID99984905/1.jpg` collide on `1.jpg` — the second is silently dropped — and whichever
   survives arrives with no folder at all.
2. **The core zip reader strips member folders.** `ZipHandler.cs:190` sets
   `originalFileName = Path.GetFileName(memberPath)`, so a ZIP member's `InitialFullName` is always a
   bare filename. Since the runner routes everything except one seed image through a repacked ZIP,
   folder structure would be lost even if cause 1 were fixed.
3. **`MeaningfulTokens` doesn't split `foldercontainsID99984905`** — see §2.3.

`FolderNameEnricher` needs `InitialFullName` to contain a slash (`GetFolderPath` returns null
otherwise), so it can never fire through this path.

**Effect:** a run over JBComplete today silently drops 2 images and reports the other 7 as unmatched.
That looks like correct behaviour for `99984901` and like a matcher bug for the other two folders,
when neither is true. Listing those 9 in the match golden with `FamilyId: null` would bake the harness
defect in as if it were the right answer, so they are held out. They **are** labelled in
`expected-phenotype.json`, and adding them to the match golden is the last step of [[T-5020]].

### 4.3 `expected-phenotype.json` — 99 entries

`SourceReference` → `Phenotype`, with optional `Confidence` and `Note`. **Nothing reads this file yet**
— there is no phenotype assertion mode in `Invoke-CiPipeline.ps1`. It exists so the M11 confusion
matrix can be computed instead of eyeballed.

- 20 of 21 phenotypes have at least one positive case.
- **How ghost was told apart from flat-lay**, since it decided 12 labels and it was wrong at first
  pass. At 400 px thumbnails the six Vingino garments read as flat and were labelled `*-packshot`.
  What forced a re-check was `ghost-side` having zero coverage while two files named "DETAIL" showed
  *whole* garments from the side — an odd thing for a detail shot. Re-rendered at ~700 px, three cues
  separate them: the waistband holds an open rounded form instead of collapsing to two flat edges,
  the legs carry internal volume, and there are shadows *inside* the garment opening. None of those
  survive a flat lay, where fabric pools into creases. That is visual judgement at adequate
  resolution, not a method — which is why 16 rows are still marked low-confidence.
- **16 entries are marked `"Confidence": "low"`** — those are a reading of a downscaled thumbnail, not
  domain truth, and need a human pass before any number derived from them is trusted. They concentrate
  in front-vs-back on flat garments, side-vs-back on turned models, and the underwear multipack.
- 3 entries have `"Phenotype": null` — the picture genuinely fits no slot in the taxonomy
  (`100267_5`, a product exploded into two parts; `100267_6` and `100267_7`, marketing infographics).
- The subfolder images **are** labelled here even though they cannot be scored yet, so they become
  usable the moment §4.2 is fixed.

---

### 4.4 What a human review of these files should actually cover

Most of this README verifies itself — sheet validity, folder tokenization and the file-level facts are
all re-runnable, so reading them gains nothing. Only 36 rows and two paragraphs encode a judgement
that no test can catch if it is wrong:

1. **The 20 `FamilyId: null` rows in `expected-match.json`** — not the 70 matches, which follow from
   reference numbers. Two of the rejections are contestable product calls. `OMB-E129-TGV_*` is held
   unmatched on the reasoning that a wrong reference must not be rescued by colour/type/brand
   agreement, or every green bag matches every green-bag family. `4471-2290-*` is held unmatched
   because the reference sits under two FamilyIDs across two sheets.
2. **The 16 `"Confidence": "low"` rows in `expected-phenotype.json`** — filter on that key.
3. **Two intent inferences in §2.2**: that the leetspeak files and `T_SHIRT_EGRET_DETAIL.jpg` all
   belong to the polo `98226808` rather than the t-shirt `98226972`, and that `triggered-mistery.jpg`
   belongs to the Crème row via the "LEMON" print on the garment.

Worth holding that review until [[T-5020]] lands — it adds 9 rows to the match golden and may change
how the folder images read.

## 5. What still blocks a scored run

1. **The folder cases are unreachable** — [[T-5020]], §4.2. Highest priority of the three.
2. **The low-confidence phenotype labels need a human pass** (§4.3). 16 of 99.
3. **A phenotype measurement must handle the filename-keyword problem first** (§3), or the number it
   produces describes the filenames rather than the model.
4. **[[T-3800]] is still not unblocked.** Its Bracket-3 fuzzy-colour case now has a live instance
   here (§6.3), but **no image in JBComplete reaches Bracket 4**, which was T-3800's other blocker.
   Building the Bracket-4 case from §6.1 is a separate piece of work.
5. **Two families have no images** (`98226972`; `98636325` only via the near-miss `E129` files) and
   **one folder has no family** (`99984901`). That is intended, but a run will report it and someone
   will file it as a bug unless it is written down. It is now.

---

## 6. Salvage from the `Products` sheet

That sheet was generated by an agent, so it is not authoritative and it contains hallucinated detail
(image files that were never made, a `WebsiteImageLink` pointing at nothing). But its `Notes` column
describes matcher behaviour in more detail than anything else in the repo. Sorted below by whether the
case is worth building.

### 6.1 Worth building — a real mechanism nothing currently tests

| Case as described | The mechanism it tests | Why it is not covered today |
|---|---|---|
| `4471-2290.jpg` is the only product containing **both** `4471` and `2290`; three decoys make `4471` alone ambiguous and two make `2290` alone ambiguous | Does the matcher **intersect** evidence across several numeric tokens, or reject as soon as one token is ambiguous? | JBComplete's live `4471-2290-*` files hit a different case — the *whole* reference exists under two FamilyIDs. The decoy structure that makes each half ambiguous does not exist. |
| `grey-scarf.jpg` vs a product whose COLOR is the US spelling `gray`; counter-case `graphite` too far to match, **and** `grey` appearing only in free-text description must not count | Bracket-3 fuzzy categorical matching — and the guardrail that a fuzzy hit only counts in a *categorical* column, not anywhere the word happens to appear | This is [[T-3800]]'s Bracket-3 blocker. The free-text guardrail half is the subtle part and is not tested anywhere. |
| `IMG_7710.jpg` tuned to sit on the accept/reject line, with two decoy products deliberately left unmatched at the moment it is scored | Bracket 4, and specifically the old "count of other unmatched products" scoring bug | [[T-3800]] item 4 is blocked precisely because **0 of the 14 CiMini goldens reach Bracket 4**. JBComplete adds no Bracket-4 case either. This is the one gap the new dataset did not close. |
| `IMG_9021.jpg` has no filename connection; it is the only unmatched red dress left, so it matches confidently | Assignment by elimination — a decision that depends on what else is still unmatched, not on this image alone | A different kind of matching from anything PRISM does per-image. Worth establishing whether it should exist at all. |
| `5521-red.jpg` (number + colour agree) and `5521.jpg` (number only) resolve to the **same** product at **different** confidence | Confidence reflects evidence strength, not just accept/reject | Nothing measures confidence gradation on a fixed outcome. Directly relevant to [[T-2600]]. |
| `sweater-detail.jpg` shares only the word "sweater" but should inherit the family its siblings already matched | `SiblingPropagator` positive case | JBComplete has the **counter**-case (`triggered-mistery.jpg`, which shares tokens with all three Triggered rows and must be refused) but no positive case. |
| `blazer-2201.jpg` ties two products; the rejection must **name both** | KO diagnostic quality — the manifest should record which families collided, not just that something did | JBComplete has three genuine ambiguity rejections (`133726012`, `87186790_*`, `4471-2290-*`). If the KO payload carries candidate IDs, they already test it; if it does not, that is the finding. |

### 6.2 Already covered by real data in this dataset

- **Non-clothing default product type** (`DF001`, a ceramic vase that resolves to no clothing type) —
  covered for real by `91337133`, the MSV trash can (NGP `Office supplies`, Type `Trash Can`).
- **Two images competing for one det slot** (`jacket-flatlay-1/2.jpg`) — covered by
  `OMB-E181-CVW_5` and `_6`, both interior shots of the same bag, and by
  `AY_FFK0230_83035_02FW001_A` and `_06FW001_f`, both diagonal.
- **Filename recorded verbatim in a cell** (`photo_final_2.jpg` in `WebsiteImageLink`) — covered by
  the `100267_*` files, whose only link to `91337133` is the URL text in the marketing description.
- **Digits hidden inside a barcode** (`BC001`/`BC002`) — covered by `87186790_1/2`, whose prefix sits
  inside two different EANs.
- **Folder reference inheritance with sibling decoys** (`23456-red-tote` + two siblings) — covered by
  the three real subfolders, which satisfy `minPerItemSiblings: 2`.
- **Minimum distinct tokens for Bracket 3** (`blue-hoodie.jpg` accepts on 2 words, `blue.jpg` must not
  on 1) — this is `bracket3MinDistinctTokens: 2`, already unit-tested in `MatcherUpgradeTests`.

### 6.3 A live fuzzy-colour case nobody planned

Worth flagging because it appeared by accident and is sharper than the designed one. The Vingino
filenames say **`Grey`**; family `99147533` carries Color **`Gray`** — an edit distance of 1, exactly
the Bracket-3 fuzzy case. But those files belong to `99147525` by reference number, and `99147525`'s
Color cell says `Twill sand`. So the fuzzy colour match and the exact reference match **point at
different families**. That is a stronger test than `grey-scarf.jpg` would have been: it does not just
ask whether fuzzy colour matching works, it asks whether it can override a reference.

### 6.4 Where the stray `FW001` / `BW001` tokens in the filenames come from

`AY_FFK0230_83035_01 FW001_b.png`, `100267_6  - BW001_c.jpg` and friends carry tokens from that
sheet's family scaffolding — `TW001` topwear, `BW001` bottomwear, `FW001` footwear, `BA001` bags,
`DF001` default. Those rows never load, so **the tokens resolve to nothing**. They are noise, not
evidence, and a matcher that reacts to them is over-reaching. Leave them in place; they are a
free negative test.

### 6.5 Filenames the sheet names that do not exist

`green-sweater-front/back.jpg`, `sweater-detail.jpg`, `sweater-mystery.jpg`, `grey-sweater-main.jpg`,
`grey-scarf.jpg`, `blue-hoodie.jpg`, `blue.jpg`, `jacket-flatlay-1/2.jpg`, `IMG_9021.jpg`,
`IMG_7710.jpg`, `photo_final_2.jpg`, `5521-red.jpg`, `5521.jpg`, `blazer-2201.jpg`, and the folders
`23456-red-tote` / `23457-blue-tote` / `23458-black-bag`.
