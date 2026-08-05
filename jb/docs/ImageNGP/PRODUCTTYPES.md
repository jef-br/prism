# Product Type Catalog

> **STALE (2026-07-27, T-4700 + follow-up ticket):** this catalog predates two collapses and has
> not been reconciled with either. (1) 6 phenotypes (`packaging-shot`, `scale-reference-shot`,
> `size-chart`, `sitting-on-model`, `flatlay-front`, `flatlay-styled`) were removed — every
> section below still references some of them. (2) `DetOrderRules.json`/`ProductTypeMap.json`
> were collapsed from 19 product types (`default` + 18 bespoke) to 5 (`default`, `topwear` [was `clothing-tops`],
> `bottomwear` [merged `clothing-bottoms`+`clothing-dresses`], `footwear`, `bags-accessories`) —
> the other 13 sections here (`clothing-outerwear`, `fmcg-*`, `beauty-cosmetics`,
> `electronics-*`, `homeware-*`, `toys-children`, `diy-tools`, `gardening`,
> `sports-equipment`, `furniture`) no longer have a corresponding `DetOrderRules.json` table —
> those product types now use `default`. The "DetOrderRules.json is indicative only, this
> catalog is authoritative" architecture decision below is also no longer how this actually
> works in practice — the two live config files (`DetOrderRules.json`, `ProductTypeMap.json`)
> are the current source of truth; treat this catalog as historical context pending a full
> rewrite, not as an instruction to follow.

This catalog defines common e-commerce product types, the expected 8-image sequence per type, and the acceptable image phenotypes per `DetOrder` slot.

All phenotype references use ids from `imagePhenotypes.md`.
DetOrder slot keywords in brackets `[keyword]` cross-reference `DetOrderRules.json` role keywords.

## Conventions

**Ordering preferences (universal)**
1. Human → artificial: on-model > packshot > render
   (This ladder used to read `on-model > ghost > flat-lay > clipping-path packshot > render`.
   `clipping-path` went in T-5030 and the ghost rung went in T-5040 — PRISM has no measurable
   signal separating a ghost-mannequin shot from a flat lay, so `packshot` now covers both.)
2. Occlusion: full-product > mostly-visible > partially-occluded > closeup
3. Background: natural/studio (neutral) > flat/white > lifestyle/ambiance
4. Within same preference tier: front > diagonal > side > back > top > bottom

Each slot lists primary phenotypes in preference order, separated by ` | `.
`[keyword]` after a slot links to the `DetOrderRules.json` role keyword for this slot.

**Orientation vocabulary:** `front`, `front-3-quarter`, `side`, `rear`, `top`, `bottom`, `interior`, `detail-view`
**Occlusion vocabulary:** `full-product`, `mostly-visible`, `partially-occluded`
**Presentation style vocabulary:** `on-model`, `packshot` (covers flat-lay *and* ghost-mannequin), `detail`, `ambiance`, `lifestyle`, `scale-reference`, `illustration-technical-drawing`

---

## Product type index

| # | Product type id | Common name |
|---|---|---|
| 1 | `clothing-tops` | T-shirts, shirts, blouses, sweaters, hoodies |
| 2 | `clothing-bottoms` | Trousers, jeans, skirts, shorts |
| 3 | `clothing-outerwear` | Jackets, coats, blazers |
| 4 | `clothing-dresses` | Dresses, jumpsuits, overalls |
| 5 | `footwear` | Shoes, boots, sneakers, sandals |
| 6 | `bags-accessories` | Handbags, backpacks, wallets, belts |
| 7 | `fmcg-packaged-food` | Packaged food & beverages (bottle, can, box) |
| 8 | `fmcg-personal-care` | Shampoo, skincare, cosmetics, deodorant |
| 9 | `beauty-cosmetics` | Makeup, fragrance, nail products |
| 10 | `electronics-small` | Headphones, cables, small devices, accessories |
| 11 | `electronics-large` | Laptops, tablets, monitors, cameras |
| 12 | `homeware-soft` | Cushions, throws, towels, bed linen |
| 13 | `homeware-hard` | Kitchenware, crockery, glassware, storage |
| 14 | `toys-children` | Action figures, dolls, board games, learning toys |
| 15 | `diy-tools` | Power tools, hand tools, fixings |
| 16 | `gardening` | Plants, garden tools, pots, outdoor furniture |
| 17 | `sports-equipment` | Bikes, rackets, weights, protective gear |
| 18 | `furniture` | Chairs, tables, shelving, beds |

