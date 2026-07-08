using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Services.Matching;

/*
 Proposed workings (STUB — see Analyzer_LogoPresent.md)
 -----------------
 A logo is a small, high-contrast, color-consistent connected region visually distinct from
 the surrounding product texture. Heuristic: connected components on the gradient map inside
 the subject box, filtered by relative size (0.2–5% of the box), compactness, and low
 internal color variance. Long term: a small logo-detection ONNX if the heuristic's false
 positives (prints, patterns) prove too high on real batches.
*/

/// <summary>STUB: will set the <c>logo-present</c> ImageFeature. Currently writes nothing.</summary>
internal static class Analyzer_LogoPresent
{
    public static void Analyze(Image<Rgba32> image, ImageFeatureSnapshot snapshot)
    {
        // Not implemented — logo-present stays UNKNOWN.
    }
}
