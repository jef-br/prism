using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Core;

/*
 Proposed workings (STUB — see Analyzer_FacePose.md)
 -----------------
 When YOLO reports a person, refine the human evidence inside the person box:
   - has-face / face-visible / has-head / head-visible: OpenCV Haar cascades (frontal +
     profile) over the top region of the person box — OpenCV is approved for Classify.
   - body-visible (full / three-quarter / half / bust): ratio of the person box to the frame
     and which borders it intersects.
   - pose-type (standing / sitting / crouching / lying): person box aspect ratio as a first
     cut; a lightweight pose ONNX (e.g. yolov8n-pose) if aspect alone proves too coarse.
 Gated on a person detection, so the cascades never run on product-only images.
*/

/// <summary>
/// STUB: will set has-head, head-visible, has-face, face-visible, body-visible, and pose-type
/// from the YOLO person box. Currently writes nothing.
/// </summary>
internal static class Analyzer_FacePose
{
    public static void Analyze(Image<Rgba32> image, IReadOnlyList<YoloDetection> detections, ImageFeatureSnapshot snapshot)
    {
        // Not implemented — face/pose features stay UNKNOWN (CLIP still covers head-visible/body-visible).
    }
}
