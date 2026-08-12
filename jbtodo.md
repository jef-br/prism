
- [x] `6318-5274.jpg` → green cable-knit sweater, flat packshot on white
- [x] `grey-scarf.jpg` → gray knitted scarf, packshot, no reference number anywhere in the filename
- [x] `graphite-scarf.jpg` → dark graphite knitted wrap scarf → must NOT resolve to 96000007 (`graphite` is too far from `gray`); resolving to 96000008 on its exact Color is correct
- [x] `charcol-wrap.jpg` → plain charcoal-toned wrap scarf → must NOT match: `charcol` is one letter off `charcoal`, which appears only in 96000008's free-text `Description`, in no categorical column
- [x] `blue.jpg` → plain blue garment packshot → must NOT match (one categorical token only)
- [x] `blue-hoodie.jpg` → blue zip hoodie packshot → must match 96000009 (`blue` + `hoodie`)
- [x] `IMG_9021.jpg` → red wrap dress on a hanger → picture alone should confidently match 96000011, the only unmatched red dress (`IMG` is a configured camera prefix, so the filename carries nothing)
- [x] `IMG_2619_indigo.png` → blue jeans, flat, ambiguous cut → one categorical token (`indigo`) is below `bracket3MinDistinctTokens: 2` and the picture is not decisive → must end up KO, not a forced guess
- [x] `IMG_7710.jpg` → denim jacket shot so the picture gives only partial help, sitting on the accept/reject line against 96000013 while 96000014 and 96000015 are still unmatched
- [x] `green-sweater-front.jpg` → green cable sweater, front packshot (seed match on `green` + `sweater`)
- [x] `green-sweater-back.jpg` → same sweater, back packshot (seed match)
- [x] `sweater-detail.jpg` → close-up of the same sweater's knit → shares only `sweater` → must inherit 96000016 via sibling propagation
- [x] `5283-410.jpg` → red crew-neck t-shirt packshot → filename carries 96000017's reference and the visible color matches its `Color` cell
- [x] `90861083_e.jpg` → flat line drawing of the sweater with measurement call-outs, black lines on white, no photographic content (needs `is-illustration=true`)
- [x] `90861071_b.jpg` → single flat packshot of the scarf; the scarf's own pattern is symmetric so this one photo alone can't reveal front vs. back → must NOT produce a confident `hero-orientation`
- [ ] pareo shadow-pair swapped to a different family — pareos are hard to shoot cleanly; TBD which family, see note below
- [x] `OMB-E166-BV_1-front.jpg` → byte-identical duplicate of `OMB-E166-BV_1.jpg`, filename says `front`
- [x] `OMB-E166-BV_1-back.jpg` → byte-identical duplicate of `OMB-E166-BV_1.jpg`, filename says `back`


---

ALL EXCEL DATA HAS BEEN ADDED TO Brackets-Complete.xlsx



---

bracket # CiMini dataset needs full per-bracket coverage (raised 2026-07-17)

All Excel rows below are new rows on the **`JB-generated`** sheet of
`test/datasets/CiMini/Brackets-Complete.xlsx`. Column names are that sheet's headers verbatim
(`Family ID`, `Reference`, `EAN`, `Label`, `NGP`, `Full name`, `Brand`, `Type of product`,
`Collection name`, `Composition (% of material)`, `Description`, `Material`, `Color`, `Pattern`, …).
`Family ID` must be 8 characters, all digits (`ExcelConfig.json`), so the `96000001+` block below is
used — every value is verified absent from the workbook and from every filename in the folder.

**Two numbers in the filename, each ambiguous alone, only their combination picks one product**
- Images:
- [x] `6318-5274.jpg` → green cable-knit sweater, flat packshot on white
- Excel:
- [x] Family ID=96000001, Reference=6318-5274, NGP=SWEATER AND CARDIGAN, Type of product=SWEATER, Color=Green, Brand=ZARA → the only row containing both fragments
- [x] Family ID=96000002, Reference=6318-7392, NGP=SWEATER AND CARDIGAN, Type of product=SWEATER, Color=Grey, Brand=ZARA → decoy, `6318` only
- [x] Family ID=96000003, Reference=6417-6318, NGP=COAT AND JACKET, Type of product=COAT, Color=Navy, Brand=ZARA → decoy, `6318` only
- [x] Family ID=96000004, Reference=6318-4903, NGP=TOP, Type of product=SHIRT, Color=White, Brand=ZARA → decoy, `6318` only
- [x] Family ID=96000005, Reference=5274-5836, NGP=COAT AND JACKET, Type of product=COAT, Color=Beige, Brand=ZARA → decoy, `5274` only
- [x] Family ID=96000006, Reference=7215-5274, NGP=TROUSERS, Type of product=PANTS, Color=Black, Brand=ZARA → decoy, `5274` only
- Constraint: the case cannot reuse `4471`/`2290` — the existing `4471-2290-*` files hit a whole-reference
  collision across two sheets and KO as ambiguous before any intersection is attempted.

