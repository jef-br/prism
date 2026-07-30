# CiGolden — phenotype validation dataset (work in progress)

Purpose: give every one of the 20 phenotypes in `ImageRoles.json` at least one real positive case,
so phenotype assignment can be scored instead of guessed at. Raised by [[T-4970]]; the full
image-by-image specification lives in the root `jbtodo.md` under "Phenotype validation needs a
purpose-built dataset".

**Status: candidates only. This is not yet a usable dataset.** `candidates/` holds 22 images pulled
from datasets already in this repo and downscaled to 1024px longest edge. There is no Excel file
yet, so every image would KO on matching, and a KO'd image never reaches phenotype assignment
(`MatchingService.cs:315` skips `Refine` when `IsKo`). Three things are still needed: an Excel with
one row per FamilyID, filenames carrying those FamilyIDs, and a `expected-phenotype.json` ground
truth file.

## Why these images and not SPACINI29

SPACINI29 is 86 images of one model on one white sweep. Measured for T-4970: only **3 of the 20
phenotypes** have a true case in it. Every packshot, ghost, lifestyle and non-human phenotype is
absent by construction. These 22 candidates were chosen to fill exactly those holes.

## Naming rule that constrains every filename here

`Analyzer_FilenameEvidence` writes `hero-orientation` straight from whole-token filename matches
(`front`, `back`, `side`, `top`, `bottom`, `diagonal`, `angle`, …) at **0.75 confidence** — higher
than any CLIP orientation score ever recorded (max 0.582). So a filename containing "front" decides
the orientation before the picture is ever looked at.

The candidate filenames below use phenotype names for human readability, and **several of them
contain those tokens**. They must be renamed to keyword-free names before any measurement run, or
the result measures the filename, not the model. This is the single easiest way to get a
falsely-encouraging number out of this dataset.

## What each candidate image shows

Source is the dataset it was copied from. All are downscaled to 1024px longest edge; all clear the
570px `MinInputSizeInPixels` floor.

| File | Source | What the picture shows | Targets |
|---|---|---|---|
| `01_ghost-or-front-packshot_alpha.png` | FILA94 | Beige tracksuit trousers, front, laid flat, no person. **Transparent PNG — real alpha channel, not white.** | `ghost-front` (alpha ⇒ `clipping-path=true`) |
| `02_ghost-or-back-packshot_alpha.png` | FILA94 | Same trousers photographed from the reverse. Transparent PNG. | `ghost-back` |
| `03_closeup-image_alpha.png` | FILA94 | The waistband and pocket filling the whole frame, garment running off the edges, no person. Transparent PNG. | `closeup-image` |
| `04_front-on-model-full.jpg` | FILA94 | Male model standing square to camera, whole body including shoes, beige studio background, nothing touching an edge. | `front-on-model-full-product` |
| `05_front-on-model-partial.jpg` | FILA94 | Same model, same trousers, framed from chest to shoes. Head is cut off by the top edge. | `front-on-model-partial` |
| `06_back-on-model-partial_NORULE.jpg` | FILA94 | Same model turned fully away, framed from head to knee, cut by the bottom edge. **No rule in the taxonomy fits this image** — that is the point of including it. | nothing (gap case) |
| `07_model-detail-closeup_TRUE.jpg` | FILA94 | Tight shot of the trouser pocket with the model's hand in it. No face, no torso. A genuine detail crop. | `model-detail-closeup` |
| `08_side-packshot_shoe.jpg` | FILA94 | One white sneaker in exact side profile on a plain light background, whole shoe in frame. | `side-packshot` |
| `09_bottom-packshot_sole.jpg` | FILA94 | The sole of the same sneaker facing the camera, plain background. | `bottom-packshot` |
| `10_top-packshot_overhead.jpg` | FILA94 | A pair of sneakers shot straight from overhead, plain background. | `top-packshot` |
| `11_lifestyle-hero.jpg` | MMERO26 | Woman in a light-blue sweater seated against a dark wood wall. Real location, garment clearly the subject. | `lifestyle-hero` |
| `12_lifestyle-context.jpg` | MMERO26 | Woman seated by a lit fireplace in a styled interior. The room is as prominent as the clothing. | `lifestyle-context` |
| `13_front-packshot_whitebg.jpg` | MMERO26 | Sage-green knitted cardigan laid flat, front up, plain white background, clear margin all round, no person. | `front-packshot` |
| `14_back-packshot_whitebg.jpg` | MMERO26 | Cream padded jacket photographed from the reverse, plain white background, clear margin. | `back-packshot` |
| `15_closeup-image_whitebg.jpg` | MMERO26 | Pink knit cuff and diamond stitch filling the frame, running off all edges, no person. | `closeup-image` |
| `16_side-on-model.jpg` | MMERO26 | Woman in a true left-side profile, cream top and beige trousers, plain background, most of the body in frame. | `side-on-model` |
| `17_on-model-with-accessories.jpg` | MMERO26 | Woman wearing a cream sweater and beige trousers **and** holding a large patterned scarf — more than one product in shot. | `on-model-with-accessories` (needs YOLO to report >1 object) |
| `18_front-on-model-full_whitebg.jpg` | MMERO26 | Woman standing square to camera in a striped shirt and beige trousers, whole body including shoes, white background, margin all round. | `front-on-model-full-product` |
| `19_front-on-model-full_clean.jpg` | HEROAUT3 | Model facing camera in black swimwear, whole body head to feet, white sweep, wide margin on all sides. The cleanest full-product case available. | `front-on-model-full-product` |
| `20_back-on-model-full_clean.jpg` | HEROAUT3 | Same setup, model turned fully away. Whole body in frame, back of head only. | `back-on-model-full-product` |
| `21_cropcase_front-partial.jpg` | SPACINI29 | Woman in a camel knit sweater, front-facing, framed from mid-head to hips. Top of head cut off by the frame. The ordinary catalogue crop. | `front-on-model-partial` — today the pipeline calls it `model-detail-closeup` |
| `22_cropcase_back-partial_NORULE.jpg` | SPACINI29 | The same sweater from behind, framed the same way. | nothing (gap case) |

