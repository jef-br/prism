using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Prism.Core;

/*JB: Note to Claude: Haar-Features can help detect facial features. */

/// <summary>
/// Extracts measurable ImageFeatures from a normalized image using CPU-only methods.
/// Uses ImageSharp for pixel-level analysis; does not require a GPU or external service.
///
/// Features requiring heavier models (pose estimation, orientation, human detection)
/// are recorded as UNKNOWN and will be populated by the CLIP-backed <see cref="ImageClassifier"/>
/// or specialized analyzers when those are implemented.
/// </summary>
public static class ImageFeatureAnalyzer
{
    // Background analysis
    private const float BackgroundVarianceSolidColorMax = 0.012f;
    private const float BackgroundVarianceLifestyleMin  = 0.040f;
    private const float NearWhiteChannelMin             = 0.90f;

    /// <summary>
    /// Analyzes the pre-loaded <paramref name="image"/> and writes all detectable
    /// feature values into <paramref name="snapshot"/>.
    /// Features that cannot be determined are recorded as UNKNOWN.
    /// </summary>
    public static void Analyze(Image<Rgba32> image, ImageFeatureSnapshot snapshot)
    {
        AnalyzeGeometry(image, snapshot);
        AnalyzeBackground(image, snapshot, out _, out _, out _);
        WriteEdgeIntersections(SubjectEdgeDetector.Detect(image), snapshot);
        DeriveOcclusionLevel(snapshot);
        AnalyzeSkinTone(image, snapshot);
        AnalyzeInterior(image, snapshot);
        RecordUnknownFeatures(snapshot);
    }

    //  Geometry 

    private static void AnalyzeGeometry(Image<Rgba32> image, ImageFeatureSnapshot snapshot)
    {
        double aspectRatio = (double)image.Width / image.Height;
        snapshot.Set("aspect-ratio",
            aspectRatio.ToString("F4", CultureInfo.InvariantCulture), 1.0, "geometry");
    }

    //  Background 

    private static void AnalyzeBackground(
        Image<Rgba32> image, ImageFeatureSnapshot snapshot,
        out float bgR, out float bgG, out float bgB)
    {
        bool hasAlpha = HasTransparentPixels(image);
        snapshot.Set("transparent-background", hasAlpha ? "true" : "false", 1.0, "imagesharp");
        snapshot.Set("clipping-path",          hasAlpha ? "true" : "false", 0.90, "imagesharp");

        SampleCorners(image, out bgR, out bgG, out bgB, out float variance);

        bool nearWhite = bgR > NearWhiteChannelMin && bgG > NearWhiteChannelMin && bgB > NearWhiteChannelMin;
        snapshot.Set("white-background", nearWhite ? "true" : "false", 0.92, "imagesharp");

        string bgType;
        if (hasAlpha)
        {
            bgType = "SOLIDCOLOR";
            snapshot.Set("lifestyle-background", "false", 0.95, "imagesharp");
        }
        else if (variance < BackgroundVarianceSolidColorMax)
        {
            bgType = "SOLIDCOLOR";
            snapshot.Set("lifestyle-background", "false", 0.85, "imagesharp");
        }
        else if (variance > BackgroundVarianceLifestyleMin)
        {
            bgType = "REALLIFE";
            snapshot.Set("lifestyle-background", "true", 0.72, "heuristic");
        }
        else
        {
            bgType = "UNKNOWN";
            snapshot.Set("lifestyle-background", "UNKNOWN", 0.0, "heuristic");
        }

        snapshot.Set("background-type", bgType, 0.82, "imagesharp");
    }

    //  Border intersections 

    private static void WriteEdgeIntersections(EdgeIntersectionResult r, ImageFeatureSnapshot snapshot)
    {
        snapshot.Set("intersects-top",     r.IntersectsTop    ? "true" : "false", 0.85, "heuristic");
        snapshot.Set("intersects-bottom",  r.IntersectsBottom ? "true" : "false", 0.85, "heuristic");
        snapshot.Set("intersects-left",    r.IntersectsLeft   ? "true" : "false", 0.85, "heuristic");
        snapshot.Set("intersects-right",   r.IntersectsRight  ? "true" : "false", 0.85, "heuristic");
        snapshot.Set("intersection-count", r.IntersectionCount.ToString(CultureInfo.InvariantCulture), 0.85, "heuristic");
        snapshot.Set("fully-in-frame",     r.FullyInFrame     ? "true" : "false", 0.85, "heuristic");
    }

