using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Core;

/*
 Proposed workings (STUB — see Analyzer_MaterialTexture.md)
 -----------------
 material-texture-visible is a closeup property: the weave/grain must be resolvable. Measure
 high-frequency energy (gradient magnitude above a fine-detail threshold) inside the subject
 box and require high crop-tightness — a tight crop with strong fine-grained texture energy
 means the material is visible. Calibrate thresholds against real detail shots.
*/

/// <summary>STUB: will set <c>material-texture-visible</c>. Currently writes nothing.</summary>
internal static class Analyzer_MaterialTexture
{
    public static void Analyze(Image<Rgba32> image, ImageFeatureSnapshot snapshot)
    {
        // Not implemented — material-texture-visible stays UNKNOWN.
    }
}
