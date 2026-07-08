using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Services.Matching;

/*
 Proposed workings
 -----------------
 Samples pixels inside the subject box only, EXCLUDING background-like pixels (close to the
 border background estimate) and skin-tone pixels (so a model's skin never becomes a product
 color). Survivors quantize into an 8×8×8 RGB grid; the top 4 buckets above the minimum share
 become the dominant-colors hex list, strongest first.

 Hard cases (calibrate against real batches — see Analyzer_DominantColors.md):
   - White product on white background: exclusion eats everything → too few samples →
     dominant-colors stays UNKNOWN. Never guess.
   - Skin-colored product on a human (tan bathing suit on tanned model): skin exclusion may
     eat the product too; the low surviving-sample share keeps confidence honest.
*/

/// <summary>
/// Measures the dominant subject colors. Returns the ranked buckets so Analyzer_ProductColor
/// reuses the same sampling.
/// </summary>
internal static class Analyzer_DominantColors
{
    public static IReadOnlyList<ColorBucket> Analyze(Image<Rgba32> image, SubjectBox? subject, ImageFeatureSnapshot snapshot, ColorAnalyzerConfig cfg)
    {
        if (subject is null) return [];

        (float bgR, float bgG, float bgB) = AnalyzerMath.EstimateBackgroundColor(image);
        int bins = cfg.BinsPerChannel;
        int[] counts = new int[bins * bins * bins];
        double[] sumR = new double[counts.Length];
        double[] sumG = new double[counts.Length];
        double[] sumB = new double[counts.Length];
        int sampled = 0, survived = 0;

        int x0 = (int)(subject.X1 * image.Width);
        int x1 = (int)(subject.X2 * image.Width);
        int y0 = (int)(subject.Y1 * image.Height);
        int y1 = (int)(subject.Y2 * image.Height);

        image.ProcessPixelRows(accessor =>
        {
            for (int y = Math.Max(0, y0); y < Math.Min(accessor.Height, y1); y += 2)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = Math.Max(0, x0); x < Math.Min(row.Length, x1); x += 2)
                {
                    Rgba32 p = row[x];
                    if (p.A < 128) continue;
                    sampled++;

                    float r = p.R / 255f, g = p.G / 255f, b = p.B / 255f;
                    if (AnalyzerMath.ColorDistance(r, g, b, bgR, bgG, bgB) < cfg.BackgroundDistance) continue;
                    if (AnalyzerMath.IsSkinTone(p)) continue;

                    survived++;
                    int idx = Math.Min((int)(r * bins), bins - 1) * bins * bins
                            + Math.Min((int)(g * bins), bins - 1) * bins
                            + Math.Min((int)(b * bins), bins - 1);
                    counts[idx]++;
                    sumR[idx] += r; sumG[idx] += g; sumB[idx] += b;
                }
            }
        });

        // Too few surviving pixels (white-on-white, skin-colored product): stay UNKNOWN.
        if (sampled == 0 || (float)survived / sampled < cfg.MinSampleFraction) return [];

        List<ColorBucket> buckets = [];
        foreach (int idx in Enumerable.Range(0, counts.Length).OrderByDescending(i => counts[i]).Take(cfg.BucketCount))
        {
            float share = (float)counts[idx] / survived;
            if (counts[idx] == 0 || share < cfg.MinBucketShare) break;
            buckets.Add(new ColorBucket((float)(sumR[idx] / counts[idx]), (float)(sumG[idx] / counts[idx]), (float)(sumB[idx] / counts[idx]), share));
        }

        if (buckets.Count == 0) return [];

        snapshot.Set("dominant-colors", string.Join(",", buckets.Select(b => b.Hex)), cfg.DominantColorsConfidence, "imagesharp");
        return buckets;
    }
}