---

## 1. `clothing-tops`
T-shirts, shirts, blouses, polo shirts, sweaters, hoodies.

```
det0 [front]     : front-on-model-full-product | ghost-front | front-packshot
det1 [back]      : back-on-model-full-product | ghost-back | back-packshot
det2 [side]      : side-on-model | ghost-side | side-packshot
det3 [detail]    : closeup-image
det4 [lifestyle] : lifestyle-hero | flatlay-styled
det5 [front]     : front-on-model-partial | flatlay-front
det6 [label]     : closeup-image | packaging-shot
det7 [material]  : closeup-image | illustration-technical-drawing
```

| Slot | Orientation | Occlusion | Presentation style | Notes |
|---|---|---|---|---|
| det0 | front | full-product | on-model > on-ghost > packshot | Primary hero; prefer human first |
| det1 | rear | full-product | on-model > on-ghost > packshot | Back labels, print, construction |
| det2 | side | full-product | on-model > on-ghost > packshot | Silhouette, sleeve length |
| det3 | detail-view | partially-occluded | detail | Fabric, print, or hardware close-up |
| det4 | front | full-product | lifestyle > flat-lay | Brand story / style context |
| det5 | front | mostly-visible | on-model > flat-lay | Alternate angle or folded flat |
| det6 | detail-view | mostly-visible | detail | Wash/care label or branding tag |
| det7 | detail-view | mostly-visible | detail | Material or stitching close-up |

---

## 2. `clothing-bottoms`
Trousers, jeans, chinos, shorts, leggings, skirts.

```
det0 [front]     : front-on-model-full-product | ghost-front | front-packshot
det1 [back]      : back-on-model-full-product | ghost-back | back-packshot
det2 [side]      : side-on-model | ghost-side | side-packshot
det3 [detail]    : closeup-image
det4 [front]     : front-on-model-partial | flatlay-front
det5 [lifestyle] : lifestyle-hero | flatlay-styled
det6 [label]     : closeup-image | packaging-shot
det7 [material]  : closeup-image | illustration-technical-drawing
```

| Slot | Orientation | Occlusion | Presentation style | Notes |
|---|---|---|---|---|
| det0 | front | full-product | on-model > on-ghost > packshot | Include full leg to waist |
| det1 | rear | full-product | on-model > on-ghost > packshot | Back pockets, waistband label |
| det2 | side | full-product | on-model > on-ghost > packshot | Rise, fit silhouette |
| det3 | detail-view | partially-occluded | detail | Pocket, rivet, zipper |
| det4 | front | mostly-visible | on-model > flat-lay | Alternate pose or folded |
| det5 | front | full-product | lifestyle | Styled outfit |
| det6 | detail-view | mostly-visible | detail | Waistband / size label |
| det7 | detail-view | mostly-visible | detail | Denim weave / fabric |

---

## 3. `clothing-outerwear`
Jackets, coats, parkas, blazers, gilets.

```
det0 [front]     : front-on-model-full-product | ghost-front | front-packshot
det1 [back]      : back-on-model-full-product | ghost-back | back-packshot
det2 [side]      : side-on-model | ghost-side | side-packshot
det3 [detail]    : closeup-image
det4 [interior]  : interior-shot | closeup-image
det5 [lifestyle] : lifestyle-hero
det6 [front]     : front-on-model-partial | flatlay-front
det7 [label]     : closeup-image | packaging-shot | illustration-technical-drawing
```

