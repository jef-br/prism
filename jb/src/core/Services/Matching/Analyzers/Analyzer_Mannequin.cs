using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Core;

/*
 Proposed workings (STUB — see Analyzer_Mannequin.md)
 -----------------
 A mannequin looks like a person to YOLO but has near-zero skin-tone area and no detectable
 face: person detection + skin-tone-area below a small threshold + Analyzer_FacePose finding
 no face → contains-mannequin = true. A CLIP prompt pair ("garment on a mannequin" vs
 "garment on a person") can arbitrate borderline cases. Depends on Analyzer_FacePose.
*/

/// <summary>STUB: will set the <c>contains-mannequin</c> ImageFeature. Currently writes nothing.</summary>
internal static class Analyzer_Mannequin
{
    public static void Analyze(Image<Rgba32> image, IReadOnlyList<YoloDetection> detections, ImageFeatureSnapshot snapshot)
    {
        // Not implemented — contains-mannequin stays UNKNOWN.
    }
}
