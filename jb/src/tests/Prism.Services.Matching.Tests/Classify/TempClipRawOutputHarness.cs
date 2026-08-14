using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace PrismCoreTests.Classify;

/// <summary>
/// TEMPORARY (goal: "what does CLIP-ViT actually produce") — runs the exact production CLIP path
/// (ImageClassifier.GetShared + ClipPromptCatalog + ClassificationService.ApplyTokens's public
/// surface) over 10 real CiMini images and dumps every intermediate value: raw per-prompt logits,
/// per-feature-group softmax, winning tag, and the resulting Tags/Features written onto the LAMBDA.
/// No-op unless PRISM_CLIP_DUMP_DIR is set. Delete after the report is written.
/// </summary>
public sealed class TempClipRawOutputHarness {
    [Fact]
    public void DumpRawClipOutputForSampleImages() {
        string? dumpDir = Environment.GetEnvironmentVariable("PRISM_CLIP_DUMP_DIR");
        if (string.IsNullOrEmpty(dumpDir)) return;
        Directory.CreateDirectory(dumpDir);

        PrismConfiguration configuration =
            PrismConfiguration.LoadPrismConfig(ConfigLoader.RequireFile(PrismConfiguration.FileName));

        (string modelPath, string vocabPath, string mergesPath) = ClassificationService.ResolveClassifierPaths(configuration);
        ImageClassifier classifier = ImageClassifier.GetShared(modelPath, vocabPath, mergesPath);
        Assert.True(classifier.IsReady, "CLIP classifier failed to initialize — model/vocab/merges not found.");

        ClipPromptCatalog catalog = ClassificationService.LoadPromptCatalog();
        string[] prompts = catalog.BuildPrompts();

        using ClassificationService service = new(classifier, catalog, configuration);

        string ciMiniDir = ResolveCiMiniPath();
        string[] fileNames = [
            "24211507_CARDIGAN_76_MAGENTA_B.jpg",
            "C153KB460011_Cedric_City_Grey_FRON.png",
            "C153KU420009_Kendall_Twill sand_BACK.png",
            "blue-hoodie.jpg",
            "green-sweater-front.jpg",
            "graphite-scarf.jpg",
            "triggered_black-tshirt-front-americain.jpg",
            "triggered_black-tshirt-back-americain.jpg",
            "Pareo_exotica_F1.jpg",
            "charcol-wrap.jpg"
        ];

        var report = new List<object>();

        foreach (string fileName in fileNames) {
            string path = Path.Combine(ciMiniDir, fileName);
            using Image<Rgba32> image = Image.Load<Rgba32>(path);

            // Raw model output — exactly what ImageClassifier.ClassifyImage returns, before any
            // softmax/threshold/feature-group logic runs.
            ClassificationToken[] rawTokens = classifier.ClassifyImage(image, prompts);

            var rawPerPrompt = rawTokens.Select(t => {
                catalog.TryResolve(t.Label, out string feature, out string value);
                return new { prompt = t.Label, feature, value, rawLogit = t.Confidence };
            }).ToList();

            // Group by feature and compute the same softmax ClassificationService.ApplyTokens computes,
            // so we can show the full probability distribution per group, not just the winner.
            var groups = rawPerPrompt.GroupBy(r => r.feature).Select(g => {
                var members = g.ToList();
                double[] logits = [.. members.Select(m => m.rawLogit)];
                double max = logits.Max();
                double[] exp = [.. logits.Select(l => Math.Exp(l - max))];
                double sum = exp.Sum();
                double[] probs = [.. exp.Select(e => e / sum)];
                int winnerIdx = Array.IndexOf(probs, probs.Max());

                return new {
                    feature = g.Key,
                    distribution = members.Select((m, i) => new { m.value, m.prompt, rawLogit = m.rawLogit, probability = probs[i] }).ToList(),
                    winnerValue = members[winnerIdx].value,
                    winnerProbability = probs[winnerIdx],
                    influentialThreshold = configuration.InfluentialThresholdsByFeature.TryGetValue(g.Key, out double t) ? t : configuration.ThresholdForInfluentialTags,
                    cutoffThreshold = configuration.ThresholdForDiscardingClassificationTags
                };
            }).ToList();

            // Production path: builds lambda.Tags (Influential/Trivial) and lambda.Features exactly as
            // MatchingService.MatchAsync does per image.
            var lambda = new ImageRecord_LAMBDA { InitialFullName = path, Width = image.Width, Height = image.Height };
            service.ApplyClipTags(image, lambda, configuration.ThresholdForInfluentialTags, configuration.ThresholdForDiscardingClassificationTags);

            report.Add(new {
                fileName,
                width = image.Width,
                height = image.Height,
                promptCount = prompts.Length,
                rawPerPrompt,
                featureGroups = groups,
                finalInfluentialTags = lambda.Tags.Influential.Select(t => new { t.Label, t.Feature, t.Value, t.Confidence }),
                finalTrivialTags = lambda.Tags.Trivial.Select(t => new { t.Label, t.Feature, t.Value, t.Confidence }),
                finalFeatureSnapshot = lambda.Features.All.ToDictionary(kv => kv.Key, kv => new { kv.Value.Value, kv.Value.Confidence, kv.Value.Source })
            });

            Console.WriteLine($"[CLIP-DUMP] {fileName}: {lambda.Tags.Influential.Length} influential, {lambda.Tags.Trivial.Length} trivial tags");
        }

        string outPath = Path.Combine(dumpDir, "clip_raw_output.json");
        File.WriteAllText(outPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[CLIP-DUMP] wrote {outPath}");
    }

    private static string ResolveCiMiniPath() {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++) {
            string candidate = Path.Combine(dir, "test", "datasets", "CiMini");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }
        throw new DirectoryNotFoundException("Could not resolve test/datasets/CiMini by walking up from AppContext.BaseDirectory.");
    }
}
