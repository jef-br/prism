using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Services.Matching;

/*
 Proposed workings (STUB — see Analyzer_LightingDetail.md)
 -----------------
 lighting (EASY / HARD) summarizes how forgiving the shot is for transforms: histogram shape
 (high-key studio lighting = mass in the upper luminance range with a smooth rolloff = EASY;
 harsh mixed shadows = bimodal = HARD) plus gradient-direction coherence (one dominant light
 direction vs scattered). lighting-detail carries the raw descriptors for diagnostics.
 Builds on Analyzer_Exposure's histogram — share the pass when implementing.
*/

/// <summary>STUB: will set <c>lighting</c> and <c>lighting-detail</c>. Currently writes nothing.</summary>
internal static class Analyzer_LightingDetail {
    public static void Analyze(Image<Rgba32> image, ImageFeatureSnapshot snapshot) {
        // Not implemented — lighting/lighting-detail stay UNKNOWN.
    }
}
