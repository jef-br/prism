using OpenCvSharp;

namespace Prism.Services.Transform;

/// <summary>
/// Removes the head of a detected person at the nose-to-lips boundary.
/// Operates on the BGR Mat produced by ImagePreProcessor — no additional decode.
/// Updates <see cref="ImageRecord_LAMBDA.ProcessedBytes"/> and <see cref="ImageRecord_LAMBDA.BoundingBox"/>
/// in place when a qualifying face is found. No-ops when no face is detected.
/// </summary>
internal static class Tx_util_HeadCutter
{
    /// <summary>
    /// Detects a human face and crops the image from the nose-to-lips line downward.
    /// Always runs when headcut is requested; branches on has-human feature for search strategy.
    /// When has-human is true, Algorithm A (anatomy-guided — pending deepdive, see Transform jbtodo.md)
    /// is used; otherwise Algorithm B searches the full top half.
    /// </summary>
    public static void Analyze(ImageRecord_LAMBDA lambda, Mat colorMat, HeadCutterConfig cfg)
    {
        BoundingBox bbox = lambda.BoundingBox!.Value;

        using CascadeClassifier faceDetector = new CascadeClassifier();
        if (!faceDetector.Load(FindHaarCascadePath()))
            return;

        using Mat gray = new Mat();
        Cv2.CvtColor(colorMat, gray, ColorConversionCodes.BGR2GRAY);

        Rect[] faces = DetectFaces(faceDetector, gray, lambda, colorMat.Rows);
        if (faces.Length == 0) return;

        // Pick the face furthest from the top edge (lowest centroid Y).
        Rect bestFace = faces[0];
        foreach (Rect f in faces)
            if (f.Y + f.Height / 2 > bestFace.Y + bestFace.Height / 2)
                bestFace = f;

        int cutY = bestFace.Y + (int)(bestFace.Height * cfg.FaceHeightCutFactor);
        if (cutY <= 0 || cutY >= colorMat.Rows) return;

        // Crop from cutY downward and re-encode.
        using Mat cropped = colorMat.SubMat(new Rect(0, cutY, colorMat.Cols, colorMat.Rows - cutY));
        Cv2.ImEncode(".jpg", cropped, out byte[] resultBytes);
        lambda.ProcessedBytes = resultBytes;

        // Shift bbox up — the cut is always above the clothing bbox.
        int newY = bbox.Y - cutY;
        lambda.BoundingBox = new BoundingBox
        {
            X      = bbox.X,
            Y      = newY,
            Width  = bbox.Width,
            Height = bbox.Height,
            Left   = bbox.Left,
            Top    = newY,
            Right  = bbox.Right,
            Bottom = newY + bbox.Height
        };
    }

    //  Face detection

    private static Rect[] DetectFaces(CascadeClassifier detector, Mat gray,
        ImageRecord_LAMBDA lambda, int imageHeight)
    {
        bool hasHuman = lambda.Features.GetValue("has-human") == "true";

        // Algorithm A (has-human == true): anatomy-guided search space refinement.
        // Pending deepdive on anatomical ratio constants — falls through to Algorithm B for now.
        // See Transform jbtodo.md.

        // Algorithm B: search the full top half of the image.
        Rect[] allFaces = detector.DetectMultiScale(gray);
        int halfHeight = imageHeight / 2;

        var qualifying = new System.Collections.Generic.List<Rect>();
        foreach (Rect f in allFaces)
        {
            if (f.Y + f.Height / 2 < halfHeight)
                qualifying.Add(f);
        }
        return [.. qualifying];
    }

    //  Haar cascade path resolution

    private static string FindHaarCascadePath()
    {
        string[] candidates =
        [
            "haarcascade_frontalface_default.xml",
            Path.Combine(AppContext.BaseDirectory, "haarcascade_frontalface_default.xml"),
            Path.Combine(AppContext.BaseDirectory, "runtimes", "haarcascade_frontalface_default.xml")
        ];

        foreach (string path in candidates)
            if (File.Exists(path)) return path;

        return "haarcascade_frontalface_default.xml";
    }
}
