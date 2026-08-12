using System.Diagnostics;
using Xunit;

namespace PrismCoreTests;

/// <summary>
/// TEMPORARY (T-6910) — no-op unless PRISM_BENCH_DIR is set. Measures the Refine pass sequentially
/// versus Parallel.ForEach on the SAME images in one process, so the only variable is the loop.
/// Answers whether parallelizing Refine actually helps, given YoloDetector.Detect serializes on its
/// own RunLock. Delete after measurement.
/// </summary>
public sealed class TempRefineParallelBenchmark {
    [Fact]
    public void CompareSequentialVsParallelRefine() {
        string? dir = Environment.GetEnvironmentVariable("PRISM_BENCH_DIR");
        if (string.IsNullOrEmpty(dir)) return;

        int count = int.TryParse(Environment.GetEnvironmentVariable("PRISM_BENCH_COUNT"), out int n) ? n : 24;
        string[] files = Directory.GetFiles(dir, "*.jpg").OrderBy(f => f, StringComparer.Ordinal).Take(count).ToArray();

        PrismConfiguration configuration =
            PrismConfiguration.LoadPrismConfig(ConfigLoader.RequireFile(PrismConfiguration.FileName));
        var featureAnalysis = new FeatureAnalysisService(configuration);
        PhenotypeRuleSet ruleSet = PhenotypeRuleSet.Load(ConfigLoader.RequireFile("ImageRoles.json"));

        // Warm up model sessions / JIT so neither pass absorbs one-time startup cost.
        featureAnalysis.Refine(NewLambda(files[0]), null, files[0], ruleSet);

        var sw = Stopwatch.StartNew();
        foreach (string file in files)
            featureAnalysis.Refine(NewLambda(file), null, file, ruleSet);
        double sequentialMs = sw.Elapsed.TotalMilliseconds;

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8) };
        sw.Restart();
        Parallel.ForEach(files, parallelOptions, file =>
            featureAnalysis.Refine(NewLambda(file), null, file, ruleSet));
        double parallelMs = sw.Elapsed.TotalMilliseconds;

        Console.WriteLine($"[T6910-BENCH] {files.Length} images, MaxDOP={parallelOptions.MaxDegreeOfParallelism}, cores={Environment.ProcessorCount}");
        Console.WriteLine($"[T6910-BENCH] sequential {sequentialMs / 1000:F1}s ({sequentialMs / files.Length:F0} ms/image)");
        Console.WriteLine($"[T6910-BENCH] parallel   {parallelMs / 1000:F1}s ({parallelMs / files.Length:F0} ms/image)");
        Console.WriteLine($"[T6910-BENCH] speedup    {sequentialMs / parallelMs:F2}x");
    }

    private static ImageRecord_LAMBDA NewLambda(string file) => new() { InitialFullName = file };
}
