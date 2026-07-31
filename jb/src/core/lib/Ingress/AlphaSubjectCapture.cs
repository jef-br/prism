using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Lib.Ingress;

/// <summary>
/// Builds a <see cref="SubjectDetectionResult"/> straight from a real alpha channel, before the Imported
/// stage flattens transparency onto white. Alpha is an exact, free subject mask — measured, not
/// inferred — so it always beats the classical-CV heuristic when one is available. Mirrors the
/// box/mask/intersect conventions of the classical-CV producer (Services/Matching/SubjectDetector.cs)
/// so both producers are interchangeable downstream: <c>BoundingBox</c> fields set consistently
/// (X/Y == Left/Top, Right/Bottom == X+Width/Y+Height), the mask a single-channel 0/255 PNG, and
/// intersect flags meaning "this fraction of the edge line is subject."
/// </summary>
public static class AlphaSubjectCapture {
    // A box covering this much of the frame means the alpha channel told us nothing usable (fully
    // opaque, or effectively so) — treat it exactly like no detection, mirroring the classical
    // detector's own whole-frame check.
    private const double WholeFrameAreaFraction = 0.98;

    // maskBytes entries default to 0 (transparent); only opaque pixels are written.
    private const byte MaskOpaqueValue = 255;

    /// <summary>
    /// Scans <paramref name="image"/> for opaque pixels (alpha at or above <paramref name="opacityThreshold"/>).
    /// Returns null when no pixel is opaque; otherwise a detection whose box is the tight bound of the
    /// opaque region, whose mask mirrors that region, and whose <see cref="SubjectDetectionResult.IsWholeFrameFallback"/>
    /// is set when the opaque region covers effectively the whole frame.
    /// </summary>
    public static SubjectDetectionResult? Capture(Image image, int opacityThreshold, double edgeContactFraction) {
        using Image<Rgba32> rgba = image.CloneAs<Rgba32>();
        int width = rgba.Width, height = rgba.Height;

        (bool foundAny, byte[] maskBytes, BoundingBox box) = ScanOpaqueRegion(rgba, opacityThreshold, width, height);
        if (!foundAny) return null;

        (bool top, bool bottom, bool left, bool right) = CanvasContacts(maskBytes, width, height, edgeContactFraction);
        bool wholeFrame = (long)box.Width * box.Height >= WholeFrameAreaFraction * width * height;

        return new SubjectDetectionResult {
            Producer = "alpha",
            Box = box,
            MaskPng = EncodeMaskPng(maskBytes, width, height),
            IntersectsTop = top,
            IntersectsBottom = bottom,
            IntersectsLeft = left,
            IntersectsRight = right,
            HasHardShadowEvidence = false,
            Confidence = 1.0,
            IsWholeFrameFallback = wholeFrame
        };
    }

    // Tight bound of every pixel whose alpha reaches opacityThreshold, plus the binary mask those
    // pixels form. foundAny is false (box/maskBytes meaningless) when nothing was opaque.
    private static (bool FoundAny, byte[] MaskBytes, BoundingBox Box) ScanOpaqueRegion(
        Image<Rgba32> rgba, int opacityThreshold, int width, int height) {
        byte[] maskBytes = new byte[width * height];
        int x0 = width, y0 = height, x1 = 0, y1 = 0;
        bool foundAny = false;

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                if (rgba[x, y].A < opacityThreshold) continue;

                foundAny = true;
                maskBytes[(y * width) + x] = MaskOpaqueValue;
                if (x < x0) x0 = x;
                if (x + 1 > x1) x1 = x + 1;
                if (y < y0) y0 = y;
                if (y + 1 > y1) y1 = y + 1;
            }
        }

        BoundingBox box = new() { X = x0, Y = y0, Width = x1 - x0, Height = y1 - y0, Left = x0, Top = y0, Right = x1, Bottom = y1 };
        return (foundAny, maskBytes, box);
    }

    // Fraction of opaque pixels along each canvas edge line — same "fraction of the edge line that is
    // subject" semantics as SubjectDetector.CanvasContacts, so both producers agree on "intersects."
    private static (bool Top, bool Bottom, bool Left, bool Right) CanvasContacts(
        byte[] maskBytes, int width, int height, double edgeContactFraction) {
        int top = 0, bottom = 0;
        for (int x = 0; x < width; x++) {
            if (maskBytes[x] == MaskOpaqueValue) top++;
            if (maskBytes[((height - 1) * width) + x] == MaskOpaqueValue) bottom++;
        }

        int left = 0, right = 0;
        for (int y = 0; y < height; y++) {
            if (maskBytes[y * width] == MaskOpaqueValue) left++;
            if (maskBytes[(y * width) + width - 1] == MaskOpaqueValue) right++;
        }

        return (
            top / (double)width >= edgeContactFraction,
            bottom / (double)width >= edgeContactFraction,
            left / (double)height >= edgeContactFraction,
            right / (double)height >= edgeContactFraction);
    }

    // maskBytes is one byte per pixel, row-major, matching L8's single-byte layout exactly — no
    // per-pixel indexer round trip needed to build the encodable image.
    private static byte[] EncodeMaskPng(byte[] maskBytes, int width, int height) {
        using Image<L8> mask = Image.LoadPixelData<L8>(Configuration.Default, maskBytes, width, height);
        using MemoryStream stream = new();
        mask.SaveAsPng(stream);
        return stream.ToArray();
    }
}
