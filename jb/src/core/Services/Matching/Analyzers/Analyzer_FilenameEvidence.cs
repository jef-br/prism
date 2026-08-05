namespace Prism.Services.Matching;

/*
 Proposed workings
 -----------------
 Client filenames often encode product type and orientation directly:
   hoodie_4435345_A_FRONT.jpg  ->  product type "hoodie" (topwear), hero-orientation FRONT
 The filename is tokenized on non-alphanumerics and:
   1. Product type: when the IEM gave no ProductTypeId, the first token mapping through
      ProductTypeMap.json supplies it (supporting evidence — never overrides Excel).
   2. Orientation: a token naming an orientation (front/back/side/top/bottom/diagonal and
      common multilingual variants) writes hero-orientation at the configured confidence,
      but only when the current value is UNKNOWN or weaker — CLIP evidence wins when stronger,
      and only when that token is the LAST token of the stem (see below).
*/

/// <summary>
/// Extracts product-type and orientation evidence from the image filename. Runs in the first
/// wave of the post-match refinement chain, after Analyzer_ProductType.
/// </summary>
public static class Analyzer_FilenameEvidence {
    /// <summary>
    /// Thresholds for Analyzer_FilenameEvidence, bound from the "Filename" section of
    /// analyzer_Config.json. No defaults — every value must be present in the JSON or deserialization
    /// fails loud.
    /// </summary>
    public sealed class Config : IValidatableConfig {
        /// <summary>
        /// Confidence written on hero-orientation when a filename token names the orientation.
        /// A stronger existing measurement (e.g. CLIP) is never overwritten.
        /// </summary>
        public required float OrientationConfidence { get; init; }

        /// <summary>
        /// Filename token → hero-orientation value. Loaded from analyzer_Config.json. Keys must be
        /// lowercase: filename tokens arrive in their original case and are lowercased for lookup,
        /// so a capitalised key would silently never match. Validate rejects that rather than
        /// letting the entry sit in the file doing nothing.
        /// </summary>
        public required Dictionary<string, string> OrientationTokens { get; init; }

        public void Validate() {
            if (this.OrientationConfidence is <= 0f or > 1f)
                throw new PrismConfigurationException("Filename.OrientationConfidence must be in (0,1]");
            if (this.OrientationTokens.Count == 0)
                throw new PrismConfigurationException("Filename.OrientationTokens must not be empty");

            List<string> miscased = [.. this.OrientationTokens.Keys.Where(k => k != k.ToLowerInvariant())];
            if (miscased.Count > 0)
                throw new PrismConfigurationException($"Filename.OrientationTokens keys must be lowercase; found: {string.Join(", ", miscased)}");
        }
    }

    public static void Analyze(ImageRecord_LAMBDA lambda, ProductTypeResolver resolver, Config cfg) {
        string stem = Path.GetFileNameWithoutExtension(lambda.InitialFullName ?? string.Empty);
        if (stem.Length == 0) return;

        List<string> tokens = [.. ProductTypeResolver.Tokenize(stem)];

        lambda.ProductTypeId ??= resolver.ResolveTokens(tokens);

        WriteOrientation(lambda, tokens, cfg);
    }

    // T-5000: only the FINAL token of the stem counts as a camera view. `top`, `bottom` and `back` are
    // apparel nouns as often as view words, and a bare match on any position reads the garment as the
    // camera angle — `freya_top_cinzia_skirt_F` becomes TOP, `...-BACK-STRAP-SANDALS-...` becomes BACK.
    // Measured over test/datasets (17,616 images, 2026-08-05): 1,567 stems carry an orientation token
    // and 1,564 of them place it last, all genuine views (VINGINO79's `..._FRONT` / `..._BACK`). Every
    // known false positive — 5 back-strap sandals, 10 bikini top/bottom pieces — sits mid-name, as do
    // the three `Deep Back_FRONT` files whose *colour name* contains `back` and which the previous
    // first-match-wins scan labelled BACK on a front shot.
    private static void WriteOrientation(ImageRecord_LAMBDA lambda, List<string> tokens, Config cfg) {
        if (tokens.Count == 0) return;
        if (!cfg.OrientationTokens.TryGetValue(tokens[^1].ToLowerInvariant(), out string? orientation)) return;

        bool weaker = !lambda.Features.TryGet("hero-orientation", out ImageFeatureValue? current)
            || current.IsUnknown
            || current.Confidence < cfg.OrientationConfidence;

        if (weaker)
            lambda.Features.Set("hero-orientation", orientation, cfg.OrientationConfidence, "filename");
    }
}