**A filename word that's a typo/spelling variant of a color, material, or product-type word**
- Images:
- [x] `grey-scarf.jpg` → gray knitted scarf, packshot, no reference number anywhere in the filename
- [x] `graphite-scarf.jpg` → dark graphite knitted wrap scarf → must NOT resolve to 96000007 (`graphite` is too far from `gray`); resolving to 96000008 on its exact Color is correct
- [x] `charcol-wrap.jpg` → plain charcoal-toned wrap scarf → must NOT match: `charcol` is one letter off `charcoal`, which appears only in 96000008's free-text `Description`, in no categorical column
- Excel:
- [x] Family ID=96000007, Reference=3820-771, NGP=SCARF, Type of product=SCARF, Color=gray, Brand=ZARA → target of `grey-scarf.jpg`; US spelling, edit distance 1 from the filename word
- [x] Family ID=96000008, Reference=3820-772, NGP=SCARF, Type of product=SCARF, Color=graphite, Brand=ZARA, Description=A charcoal, grey-toned wrap for cool evenings → free-text `grey` and `charcoal` that must not count as categorical hits

**A filename with only one matching word, where two matching words are normally required**
- Images:
- [x] `blue.jpg` → plain blue garment packshot → must NOT match (one categorical token only)
- [x] `blue-hoodie.jpg` → blue zip hoodie packshot → must match 96000009 (`blue` + `hoodie`)
- Excel:
- [x] Family ID=96000009, Reference=4560-410, NGP=TOP, Type of product=Hoodie, Color=Blue, Brand=ZARA → two-token target
- [x] Family ID=96000010, Reference=5730-330, NGP=TOP, Type of product=Blouse, Color=Blue, Brand=ZARA → the single-token candidate `blue.jpg` must not resolve to

**Bracket 4 (picture-based matching) — need three cases**
- Images:
- [x] `IMG_9021.jpg` → red wrap dress on a hanger → picture alone should confidently match 96000011, the only unmatched red dress (`IMG` is a configured camera prefix, so the filename carries nothing)
- [x] `IMG_2619_indigo.png` → blue jeans, flat, ambiguous cut → one categorical token (`indigo`) is below `bracket3MinDistinctTokens: 2` and the picture is not decisive → must end up KO, not a forced guess
- [x] `IMG_7710.jpg` → denim jacket shot so the picture gives only partial help, sitting on the accept/reject line against 96000013 while 96000014 and 96000015 are still unmatched
- Excel:
- [x] Family ID=96000011, Reference=6840-901, NGP=DRESS, Type of product=Dress, Color=Red, Brand=ZARA → only unmatched red dress at match time
- [x] Family ID=96000012, Reference=7950-220, NGP=TROUSERS, Type of product=Jeans, Color=Indigo Blue, Brand=ZARA → weak partial overlap with `IMG_2619_indigo.png` only
- [x] Family ID=96000013, Reference=3061-115, NGP=COAT AND JACKET, Type of product=Denim Jacket, Color=Blue, Brand=ZARA → edge-case target
- [x] Family ID=96000014, Reference=3061-116, NGP=TOP, Type of product=Denim Vest, Color=Blue, Brand=ZARA → still-unmatched decoy at the moment 96000013 is scored
- [x] Family ID=96000015, Reference=3061-117, NGP=TOP, Type of product=Denim Shirt, Color=Blue, Brand=ZARA → still-unmatched decoy at the moment 96000013 is scored
- Constraint: no image in CiMini reaches Bracket 4 today, which is why filenames here must not carry
  two categorical words — `x-red-dress.jpg` style names resolve at Bracket 3 and never get there.

**A sibling photo that's related but not identical in wording to an already-matched photo**
- Images:
- [x] `green-sweater-front.jpg` → green cable sweater, front packshot (seed match on `green` + `sweater`)
- [x] `green-sweater-back.jpg` → same sweater, back packshot (seed match)
- [x] `sweater-detail.jpg` → close-up of the same sweater's knit → shares only `sweater` → must inherit 96000016 via sibling propagation
- Excel:
- [x] Family ID=96000016, Reference=4172-500, NGP=SWEATER AND CARDIGAN, Type of product=Sweater, Color=Green, Brand=ZARA → the family the detail shot must inherit
- Constraint: the should-NOT-propagate counter-example is already covered by `triggered-mistery.jpg`;
  what is missing is the accept path from a correct seed match.

