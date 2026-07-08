using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Services.Matching;

/// <summary>
/// Detects interior regions in product images using CPU-only ImageSharp pixel analysis.
/// An interior region is a large enclosed cavity surrounded by a strong boundary and
/// contained within a larger foreground object (e.g. inside a bag, wallet, or suitcase).
/// Sets the <c>interior-detected</c> ImageFeature consumed by the <c>interior-shot</c> phenotype rule.
/// Product-type gating (wallet/bag/suitcase only) is applied at the Order stage (T-1800).
/// </summary>
internal static class Analyzer_Interior
{
    /// <summary>
    /// Returns true when the image contains an interior region: a large enclosed cavity
    /// surrounded by a strong boundary and contained within a larger foreground object.
    /// Uses CPU-only ImageSharp processing. Thresholds come from analyzer_Config.json.
    /// </summary>
    public static bool Analyze(Image<Rgba32> image, InteriorAnalyzerConfig cfg)
    {
        int w = image.Width;
        int h = image.Height;
        float[,] gray = AnalyzerMath.ToGrayscale(image, w, h);
        float[,] edges = AnalyzerMath.ComputeGradientMagnitude(gray, w, h);
        return HasInteriorRegion(gray, edges, w, h, cfg);
    }

    // Scans a grid of candidate rectangular patches. For each candidate, tests whether
    // the interior is smoother than its surrounding ring and bounded by strong edges,
    // and whether the patch lies well inside the image frame.
    private static bool HasInteriorRegion(float[,] gray, float[,] edges, int w, int h, InteriorAnalyzerConfig cfg)
    {
        float totalArea = w * h;
        int borderRing = Math.Max(4, Math.Min(w, h) / 10);
        int stepY = Math.Max(1, h / 16);
        int stepX = Math.Max(1, w / 16);
        int shortSide = Math.Min(w, h);
        int sizeStep = Math.Max(1, shortSide / 6);

        for (int y0 = borderRing; y0 < h - borderRing; y0 += stepY)
        {
            for (int x0 = borderRing; x0 < w - borderRing; x0 += stepX)
            {
                for (int size = shortSide / 6; size <= shortSide / 2; size += sizeStep)
                {
                    int y1 = y0 + size;
                    int x1 = x0 + size;
                    if (y1 >= h - borderRing || x1 >= w - borderRing) continue;

                    float regionArea = (y1 - y0) * (x1 - x0);
                    if (regionArea / totalArea < cfg.MinAreaFraction) continue;

                    float interiorVar  = ComputeVariance(gray, x0, y0, x1, y1);
                    float surroundVar  = ComputeSurroundVariance(gray, x0, y0, x1, y1, borderRing, w, h);
                    float boundaryEdge = ComputeBoundaryEdge(edges, x0, y0, x1, y1);

                    bool strongBoundary   = boundaryEdge >= cfg.MinEdgeStrength;
                    bool smootherInterior = surroundVar - interiorVar >= cfg.TextureDiffMin;
                    bool containedInFrame = x0 > borderRing && y0 > borderRing
                                        && x1 < w - borderRing && y1 < h - borderRing;

                    if (strongBoundary && smootherInterior && containedInFrame)
                        return true;
                }
            }
        }
        return false;
    }

    private static float ComputeVariance(float[,] gray, int x0, int y0, int x1, int y1)
    {
        float sum = 0f, sumSq = 0f;
        int count = 0;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                sum   += gray[y, x];
                sumSq += gray[y, x] * gray[y, x];
                count++;
            }
        }
        if (count == 0) return 0f;
        float mean = sum / count;
        return sumSq / count - mean * mean;
    }

    private static float ComputeSurroundVariance(float[,] gray, int x0, int y0, int x1, int y1, int ring, int w, int h)
    {
        int ox0 = Math.Max(0, x0 - ring);
        int oy0 = Math.Max(0, y0 - ring);
        int ox1 = Math.Min(w, x1 + ring);
        int oy1 = Math.Min(h, y1 + ring);
        float sum = 0f, sumSq = 0f;
        int count = 0;
        for (int y = oy0; y < oy1; y++)
        {
            for (int x = ox0; x < ox1; x++)
            {
                if (x >= x0 && x < x1 && y >= y0 && y < y1) continue;
                sum   += gray[y, x];
                sumSq += gray[y, x] * gray[y, x];
                count++;
            }
        }
        if (count == 0) return 0f;
        float mean = sum / count;
        return sumSq / count - mean * mean;
    }

    // Average gradient magnitude along the four edges of the candidate rectangle.
    private static float ComputeBoundaryEdge(float[,] edges, int x0, int y0, int x1, int y1)
    {
        float sum = 0f;
        int count = 0;
        for (int x = x0; x < x1; x++) { sum += edges[y0, x] + edges[y1 - 1, x]; count += 2; }
        for (int y = y0 + 1; y < y1 - 1; y++) { sum += edges[y, x0] + edges[y, x1 - 1]; count += 2; }
        return count == 0 ? 0f : sum / count;
    }
}