| Slot | Orientation | Occlusion | Presentation style | Notes |
|---|---|---|---|---|
| det0 | front | full-product | on-model > on-ghost > packshot | Zipped/buttoned closed |
| det1 | rear | full-product | on-model > on-ghost > packshot | Back yoke, hem, hood |
| det2 | side | full-product | on-model > on-ghost | Side pockets, sleeve |
| det3 | detail-view | mostly-visible | detail | Zipper, button, cuff hardware |
| det4 | interior | mostly-visible | detail | Lining pattern, inner pocket |
| det5 | front | full-product | lifestyle | Outdoor context |
| det6 | front | mostly-visible | on-model | Open/unzipped alt view |
| det7 | detail-view | mostly-visible | detail | Care label or brand label |

---

## 4. `clothing-dresses`
Dresses, midi dresses, maxi dresses, jumpsuits, playsuits.

```
det0 [front]     : front-on-model-full-product | ghost-front | front-packshot
det1 [back]      : back-on-model-full-product | ghost-back | back-packshot
det2 [side]      : side-on-model | ghost-side
det3 [detail]    : closeup-image
det4 [lifestyle] : lifestyle-hero | flatlay-styled
det5 [front]     : front-on-model-partial | flatlay-front
det6 [material]  : closeup-image
det7 [label]     : closeup-image | illustration-technical-drawing
```

| Slot | Orientation | Occlusion | Presentation style | Notes |
|---|---|---|---|---|
| det0 | front | full-product | on-model > on-ghost > packshot | Show full length |
| det1 | rear | full-product | on-model > on-ghost | Back neckline, zipper, drape |
| det2 | side | full-product | on-model > on-ghost | Silhouette, skirt volume |
| det3 | detail-view | partially-occluded | detail | Print, embroidery, closure |
| det4 | front | full-product | lifestyle | Styled/occasion context |
| det5 | front | mostly-visible | on-model > flat-lay | Waist-up or hem detail |
| det6 | detail-view | mostly-visible | detail | Fabric texture |
| det7 | detail-view | mostly-visible | detail | Label |

---

## 5. `footwear`
Shoes, boots, sneakers, sandals, loafers, pumps.

```
det0 [diagonal]  : diagonal-packshot | front-on-model-full-product
det1 [front]     : front-packshot | front-on-model-partial
det2 [side]      : side-packshot | side-on-model
det3 [back]      : back-packshot
det4 [bottom]    : bottom-packshot
det5 [top]       : top-packshot
det6 [detail]    : closeup-image
det7 [lifestyle] : lifestyle-hero | on-model-with-accessories | illustration-technical-drawing
```

| Slot | Orientation | Occlusion | Presentation style | Notes |
|---|---|---|---|---|
| det0 | front-3-quarter | full-product | packshot > on-model | Diagonal best shows shape/volume |
| det1 | front | full-product | packshot > on-model | Toe box, vamp |
| det2 | side | full-product | packshot > on-model | Profile, heel height, silhouette |
| det3 | rear | full-product | packshot | Counter, heel detail |
| det4 | bottom | full-product | packshot | Sole unit, tread pattern |
| det5 | top | full-product | packshot | Insole, collar, lacing |
| det6 | detail-view | mostly-visible | detail | Material, stitching, hardware |
| det7 | front | full-product | lifestyle | Worn in context |

---

## 6. `bags-accessories`
Handbags, tote bags, backpacks, wallets, purses, belts, hats, scarves, sunglasses.

```
det0 [front]     : front-packshot | ghost-front
det1 [side]      : side-packshot
det2 [back]      : back-packshot
det3 [top]       : top-packshot
det4 [interior]  : interior-shot
det5 [detail]    : closeup-image
det6 [lifestyle] : lifestyle-hero | on-model-with-accessories
det7 [label]     : closeup-image | packaging-shot | illustration-technical-drawing
```

