namespace Prism.Core;

/*
 Proposed workings
 -----------------
 Client filenames often encode product type and orientation directly:
   headphone_4435345_A_FRONT.jpg  ->  product type "headphone" (electronics-small),
                                      hero-orientation FRONT
 The filename is tokenized on non-alphanumerics and:
   1. Product type: when the IEM gave no ProductTypeId, the first token mapping through
      ProductTypeMap.json supplies it (supporting evidence — never overrides Excel).
   2. Orientation: tokens naming an orientation (front/back/side/top/bottom/diagonal and
      common multilingual variants) write hero-orientation at the configured confidence,
      but only when the current value is UNKNOWN or weaker — CLIP evidence wins when stronger.
*/

/// <summary>
/// Extracts product-type and orientation evidence from the image filename. Runs in the first
/// wave of the post-match refinement chain, after Analyzer_ProductType.
/// </summary>
internal static class Analyzer_FilenameEvidence
{
    private static readonly Dictionary<string, string> OrientationTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["front"] = "FRONT", ["frontal"] = "FRONT", ["face"] = "FRONT", ["avant"] = "FRONT", ["delante"] = "FRONT",
        ["back"] = "BACK", ["rear"] = "BACK", ["dos"] = "BACK", ["espalda"] = "BACK", ["retro"] = "BACK", ["achterkant"] = "BACK",
        ["side"] = "SIDEON", ["lateral"] = "SIDEON", ["profile"] = "SIDEON", ["profil"] = "SIDEON", ["perfil"] = "SIDEON",
        ["top"] = "TOP", ["boven"] = "TOP", ["oben"] = "TOP",
        ["bottom"] = "BOTTOM", ["under"] = "BOTTOM", ["dessous"] = "BOTTOM", ["onder"] = "BOTTOM",
        ["diagonal"] = "DIAGONAL", ["diag"] = "DIAGONAL", ["angle"] = "DIAGONAL"
    };

    public static void Analyze(ImageRecord_LAMBDA lambda, ProductTypeResolver resolver, FilenameAnalyzerConfig cfg)
    {
        string stem = Path.GetFileNameWithoutExtension(lambda.InitialFullName ?? string.Empty);
        if (stem.Length == 0) return;

        List<string> tokens = [.. ProductTypeResolver.Tokenize(stem)];

        lambda.ProductTypeId ??= resolver.ResolveTokens(tokens);

        foreach (string token in tokens)
        {
            if (!OrientationTokens.TryGetValue(token, out string? orientation)) continue;

            bool weaker = !lambda.Features.TryGet("hero-orientation", out ImageFeatureValue? current)
                || current.IsUnknown
                || current.Confidence < cfg.OrientationConfidence;

            if (weaker)
                lambda.Features.Set("hero-orientation", orientation, cfg.OrientationConfidence, "filename");
            break;
        }
    }
}
