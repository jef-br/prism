using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace PrismCoreTests;

/// <summary>
/// TEMPORARY (T-6900) pre-match cost harness — no-op unless PRISM_TIMING_DIR is set.
/// Times decode / hash / feature-analysis separately over N real images so the pre-match
/// bottleneck can be localized instead of inferred. Delete after measurement.
/// </summary>
public sealed class TempPrematchTimingHarness {
    [Fact]
    public void TimePrematchStages() {
        string? dir = Environment.GetEnvironmentVariable("PRISM_TIMING_DIR");
        if (string.IsNullOrEmpty(dir)) return;

        int count = int.TryParse(Environment.GetEnvironmentVariable("PRISM_TIMING_COUNT"), out int n) ? n : 12;
        string[] files = Directory.GetFiles(dir, "*.jpg").OrderBy(f => f).Take(count).ToArray();

        PrismConfiguration configuration =
            PrismConfiguration.LoadPrismConfig(ConfigLoader.RequireFile(PrismConfiguration.FileName));
        var featureAnalysis = new FeatureAnalysisService(configuration);

        // Warm up model sessions / JIT so the first image doesn't absorb one-time startup cost.
        using (Image<Rgba32> warm = Image.Load<Rgba32>(files[0]))
            featureAnalysis.Analyze(warm, new ImageFeatureSnapshot());

        PhenotypeRuleSet ruleSet = PhenotypeRuleSet.Load(ConfigLoader.RequireFile("ImageRoles.json"));
        double totalDecode = 0, totalHash = 0, totalAnalyze = 0, totalRefine = 0;
        var sw = new Stopwatch();

        foreach (string file in files) {
            sw.Restart();
            using Image<Rgba32> image = Image.Load<Rgba32>(file);
            double decodeMs = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            VisualHasher.ComputeHash(image);
            double hashMs = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            featureAnalysis.Analyze(image, new ImageFeatureSnapshot());
            double analyzeMs = sw.Elapsed.TotalMilliseconds;

            // Refine is the SECOND full-resolution pass: it re-reads the file from disk itself, so it
            // is timed from the path, exactly as MatchingService.RefinePhenotypes calls it.
            var lambda = new ImageRecord_LAMBDA { InitialFullName = file, Width = image.Width, Height = image.Height };
            sw.Restart();
            featureAnalysis.Refine(lambda, family: null, file, ruleSet);
            double refineMs = sw.Elapsed.TotalMilliseconds;

            totalDecode += decodeMs;
            totalHash += hashMs;
            totalAnalyze += analyzeMs;
            totalRefine += refineMs;
            Console.WriteLine($"[T6900] {Path.GetFileName(file)} {image.Width}x{image.Height} " +
                $"decode={decodeMs:F0}ms hash={hashMs:F0}ms analyze={analyzeMs:F0}ms refine={refineMs:F0}ms");
        }

        int c = files.Length;
        double perImage = (totalDecode + totalHash + totalAnalyze + totalRefine) / c;
        Console.WriteLine($"[T6900] === {c} images: decode avg {totalDecode / c:F0}ms | " +
            $"hash avg {totalHash / c:F0}ms | analyze avg {totalAnalyze / c:F0}ms | " +
            $"refine avg {totalRefine / c:F0}ms | total avg {perImage:F0}ms");
        Console.WriteLine($"[T6900] === projected 1774 images: analyze {totalAnalyze / c * 1774 / 60000:F1} min " +
            $"(8-way parallel: {totalAnalyze / c * 1774 / 60000 / 8:F1} min) | " +
            $"refine {totalRefine / c * 1774 / 60000:F1} min (SEQUENTIAL, no parallelism)");
    }
}