| Slot | Orientation | Occlusion | Presentation style | Notes |
|---|---|---|---|---|
| det0 | front | full-product | packshot > on-ghost | Classic front view |
| det1 | side | full-product | packshot | Width, gusset |
| det2 | rear | full-product | packshot | Back pocket, strap attachment |
| det3 | top | full-product | packshot | Opening, handles, top view |
| det4 | interior | mostly-visible | detail | Interior pockets, lining |
| det5 | detail-view | mostly-visible | detail | Hardware, logo, stitching |
| det6 | front | full-product | lifestyle | Worn or styled |
| det7 | detail-view | mostly-visible | detail | Brand label, authenticity mark |

---

## 7. `fmcg-packaged-food`
Packaged food and beverages: bottles, cans, boxes, bags, jars, sachets.

```
det0 [front]     : packaging-shot | front-packshot
det1 [back]      : back-packshot | packaging-shot
det2 [side]      : side-packshot | packaging-shot
det3 [top]       : top-packshot
det4 [detail]    : closeup-image
det5 [pack]      : packaging-shot
det6 [lifestyle] : lifestyle-hero | scale-reference-shot
det7 [label]     : closeup-image | illustration-technical-drawing
```

| Slot | Orientation | Occlusion | Presentation style | Notes |
|---|---|---|---|---|
| det0 | front | full-product | packshot | Primary brand panel, front label |
| det1 | rear | full-product | packshot | Nutrition table, ingredients, barcode |
| det2 | side | full-product | packshot | Volume/weight markings |
| det3 | top | full-product | packshot | Lid/cap, closure type |
| det4 | detail-view | mostly-visible | detail | Ingredients / allergen label close-up |
| det5 | front | full-product | packshot | Full packaging group shot (if multipacks) |
| det6 | front | full-product | lifestyle | Product in use / serving suggestion |
| det7 | detail-view | mostly-visible | detail | Best-before / batch code panel |

---

## 8. `fmcg-personal-care`
Shampoo, conditioner, shower gel, deodorant, sunscreen, hand cream, toothpaste.

```
det0 [front]     : packaging-shot | front-packshot
det1 [back]      : back-packshot | packaging-shot
det2 [side]      : side-packshot
det3 [top]       : top-packshot
det4 [detail]    : closeup-image
det5 [pack]      : packaging-shot
det6 [lifestyle] : lifestyle-hero
det7 [label]     : closeup-image | illustration-technical-drawing
```

| Slot | Orientation | Occlusion | Presentation style | Notes |
|---|---|---|---|---|
| det0 | front | full-product | packshot | Brand + product name panel |
| det1 | rear | full-product | packshot | Ingredients, INCI list, instructions |
| det2 | side | full-product | packshot | Capacity, certifications |
| det3 | top | full-product | packshot | Cap/pump detail |
| det4 | detail-view | mostly-visible | detail | Active ingredients label close-up |
| det5 | front | full-product | packshot | Full product range / pack group |
| det6 | front | mostly-visible | lifestyle | Bathroom/shower context |
| det7 | detail-view | mostly-visible | detail | Certification logos or certifications panel |

---

## 9. `beauty-cosmetics`
Makeup (foundation, lipstick, mascara, eyeshadow palette), fragrance, nail polish, skincare serums.

```
det0 [front]     : packaging-shot | front-packshot | diagonal-packshot
det1 [side]      : side-packshot | diagonal-packshot
det2 [top]       : top-packshot
det3 [detail]    : closeup-image
det4 [interior]  : interior-shot
det5 [lifestyle] : lifestyle-hero | on-model-with-accessories
det6 [pack]      : packaging-shot
det7 [label]     : closeup-image | illustration-technical-drawing
```

| Slot | Orientation | Occlusion | Presentation style | Notes |
|---|---|---|---|---|
| det0 | front | full-product | packshot > diagonal | Packaging front / product face |
| det1 | side/diagonal | full-product | packshot | Depth, applicator, cap |
| det2 | top | full-product | packshot | Palette overview, lid open |
| det3 | detail-view | mostly-visible | detail | Shade swatch, texture, brush tip |
| det4 | interior | mostly-visible | detail | Open palette showing shades |
| det5 | front | full-product | lifestyle | Model application or styled shot |
| det6 | front | full-product | packshot | Complete product line / range group |
| det7 | detail-view | mostly-visible | detail | Shade name / ingredient panel |

