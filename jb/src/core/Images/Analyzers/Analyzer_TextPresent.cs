using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Core;

/*
 Proposed workings (STUB — see Analyzer_TextPresent.md)
 -----------------
 Detects printed/rendered text (size charts, care labels, packaging copy) for text-present.
 Cheap heuristic first: stroke-width-transform / MSER-style analysis on the gradient map —
 text is many small, high-contrast, similarly-sized connected strokes aligned in rows.
 If precision is insufficient on real batches, upgrade to a text-detection ONNX (EAST or
 DBNet, both small). Feeds the size-chart phenotype rule directly.
*/

/// <summary>STUB: will set the <c>text-present</c> ImageFeature. Currently writes nothing.</summary>
internal static class Analyzer_TextPresent
{
    public static void Analyze(Image<Rgba32> image, ImageFeatureSnapshot snapshot)
    {
        // Not implemented — text-present stays UNKNOWN.
    }
}
