using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Core;

/// <summary>
/// Detects interior regions in product images using CPU-only ImageSharp pixel analysis.
/// An interior region is a large enclosed cavity surrounded by a strong boundary and
/// contained within a larger foreground object (e.g. inside a bag, wallet, or suitcase).
/// Sets the <c>interior-detected</c> ImageFeature consumed by the <c>interior-shot</c> phenotype rule.
/// Product-type gating (wallet/bag/suitcase only) is applied at the Order stage (T-1800).
/// </summary>
internal static class InteriorAnalyzer
{
    // Minimum fraction of image area an interior region must cover
    private const float MinAreaFraction = 0.04f;
    // Edge strength threshold (0–1 scale, gradient magnitude from 0–255 pixels)
    private const float MinEdgeStrength = 30f / 255f;
    // Interior texture must be meaningfully smoother than surroundings
    private const float TextureDiffMin = 0.015f;

    /// <summary>
    /// Returns true when the image contains an interior region: a large enclosed cavity
    /// surrounded by a strong boundary and contained within a larger foreground object.
    /// Uses CPU-only ImageSharp processing.
    /// </summary>
    public static bool Analyze(Image<Rgba32> image)
    {
        int w = image.Width;
        int h = image.Height;
        float[,] gray = ToGrayscale(image, w, h);
        float[,] edges = ComputeGradientMagnitude(gray, w, h);
        return HasInteriorRegion(gray, edges, w, h);
    }

    private static float[,] ToGrayscale(Image<Rgba32> image, int w, int h)
    {
        float[,] gray = new float[h, w];
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y++)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < w; x++)
                {
                    Rgba32 p = row[x];
                    gray[y, x] = (0.299f * p.R + 0.587f * p.G + 0.114f * p.B) / 255f;
                }
            }
        });
        return gray;
    }

    // Sobel-style gradient magnitude: gx = right - left, gy = below - above.
    // Input values are [0,1]; output is sqrt(gx^2 + gy^2), also in [0, ~1.4].
    private static float[,] ComputeGradientMagnitude(float[,] gray, int w, int h)
    {
        float[,] mag = new float[h, w];
        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                float gx = gray[y, x + 1] - gray[y, x - 1];
                float gy = gray[y + 1, x] - gray[y - 1, x];
                mag[y, x] = MathF.Sqrt(gx * gx + gy * gy);
            }
        }
        return mag;
    }

    // Scans a grid of candidate rectangular patches. For each candidate, tests whether
    // the interior is smoother than its surrounding ring and bounded by strong edges,
    // and whether the patch lies well inside the image frame.
    private static bool HasInteriorRegion(float[,] gray, float[,] edges, int w, int h)
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
                    if (regionArea / totalArea < MinAreaFraction) continue;

                    float interiorVar  = ComputeVariance(gray, x0, y0, x1, y1);
                    float surroundVar  = ComputeSurroundVariance(gray, x0, y0, x1, y1, borderRing, w, h);
                    float boundaryEdge = ComputeBoundaryEdge(edges, x0, y0, x1, y1);

                    bool strongBoundary   = boundaryEdge >= MinEdgeStrength;
                    bool smootherInterior = surroundVar - interiorVar >= TextureDiffMin;
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