---

## 10. `electronics-small`
Headphones, earbuds, cables, USB hubs, phone cases, smart watches, portable chargers.

```
det0 [front]     : front-packshot | diagonal-packshot
det1 [side]      : side-packshot | diagonal-packshot
det2 [back]      : back-packshot
det3 [detail]    : closeup-image
det4 [pack]      : packaging-shot
det5 [lifestyle] : lifestyle-hero | scale-reference-shot
det6 [top]       : top-packshot
det7 [label]     : closeup-image | illustration-technical-drawing
```

| Slot | Orientation | Occlusion | Presentation style | Notes |
|---|---|---|---|---|
| det0 | front/diagonal | full-product | packshot | Primary product face — clean white bg |
| det1 | side | full-product | packshot | Thickness, port positions |
| det2 | rear | full-product | packshot | Back design, labels |
| det3 | detail-view | mostly-visible | detail | Connector, button, LED indicator |
| det4 | front | full-product | packshot | Box + contents |
| det5 | front | full-product | lifestyle | In use / worn / connected |
| det6 | top | full-product | packshot | Top-view layout |
| det7 | detail-view | mostly-visible | detail | Model number / CE marks label |

---

## 11. `electronics-large`
Laptops, monitors, tablets, cameras, printers, gaming consoles, TVs.

```
det0 [front]     : front-packshot | diagonal-packshot
det1 [side]      : side-packshot
det2 [back]      : back-packshot
det3 [top]       : top-packshot
det4 [detail]    : closeup-image
det5 [lifestyle] : lifestyle-hero | scale-reference-shot
det6 [pack]      : packaging-shot
det7 [interior]  : interior-shot | closeup-image | illustration-technical-drawing
```

| Slot | Orientation | Occlusion | Presentation style | Notes |
|---|---|---|---|---|
| det0 | front/diagonal | full-product | packshot | Main face — screen on, keyboard visible |
| det1 | side | full-product | packshot | Thinness, port layout |
| det2 | rear | full-product | packshot | Ventilation, label |
| det3 | top | full-product | packshot | Lid/case, overview |
| det4 | detail-view | mostly-visible | detail | Port, key, button detail |
| det5 | front | full-product | lifestyle | Desk / workspace context |
| det6 | front | full-product | packshot | Box contents, accessories included |
| det7 | interior | mostly-visible | detail | Open lid, hinge, internal detail |

---

## 12. `homeware-soft`
Cushions, throws, blankets, towels, bath mats, bed linen sets, curtains.

```
det0 [front]     : flatlay-front | front-packshot | front-on-model-full-product
det1 [detail]    : closeup-image
det2 [lifestyle] : lifestyle-hero | flatlay-styled
det3 [side]      : side-packshot | front-packshot
det4 [top]       : top-packshot | flatlay-front
det5 [pack]      : packaging-shot | scale-reference-shot
det6 [material]  : closeup-image
det7 [label]     : closeup-image | illustration-technical-drawing
```

| Slot | Orientation | Occlusion | Presentation style | Notes |
|---|---|---|---|---|
| det0 | front | full-product | flat-lay > packshot > on-model | Clean studio flat-lay preferred |
| det1 | detail-view | mostly-visible | detail | Fabric close-up, weave, trim |
| det2 | front | full-product | lifestyle | Bed/sofa styled context |
| det3 | side/front | full-product | packshot | Folded presentation |
| det4 | top | full-product | flat-lay | Overhead layout |
| det5 | front | full-product | packshot | Packaged/folded with dimensions |
| det6 | detail-view | mostly-visible | detail | Material texture |
| det7 | detail-view | mostly-visible | detail | Care label |

---

## 13. `homeware-hard`
Kitchenware (pots, pans), crockery, glassware, cutlery, storage containers, vases.