    //  Occlusion level (derived) 

    private static void DeriveOcclusionLevel(ImageFeatureSnapshot snapshot)
    {
        string countStr = snapshot.GetValue("intersection-count");
        if (countStr == "UNKNOWN") return;

        if (!int.TryParse(countStr, out int count)) return;

        string level = count switch
        {
            0     => "full-product",
            1     => "mostly-visible",
            2     => "partially-occluded",
            >= 3  => "closeup",
            _     => "UNKNOWN"
        };

        snapshot.Set("occlusion-level", level, 0.68, "heuristic");
    }

    //  Skin tone 

    private static void AnalyzeSkinTone(Image<Rgba32> image, ImageFeatureSnapshot snapshot)
    {
        int total = 0;
        int skinPx = 0;

        // Sample every other pixel for performance.
        for (int y = 0; y < image.Height; y += 2)
        {
            for (int x = 0; x < image.Width; x += 2)
            {
                Rgba32 px = image[x, y];
                if (px.A < 128) continue;
                total++;
                if (IsSkinTone(px)) skinPx++;
            }
        }

        float ratio = total == 0 ? 0f : (float)skinPx / total;
        snapshot.Set("skin-tone-area",
            ratio.ToString("F4", CultureInfo.InvariantCulture), 0.75, "imagesharp");
    }

    //  Interior detection

    private static void AnalyzeInterior(Image<Rgba32> image, ImageFeatureSnapshot snapshot)
    {
        bool detected = InteriorAnalyzer.Analyze(image);
        snapshot.Set("interior-detected", detected ? "true" : "false", 1.0, "geometry");
    }

    //  Stubs for features that need heavier models

    private static void RecordUnknownFeatures(ImageFeatureSnapshot snapshot)
    {
        // These will be populated by the CLIP-backed classifier or specialized detectors.
        SetUnknownIfNotSet(snapshot, "hero-is-human");
        SetUnknownIfNotSet(snapshot, "hero-orientation");
        SetUnknownIfNotSet(snapshot, "has-human");
        SetUnknownIfNotSet(snapshot, "human-count");
        SetUnknownIfNotSet(snapshot, "has-head");
        SetUnknownIfNotSet(snapshot, "head-visible");
        SetUnknownIfNotSet(snapshot, "has-face");
        SetUnknownIfNotSet(snapshot, "face-visible");
        SetUnknownIfNotSet(snapshot, "body-visible");
        SetUnknownIfNotSet(snapshot, "pose-type");
        SetUnknownIfNotSet(snapshot, "contains-mannequin");
        SetUnknownIfNotSet(snapshot, "product-type-label");
        SetUnknownIfNotSet(snapshot, "packaging-visible");
        SetUnknownIfNotSet(snapshot, "multiple-products");
        SetUnknownIfNotSet(snapshot, "overlap-count");
        SetUnknownIfNotSet(snapshot, "scale-reference-present");
        SetUnknownIfNotSet(snapshot, "logo-present");
        SetUnknownIfNotSet(snapshot, "material-texture-visible");
        SetUnknownIfNotSet(snapshot, "text-present");
        SetUnknownIfNotSet(snapshot, "top-view");
        SetUnknownIfNotSet(snapshot, "shadow-present");
        SetUnknownIfNotSet(snapshot, "reflection-present");
        SetUnknownIfNotSet(snapshot, "lighting");
        SetUnknownIfNotSet(snapshot, "camera-angle");
        SetUnknownIfNotSet(snapshot, "salient-bbox");
        SetUnknownIfNotSet(snapshot, "product-coverage-ratio");
        SetUnknownIfNotSet(snapshot, "image-occupancy");
        SetUnknownIfNotSet(snapshot, "crop-tightness");
        SetUnknownIfNotSet(snapshot, "dominant-colors");
        SetUnknownIfNotSet(snapshot, "product-color");
        SetUnknownIfNotSet(snapshot, "background-color");
        SetUnknownIfNotSet(snapshot, "indoor");
        SetUnknownIfNotSet(snapshot, "outdoor");
        SetUnknownIfNotSet(snapshot, "symmetry-score");
        SetUnknownIfNotSet(snapshot, "product-aspect-ratio");
        SetUnknownIfNotSet(snapshot, "vertical-centering");
        SetUnknownIfNotSet(snapshot, "horizontal-centering");
    }

