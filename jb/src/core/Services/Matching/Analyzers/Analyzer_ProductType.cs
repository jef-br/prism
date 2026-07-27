namespace Prism.Services.Matching;

/*
 Proposed workings
 -----------------
 Resolves the product type for one image, in authority order (PRODUCTTYPES.MD: "Excel is
 authoritative for ProductType"):
   1. IEM "producttype" column — canonicalized by ModelBuilder from any language/spelling
      ("Product Type", "Tipo di prodotto", "Produkttyp", ...).
   2. IEM "ngp" column — a client Excel field carrying the same product-type semantics.
      NOTE: this Excel "NGP" column is UNRELATED to the ImageNGP phenotype taxonomy.
   3. CLIP product-type-label feature — image-derived supporting evidence, used only when
      the Excel columns resolve nothing.
 Raw values map to the canonical DetOrderRules slugs (topwear, bottomwear, footwear,
 bags-accessories, default — T-4710 collapsed 19 product types to 5) via ProductTypeResolver +
 ProductTypeMap.json ("camiseta" -> topwear). No match leaves ProductTypeId null;
 the Order stage then falls back to value sniffing and finally "default".
*/

/// <summary>
/// Sets <see cref="ImageRecord_LAMBDA.ProductTypeId"/> from IEM columns (authoritative) with
/// CLIP product-type-label fallback. First wave of the post-match refinement chain.
/// </summary>
internal static class Analyzer_ProductType {
    public static void Analyze(ImageRecord_LAMBDA lambda, FamilyIDRecord? family, ProductTypeResolver resolver) {
        string? resolved = family is null ? null : resolver.ResolveFromFamily(family);

        if (resolved is null) {
            string label = lambda.Features.GetValue("product-type-label");
            if (!string.Equals(label, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
                resolved = resolver.ResolveValue(label);
        }

        if (resolved is not null) lambda.ProductTypeId = resolved;
    }
}