```
det0 [front]     : front-packshot | diagonal-packshot
det1 [top]       : top-packshot
det2 [side]      : side-packshot
det3 [interior]  : interior-shot | top-packshot
det4 [detail]    : closeup-image
det5 [lifestyle] : lifestyle-hero
det6 [pack]      : packaging-shot | scale-reference-shot
det7 [label]     : closeup-image | illustration-technical-drawing
```

| Slot | Orientation | Occlusion | Presentation style | Notes |
|---|---|---|---|---|
| det0 | front/diagonal | full-product | packshot | Show shape and finish |
| det1 | top | full-product | packshot | Opening, interior view |
| det2 | side | full-product | packshot | Handle, depth, profile |
| det3 | interior | mostly-visible | detail | Inside — capacity, coating |
| det4 | detail-view | mostly-visible | detail | Material, glaze, handle joint |
| det5 | front | full-product | lifestyle | Kitchen/table styled context |
| det6 | front | full-product | packshot/scale-reference | Size reference included |
| det7 | detail-view | mostly-visible | detail | Brand/certification mark |

---

## 14. `toys-children`
Action figures, dolls, plush toys, board games, puzzles, construction sets, learning toys.

```
det0 [front]     : front-packshot | diagonal-packshot | packaging-shot
det1 [back]      : back-packshot | packaging-shot
det2 [side]      : side-packshot
det3 [detail]    : closeup-image
det4 [pack]      : packaging-shot
det5 [lifestyle] : lifestyle-hero | scale-reference-shot
det6 [top]       : top-packshot | flatlay-front
det7 [label]     : closeup-image | packaging-shot | illustration-technical-drawing
```

| Slot | Orientation | Occlusion | Presentation style | Notes |
|---|---|---|---|---|
| det0 | front | full-product | packshot > packaging | Product face or box front |
| det1 | rear | full-product | packshot > packaging | Box back with feature info |
| det2 | side | full-product | packshot | Box side panel |
| det3 | detail-view | mostly-visible | detail | Articulation joint, accessory, feature |
| det4 | front | full-product | packshot | Contents laid out |
| det5 | front | full-product | lifestyle/scale-ref | Child playing (scale reference) |
| det6 | top | full-product | flat-lay | Components laid out flat |
| det7 | detail-view | mostly-visible | detail | Age rating / safety label |

---

## 15. `diy-tools`
Power tools, hand tools, fixings (screws, bolts, brackets), adhesives, measuring instruments.

```
det0 [front]     : front-packshot | diagonal-packshot
det1 [side]      : side-packshot
det2 [back]      : back-packshot
det3 [detail]    : closeup-image
det4 [pack]      : packaging-shot
det5 [lifestyle] : lifestyle-hero | scale-reference-shot
det6 [top]       : top-packshot
det7 [label]     : closeup-image | illustration-technical-drawing
```

| Slot | Orientation | Occlusion | Presentation style | Notes |
|---|---|---|---|---|
| det0 | front/diagonal | full-product | packshot | Show form factor clearly |
| det1 | side | full-product | packshot | Grip, length, switch position |
| det2 | rear | full-product | packshot | Chuck, tail, battery compartment |
| det3 | detail-view | mostly-visible | detail | Blade, bit, chuck, safety |
| det4 | front | full-product | packshot | Kit contents or accessories |
| det5 | front | full-product | lifestyle/scale-ref | In-use context, scale |
| det6 | top | full-product | packshot | Top view layout |
| det7 | detail-view | mostly-visible | detail | CE/safety/spec label |

---

## 16. `gardening`
Plants, seed packets, garden tools, pots and planters, garden furniture, compost, pest control.

```
det0 [front]     : front-packshot | packaging-shot | lifestyle-hero
det1 [side]      : side-packshot
det2 [top]       : top-packshot | flatlay-front
det3 [detail]    : closeup-image
det4 [lifestyle] : lifestyle-hero
det5 [pack]      : packaging-shot | scale-reference-shot
det6 [back]      : back-packshot | packaging-shot
det7 [label]     : closeup-image | illustration-technical-drawing
```