**A photo whose confidence should get a small boost for two kinds of agreeing evidence**
- Images:
- [x] `5283-410.jpg` → red crew-neck t-shirt packshot → filename carries 96000017's reference and the visible color matches its `Color` cell
- [x] `5283-410-achterkant.jpg` → same t-shirt, back → bonus sibling, not originally requested, carries the same reference so resolves the same way
- Excel:
- [x] Family ID=96000017, Reference=5283-410, NGP=TOP, Type of product=T-shirt, Color=Red, Brand=ZARA → both evidence kinds point here
- Constraint: the convergence bonus fires today only on two wrong matches (`OMB-E129-TGV_1/_2`).

Once the images + Excel rows exist, follow the CiMini README procedure
(`test/datasets/CiMini/README.md`): downscale to ~1024px longest edge, update `Brackets-Complete.xlsx`,
eyeball a verified run, then recapture both goldens via
`pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Match -Dataset CiMini -Capture` and
`-Mode Full -Dataset CiMini -Capture`.


-----

# Phenotype validation needs a purpose-built dataset (raised 2026-07-30, from T-4970)

No Excel rows are needed anywhere in this section — every image hangs off a family that already exists
on `JB-generated`. Each filename must carry that FamilyID so it resolves in an early bracket
(`MatchingService.cs:315` skips `Refine` when `IsKo`), and must otherwise stay free of orientation
keywords (`front`, `back`, `side`, `detail`, …) because `Analyzer_FilenameEvidence` writes
`hero-orientation` straight from them — except the last pair, which carries them on purpose.

**The one phenotype with no positive case anywhere — `illustration-technical-drawing`**
- Images:
- [ ] `90861083_e.jpg` → flat line drawing of the sweater with measurement call-outs, black lines on white, no photographic content (needs `is-illustration=true`)
- Excel: none — reuses family 90861083
- Note: family 90861083 is not empty — `23211008_02_A.jpg`/`23211008_02_B.jpg` are real front/back on-model
  photos of the sweater already in CiMini (`expected-phenotype.json`). "The sweater" in the description refers
  to those, so `90861083_e.jpg` is a genuine sibling illustration, not an invented reference. No extra photos
  needed for this family.

**A near-miss negative for orientation**
- Images:
- [x] `90861071_b.jpg` → single flat scarf packshot, symmetric pattern → must NOT produce a confident `hero-orientation`
- Excel: none — reuses family 90861071
- Note: this is one photo, not a front+back pair — the ambiguity comes from the scarf's own symmetric pattern
  making front/back indistinguishable in a single shot, not from comparing two views. `_b` just follows this
  family's existing `_A`/`_B` sibling-naming convention (`23231096_35_A.jpg` is already in the family).

**A hard-shadow / soft-shadow twin pair ([[T-4945]])**
- Images:
- [x] `2426834-7558_side-packshot_shadowhard.jpg` → chunky sneaker, side profile, hard-edged cast shadow → family 98768768 (FILA sneaker, existing `2426834-7558*` family); more hard-shadow shots may be added later under this same prefix
- [x] `OMB-E181-CVW_2.jpg` → ZOLA bucket bag, strap draped across, soft diffuse shadow at the base → already existing image, family 98636312 (existing ZOLA bag family)
- Excel: none — both reuse existing families, no new rows
- Note: pareo (family 94613033) dropped per your note — hard to shoot cleanly. Not a literal same-product
  twin (unlike every other pair in this file, which reuses one real photo shot twice) — it's two different,
  already-existing products from two different families, one demonstrating a hard-edged shadow, the other a
  soft one.

**Filename-path twins — the only pair that deliberately carries keywords**
- Images:
- [x] `OMB-E166-BV_1-front.jpg` → byte-identical duplicate of `OMB-E166-BV_1.jpg`, filename says `front`
- [x] `OMB-E166-BV_1-back.jpg` → byte-identical duplicate of `OMB-E166-BV_1.jpg`, filename says `back`
- Excel: none — reuses `OMB-E166-BV_1.jpg`, which already resolves to family 98636303 (VICKY), and itself
  serves as the base shot (no orientation keyword in its name).
- Corrected: the base was `OMB-E180-BV_1.jpg`, not `OMB-E166-BV_1.jpg` — E180 is CiMini's deliberate
  "must stay unmatched" decoy bag (same colour/type as E166, no Excel row, must not steal E166's family —
  see `expected-match.json` and README's `OMB-E180-BV` row). Front/back copies of it would never resolve to
  a FamilyID, so the phenotype stage would never run on them (`MatchingService.cs:315` skips `Refine` on KO).
  Swapped to `OMB-E166-BV_1.jpg`, the real VICKY control that does resolve to 98636303. Old `-front`/`-back`
  copies of E180 deleted; E180 itself is untouched.
- Purpose: isolates how much of any orientation result comes from the filename vs. the picture.