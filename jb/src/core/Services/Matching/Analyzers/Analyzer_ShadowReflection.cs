using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Services.Matching;

/*
 Proposed workings (STUB — see Analyzer_ShadowReflection.md)
 -----------------
 On solid backgrounds, the strip directly below the subject box tells the story:
   - shadow-present: a soft luminance gradient darker than the background estimate, without
     hard edges (low gradient magnitude), fading with distance from the subject.
   - reflection-present: a vertically mirrored, low-contrast ghost of the subject's bottom
     rows (correlate the strip against the flipped subject bottom).
 Both stay UNKNOWN on REALLIFE backgrounds where the strip is scene content.
*/

/// <summary>STUB: will set <c>shadow-present</c> and <c>reflection-present</c>. Currently writes nothing.</summary>
internal static class Analyzer_ShadowReflection {
    public static void Analyze(Image<Rgba32> image, ImageFeatureSnapshot snapshot) {
        // Not implemented — shadow-present/reflection-present stay UNKNOWN.
    }
}