| Slot | Orientation | Occlusion | Presentation style | Notes |
|---|---|---|---|---|
| det0 | front | full-product | packshot > lifestyle | Plants: natural setting preferred |
| det1 | side | full-product | packshot | Height/form profile |
| det2 | top | full-product | packshot > flat-lay | Top growth view or overhead tool |
| det3 | detail-view | mostly-visible | detail | Leaf detail, seed, material texture |
| det4 | front | full-product | lifestyle | Garden/outdoor context |
| det5 | front | full-product | scale-reference | Mature size reference |
| det6 | rear | full-product | packshot/packaging | Growing instructions, ingredients |
| det7 | detail-view | mostly-visible | detail | Label / care guide close-up |

---

## 17. `sports-equipment`
Bikes, rackets, weights, dumbbells, yoga mats, helmets, protective gear, balls.

```
det0 [front]     : front-packshot | diagonal-packshot
det1 [side]      : side-packshot
det2 [back]      : back-packshot
det3 [detail]    : closeup-image
det4 [lifestyle] : lifestyle-hero | front-on-model-full-product
det5 [pack]      : packaging-shot | scale-reference-shot
det6 [top]       : top-packshot
det7 [label]     : closeup-image | illustration-technical-drawing
```

| Slot | Orientation | Occlusion | Presentation style | Notes |
|---|---|---|---|---|
| det0 | front/diagonal | full-product | packshot | Primary form factor |
| det1 | side | full-product | packshot | Profile, size, design |
| det2 | rear | full-product | packshot | Back panel, adjustment |
| det3 | detail-view | mostly-visible | detail | Material, grip, joint |
| det4 | front | full-product | lifestyle/on-model | In sport context or worn |
| det5 | front | full-product | packshot/scale-ref | Weight / dimension reference |
| det6 | top | full-product | packshot | Top view |
| det7 | detail-view | mostly-visible | detail | Certification / spec label |

---

## 18. `furniture`
Chairs, tables, sofas, shelving, beds, storage furniture, desks.

```
det0 [front]     : front-packshot | diagonal-packshot | lifestyle-hero
det1 [side]      : side-packshot
det2 [back]      : back-packshot
det3 [top]       : top-packshot
det4 [detail]    : closeup-image
det5 [lifestyle] : lifestyle-hero
det6 [pack]      : packaging-shot | scale-reference-shot
det7 [label]     : closeup-image | interior-shot | illustration-technical-drawing
```

| Slot | Orientation | Occlusion | Presentation style | Notes |
|---|---|---|---|---|
| det0 | front/diagonal | full-product | packshot > lifestyle | Room setting preferred for premium |
| det1 | side | full-product | packshot | Depth, seat height |
| det2 | rear | full-product | packshot | Back structure, leg design |
| det3 | top | full-product | packshot | Surface, seat top |
| det4 | detail-view | mostly-visible | detail | Joint, leg tip, fabric, wood grain |
| det5 | front | full-product | lifestyle | Styled room context |
| det6 | front | full-product | packshot/scale-reference | Flat-pack contents or dimensions |
| det7 | detail-view | mostly-visible | detail | Care/maintenance label |

---

## Phenotype cross-reference

All phenotype ids used in this catalog are defined in `imagePhenotypes.md`.
Features required to detect these phenotypes are defined in `ImageFeatures.md`.

