using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Services.Matching;

/*
 Proposed workings (STUB — see Analyzer_CameraAngle.md)
 -----------------
 camera-angle (eye-level / low-angle / high-angle / overhead) and top-view from a
 combination of cheap signals: subject-box vertical placement and aspect (overhead flat lays
 are wide, centered, shadow-free), shadow direction below the subject (eye-level side light
 vs overhead), and CLIP prompts ("photographed from directly above", "photographed at eye
 level") to arbitrate. Filename tokens (TOP) already contribute via Analyzer_FilenameEvidence.
*/

/// <summary>STUB: will set <c>camera-angle</c> and <c>top-view</c>. Currently writes nothing.</summary>
internal static class Analyzer_CameraAngle {
    public static void Analyze(Image<Rgba32> image, ImageFeatureSnapshot snapshot) {
        // Not implemented — camera-angle and top-view stay UNKNOWN.
    }
}
