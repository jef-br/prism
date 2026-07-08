# Analyzer_ProductType

**Status:** implemented - **Wave:** 1 (IEM) - **Writes:** `ImageRecord_LAMBDA.ProductTypeId`

## How it works
Authority order (PRODUCTTYPES.MD: Excel is authoritative):
1. IEM `producttype` column - canonicalized by ModelBuilder from any language ("Product Type", "Tipo di prodotto", "Produkttyp", ...).
2. IEM `ngp` column - client Excel field with product-type semantics. **Unrelated to the ImageNGP phenotype taxonomy.**
3. CLIP `product-type-label` feature - image-derived fallback only.

Raw values map to the 18 canonical DetOrderRules slugs via `ProductTypeResolver` + `config/ProductTypeMap.json`. No match leaves `ProductTypeId` null; Order falls back to value sniffing, then `default`.

## Open questions
- [ ] Vocabulary unification: `ProductTypeMap.json` overlaps conceptually with `TranslationDictionary.json` synonymGroups (domain productType, 4 groups). Unify or keep separate? (Resolver cannot reference TranslationConfig - project reference direction.)
- [ ] Term collisions across slugs (first match wins) - audit against real batch vocabulary.