| Phenotype id | Used by product types |
|---|---|
| `front-packshot` | all |
| `back-packshot` | clothing-tops, clothing-bottoms, clothing-outerwear, clothing-dresses, footwear, bags-accessories, fmcg-packaged-food, fmcg-personal-care, beauty-cosmetics, electronics-small, electronics-large, homeware-hard, toys-children, diy-tools, gardening, sports-equipment, furniture |
| `side-packshot` | clothing-tops, clothing-bottoms, footwear, bags-accessories, fmcg-packaged-food, fmcg-personal-care, electronics-small, electronics-large, homeware-hard, diy-tools, gardening, sports-equipment, furniture |
| `diagonal-packshot` | footwear, bags-accessories, beauty-cosmetics, electronics-small, electronics-large, diy-tools, sports-equipment, furniture |
| `top-packshot` | footwear, bags-accessories, fmcg-packaged-food, fmcg-personal-care, electronics-small, electronics-large, homeware-hard, toys-children, diy-tools, sports-equipment, furniture |
| `bottom-packshot` | footwear |
| `front-on-model-full-product` | clothing-tops, clothing-bottoms, clothing-outerwear, clothing-dresses, footwear, sports-equipment |
| `front-on-model-partial` | clothing-tops, clothing-bottoms, clothing-outerwear, clothing-dresses |
| `back-on-model-full-product` | clothing-tops, clothing-bottoms, clothing-outerwear, clothing-dresses |
| `side-on-model` | clothing-tops, clothing-bottoms, clothing-outerwear, clothing-dresses, footwear |
| `ghost-front` | clothing-tops, clothing-bottoms, clothing-outerwear, clothing-dresses, bags-accessories |
| `ghost-back` | clothing-tops, clothing-bottoms, clothing-outerwear, clothing-dresses |
| `ghost-side` | clothing-tops, clothing-bottoms, clothing-outerwear, clothing-dresses |
| `flatlay-front` | clothing-tops, clothing-bottoms, homeware-soft, toys-children, gardening |
| `flatlay-styled` | clothing-tops, clothing-bottoms, clothing-dresses, homeware-soft |
| `closeup-image` | all |
| `packaging-shot` | clothing-tops, clothing-bottoms, clothing-outerwear, bags-accessories, fmcg-packaged-food, fmcg-personal-care, beauty-cosmetics, electronics-small, electronics-large, homeware-soft, homeware-hard, toys-children, diy-tools, gardening, sports-equipment, furniture |
| `lifestyle-hero` | clothing-tops, clothing-bottoms, clothing-outerwear, clothing-dresses, footwear, bags-accessories, fmcg-packaged-food, fmcg-personal-care, beauty-cosmetics, homeware-soft, homeware-hard, toys-children, sports-equipment, furniture, gardening |
| `interior-shot` | clothing-outerwear, bags-accessories, beauty-cosmetics, electronics-large, homeware-hard, furniture |
| `scale-reference-shot` | fmcg-packaged-food, homeware-soft, toys-children, diy-tools, gardening, sports-equipment, furniture |
| `on-model-with-accessories` | bags-accessories, beauty-cosmetics, footwear |
| `illustration-technical-drawing` | all |

---

## Architecture decisions

- **ProductType authority**: Excel is authoritative for ProductType. `product-type-label` provides error-checking and supporting evidence; it becomes the authority only when Excel does not supply a ProductType value. Multiple ProductTypes may share one ImageFeature grouping (example: sweater, hoodie, pullover, jacket, short coat, vest, cardigan all map to `topwear-short`).
- **DetOrderRules.json structure**: The current content of `DetOrderRules.json` is indicative only — it demonstrates the preferred JSON structure. Per-product-type det slot preferences are defined in this catalog and should be used as the authoritative source when implementing `DetOrderRules.json`.
- **`illustration-technical-drawing`**: Covers exploded views, technical drawings, EU energy labels, multi-angle composites, vector drawings, icons, and badges. Always assigned the last configured DetOrder slot for any product type.
- **det0 orientation**: Frontal orientation is required for det0 in all product types. Fallback order when frontal is unavailable: FRONT → SIDE → DIAGONAL. Back, top, and bottom orientations do not qualify for det0.
- **Product-type-specific overrides**: No meta-override mechanism is needed. Per-product-type DetOrderRules entries already capture all variation (e.g., furniture det0 preferring `lifestyle-hero` over a pure packshot).
- **Dual-slot risk**: Little to no risk of one image mapping to two slots or two images mapping to one slot. The phenotype qualification and preference-ordering system handles assignment deterministically.
