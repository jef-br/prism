using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Core;

/// <summary>
/// Shared CPU pixel-math helpers for the analyzer chain. Centralizes the grayscale and
/// gradient computations previously duplicated across Analyzer_Interior and
/// Analyzer_IsIllustration so every analyzer works from identical intermediates.
/// </summary>
internal static class AnalyzerMath
{
    /// <summary>
    /// Converts the image to a [h, w] luminance array using Rec. 601 weights. Values are in [0, 1].
    /// </summary>
    public static float[,] ToGrayscale(Image<Rgba32> image, int w, int h)
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

    /// <summary>
    /// Sobel-style gradient magnitude: gx = right - left, gy = below - above.
    /// Input values are [0,1]; output is sqrt(gx^2 + gy^2), also in [0, ~1.4].
    /// </summary>
    public static float[,] ComputeGradientMagnitude(float[,] gray, int w, int h)
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

    /// <summary>
    /// Estimates the background color ([0,1] channels) from a 5%-deep strip along all four borders.
    /// Mirrors the SubjectEdgeDetector convention: on a packshot the border is background.
    /// </summary>
    public static (float R, float G, float B) EstimateBackgroundColor(Image<Rgba32> image)
    {
        int w = image.Width;
        int h = image.Height;
        int depth = Math.Max(1, (int)(Math.Min(w, h) * 0.05f));
        double sumR = 0, sumG = 0, sumB = 0;
        int n = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y++)
            {
                bool inTopOrBottomBand = y < depth || y >= h - depth;
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < w; x++)
                {
                    if (!inTopOrBottomBand && x >= depth && x < w - depth) continue;
                    Rgba32 p = row[x];
                    if (p.A < 128) continue;
                    sumR += p.R / 255f; sumG += p.G / 255f; sumB += p.B / 255f;
                    n++;
                }
            }
        });

        return n == 0 ? (1f, 1f, 1f) : ((float)(sumR / n), (float)(sumG / n), (float)(sumB / n));
    }

    /// <summary>
    /// Fallback subject box: the bounding rectangle of pixels farther than
    /// <paramref name="minDistance"/> (Euclidean RGB, [0,1] channels) from the background estimate.
    /// Returns null when too little foreground is found — never guesses.
    /// </summary>
    public static SubjectBox? ComputeForegroundBox(Image<Rgba32> image, (float R, float G, float B) background, float minDistance, float minForegroundFraction, float confidence)
    {
        int w = image.Width;
        int h = image.Height;
        int minX = w, minY = h, maxX = -1, maxY = -1;
        int fgCount = 0, total = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y += 2)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < w; x += 2)
                {
                    Rgba32 p = row[x];
                    total++;
                    bool foreground = p.A >= 128
                        && ColorDistance(p.R / 255f, p.G / 255f, p.B / 255f, background.R, background.G, background.B) > minDistance;
                    if (!foreground) continue;

                    fgCount++;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        });

        if (maxX < 0 || total == 0 || (float)fgCount / total < minForegroundFraction) return null;

        return new SubjectBox(minX / (float)w, minY / (float)h, (maxX + 1) / (float)w, (maxY + 1) / (float)h, confidence, "foreground");
    }

    /// <summary>Euclidean distance between two RGB colors with [0,1] channels (range [0, ~1.73]).</summary>
    public static float ColorDistance(float r1, float g1, float b1, float r2, float g2, float b2)
    {
        float dr = r1 - r2, dg = g1 - g2, db = b1 - b2;
        return MathF.Sqrt(dr * dr + dg * dg + db * db);
    }

    /// <summary>
    /// Multi-tone skin detection using YCbCr chrominance ranges.
    /// Covers all human skin tones under common studio and daylight conditions.
    /// </summary>
    public static bool IsSkinTone(Rgba32 px)
    {
        float r = px.R / 255f, g = px.G / 255f, b = px.B / 255f;
        float y  =  0.299f  * r + 0.587f  * g + 0.114f  * b;
        float cb = -0.1687f * r - 0.3313f * g + 0.5f    * b + 0.5f;
        float cr =  0.5f    * r - 0.4187f * g - 0.0813f * b + 0.5f;

        return y is > 0.10f and < 0.95f
            && cb is > 0.30f and < 0.53f
            && cr is > 0.52f and < 0.68f;
    }

    /// <summary>Returns the palette name (name → #rrggbb map) nearest to the given [0,1] RGB color.</summary>
    public static string NearestPaletteName(float r, float g, float b, IReadOnlyDictionary<string, string> palette)
    {
        string bestName = "unknown";
        float bestDistance = float.MaxValue;

        foreach ((string name, string hex) in palette)
        {
            if (hex.Length != 7 || hex[0] != '#') continue;
            float pr = Convert.ToInt32(hex.Substring(1, 2), 16) / 255f;
            float pg = Convert.ToInt32(hex.Substring(3, 2), 16) / 255f;
            float pb = Convert.ToInt32(hex.Substring(5, 2), 16) / 255f;

            float distance = ColorDistance(r, g, b, pr, pg, pb);
            if (distance < bestDistance) { bestDistance = distance; bestName = name; }
        }

        return bestName;
    }
}