## Still missing — no image in this repo fits

These six need to be supplied. Nothing in FILA94, MMERO26, HEROAUT3, SPACINI29 or the other datasets
covers them.

1. **`diagonal-packshot`** — a single product at a three-quarter angle on a **plain solid**
   background, whole product in frame with a margin. The repo's angled shoe shots all sit on
   patterned or gradient backdrops, which fails the rule's `background-type=SOLIDCOLOR` condition.
2. **`interior-shot`** — the inside of a bag, pocket or lining, shot down into the compartment so
   the interior is the subject. Needed for `interior-detected=true`.
3. **`illustration-technical-drawing`** — a flat technical sketch of a garment, black line on white,
   with measurement call-outs. No photographic content. Needed for `is-illustration=true`.
4. **`front-packshot` on white for a second product type** — a bag or a shoe rather than a garment,
   so the det-slot mapping is exercised on more than one `DetOrderRules` product type.
5. **Low-contrast white-on-white** — a white garment or shoe on a white sweep under flat lighting,
   where the product edge is barely separable from the background. This is [[T-4948]]'s case: the
   detector has an uncharacterised contrast floor around 40 grey levels.
6. **Hard-shadow vs soft-shadow pair** — the same product shot twice, once with a crisp hard shadow
   on the sweep and once with a diffuse soft one. This is [[T-4945]]'s case; the current
   `HardShadowEvidenceFraction` of 0.05 was picked off an unlabelled distribution.

## Before this becomes a real dataset

1. Rename every file to something FamilyID-based and free of orientation keywords.
2. Build the Excel: one row per FamilyID, a description that resolves the intended product type
   through `ProductTypeMap.json`, and a colour column. Follow `test/datasets/CiMini/README.md`.
3. Write `expected-phenotype.json` (`filename → phenotype id`), so the M11 confusion matrix can be
   computed rather than eyeballed. Without it this set measures coverage but not correctness.
4. Verify no image KOs before trusting any number. The T-4970 MMERO26 attempt produced nothing
   because 59 of 60 images KO'd on `MATCHES_MULTIPLE_FAMILYIDS`.