    private static void SetUnknownIfNotSet(ImageFeatureSnapshot snapshot, string featureId)
    {
        if (!snapshot.TryGet(featureId, out _))
            snapshot.Set(featureId, "UNKNOWN", 0.0, "heuristic");
    }

    //  Pixel helpers 

    private static bool HasTransparentPixels(Image<Rgba32> image)
    {
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
                if (image[x, y].A < 128) return true;
        return false;
    }

    private static void SampleCorners(
        Image<Rgba32> image,
        out float avgR, out float avgG, out float avgB, out float variance)
    {
        int cw = Math.Max(1, image.Width  / 10);
        int ch = Math.Max(1, image.Height / 10);

        float sumR = 0, sumG = 0, sumB = 0;
        int n = 0;

        void AddCorner(int x, int y)
        {
            if (x < 0 || x >= image.Width || y < 0 || y >= image.Height) return;
            Rgba32 px = image[x, y];
            if (px.A < 128) return;
            sumR += px.R / 255f;
            sumG += px.G / 255f;
            sumB += px.B / 255f;
            n++;
        }

        for (int dy = 0; dy < ch; dy++)
        {
            for (int dx = 0; dx < cw; dx++)
            {
                AddCorner(dx, dy);
                AddCorner(image.Width - 1 - dx, dy);
                AddCorner(dx, image.Height - 1 - dy);
                AddCorner(image.Width - 1 - dx, image.Height - 1 - dy);
            }
        }

        if (n == 0) { avgR = avgG = avgB = variance = 0f; return; }

        avgR = sumR / n;
        avgG = sumG / n;
        avgB = sumB / n;

        // Compute variance as mean squared deviation across channels.
        float varSum = 0;
        for (int dy = 0; dy < ch; dy++)
        {
            for (int dx = 0; dx < cw; dx++)
            {
                ComputeVarianceContrib(image, dx, dy, avgR, avgG, avgB, ref varSum);
                ComputeVarianceContrib(image, image.Width - 1 - dx, dy, avgR, avgG, avgB, ref varSum);
                ComputeVarianceContrib(image, dx, image.Height - 1 - dy, avgR, avgG, avgB, ref varSum);
                ComputeVarianceContrib(image, image.Width - 1 - dx, image.Height - 1 - dy, avgR, avgG, avgB, ref varSum);
            }
        }

        variance = varSum / (n * 3);
    }

    private static void ComputeVarianceContrib(
        Image<Rgba32> image, int x, int y,
        float avgR, float avgG, float avgB, ref float varSum)
    {
        if (x < 0 || x >= image.Width || y < 0 || y >= image.Height) return;
        Rgba32 px = image[x, y];
        if (px.A < 128) return;
        float dr = (px.R / 255f) - avgR;
        float dg = (px.G / 255f) - avgG;
        float db = (px.B / 255f) - avgB;
        varSum += dr * dr + dg * dg + db * db;
    }

    /// <summary>
    /// Multi-tone skin detection using YCbCr chrominance ranges.
    /// Covers all human skin tones under common studio and daylight conditions.
    /// </summary>
    private static bool IsSkinTone(Rgba32 px)
    {
        float r = px.R / 255f, g = px.G / 255f, b = px.B / 255f;
        float y  =  0.299f  * r + 0.587f  * g + 0.114f  * b;
        float cb = -0.1687f * r - 0.3313f * g + 0.5f    * b + 0.5f;
        float cr =  0.5f    * r - 0.4187f * g - 0.0813f * b + 0.5f;

        return y is > 0.10f and < 0.95f
            && cb is > 0.30f and < 0.53f
            && cr is > 0.52f and < 0.68f;
    }
}
