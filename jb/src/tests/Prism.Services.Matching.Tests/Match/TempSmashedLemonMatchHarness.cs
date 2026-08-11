using System.Text;
using Xunit;

namespace PrismCoreTests.Match;

/// <summary>
/// TEMPORARY (T-6900) matching-rate harness — no-op unless PRISM_SL_OUT is set.
/// Runs the REAL Excel model builder and the REAL ImageMatcher waterfall over SMASHEDLEMON45's
/// 1774 filenames without decoding a single image, so matching changes can be measured in seconds
/// instead of the ~78 minutes full feature analysis costs. Delete after measurement.
/// </summary>
public sealed class TempSmashedLemonMatchHarness {
    private const string DatasetRoot = @"test\datasets\BE tests\X SMASHEDLEMON45";

    [Fact]
    public void MeasureMatchRate() {
        string? outPath = Environment.GetEnvironmentVariable("PRISM_SL_OUT");
        if (string.IsNullOrEmpty(outPath)) return;

        string repoRoot = FindRepoRoot();
        string datasetDir = Path.Combine(repoRoot, DatasetRoot);
        string[] excelFiles = Directory.GetFiles(datasetDir, "*.xlsx", SearchOption.AllDirectories);
        string[] imageFiles = Directory.GetFiles(datasetDir, "*.jpg", SearchOption.AllDirectories);

        ExcelConfig excelConfig = ExcelConfig.Load(ConfigLoader.RequireFile("ExcelConfig.json"));
        TranslationConfig translationConfig = TranslationConfig.Load(ConfigLoader.RequireFile("TranslationDictionary.json"));
        ModelBuilder builder = new(excelConfig, translationConfig);
        IReadOnlyList<FamilyIDRecord> families = builder.BuildFromExcelFiles(excelFiles).FamilyRecords;

        // PRISM_SL_NAMEMODE: "full" (local-path ingest) or "bare" (multipart upload, filename only).
        // Both are real production shapes, so the harness measures whichever is asked for.
        bool bareNames = string.Equals(Environment.GetEnvironmentVariable("PRISM_SL_NAMEMODE"), "bare", StringComparison.OrdinalIgnoreCase);
        List<ImageRecord_LAMBDA> records = imageFiles
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f => new ImageRecord_LAMBDA {
                InitialFullName = bareNames ? Path.GetFileName(f) : f,
                Width = 1000,
                Height = 1500
            })
            .ToList();

        int koCount = ImageMatcher.Run(records, families);

        // Ground truth: filename "<article>_<colors>_<shot>_B2C.jpg" must resolve to the FamilyID whose
        // RefCo cell is "<article>-<colors joined by />". Built independently of the matcher.
        Dictionary<string, string> refCoToFamily = new(StringComparer.OrdinalIgnoreCase);
        foreach (FamilyIDRecord family in families) {
            foreach (KeyValuePair<string, string> prop in family.CanonicalProperties) {
                if (prop.Key.Contains("refco", StringComparison.OrdinalIgnoreCase))
                    refCoToFamily[NormalizeKey(prop.Value)] = family.FamilyID;
            }
        }

        int matched = 0, correct = 0, wrong = 0, koWithTruth = 0, noTruth = 0;
        Dictionary<string, int> koReasons = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> brackets = new(StringComparer.OrdinalIgnoreCase);
        StringBuilder wrongDetail = new();

        foreach (ImageRecord_LAMBDA record in records) {
            string stem = Path.GetFileNameWithoutExtension(record.InitialFullName!);
            string[] parts = stem.Split('_');
            string truthKey = parts.Length >= 2 ? NormalizeKey(parts[0] + "-" + parts[1]) : string.Empty;
            bool hasTruth = refCoToFamily.TryGetValue(truthKey, out string? expected);
            if (!hasTruth) noTruth++;

            string? actual = record.IsKo ? null : record.MatchEvidence?.FinalFamilyId;
            if (actual is null) {
                koReasons[record.KoReasonCode ?? "?"] = koReasons.GetValueOrDefault(record.KoReasonCode ?? "?") + 1;
                if (hasTruth) koWithTruth++;
                continue;
            }

            matched++;
            string bracket = record.MatchEvidence?.MatcherWeights is { Count: > 0 } w ? w[0].MatcherName : "?";
            brackets[bracket] = brackets.GetValueOrDefault(bracket) + 1;
            if (!hasTruth) continue;
            if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)) correct++;
            else {
                wrong++;
                if (wrongDetail.Length < 4000)
                    wrongDetail.AppendLine($"  WRONG {stem}: got {actual}, expected {expected}");
            }
        }

        StringBuilder report = new();
        report.AppendLine($"images={records.Count} families={families.Count}");
        report.AppendLine($"matched={matched} ({matched * 100.0 / records.Count:F1}%)  ko={koCount}");
        report.AppendLine($"correct={correct} ({correct * 100.0 / records.Count:F1}%)  wrong={wrong}");
        report.AppendLine($"imagesWithNoGroundTruthRow={noTruth}  koDespiteHavingTruth={koWithTruth}");
        report.AppendLine("matched by bracket:");
        foreach (KeyValuePair<string, int> kv in brackets.OrderByDescending(k => k.Value))
            report.AppendLine($"  {kv.Key}: {kv.Value}");
        report.AppendLine("ko reasons:");
        foreach (KeyValuePair<string, int> kv in koReasons.OrderByDescending(k => k.Value))
            report.AppendLine($"  {kv.Key}: {kv.Value}");
        report.Append(wrongDetail);

        Console.WriteLine(report.ToString());
        File.WriteAllText(outPath, report.ToString());
    }

    private static string NormalizeKey(string value) {
        StringBuilder sb = new(value.Length);
        foreach (char c in value)
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
            else sb.Append('-');
        return sb.ToString();
    }

    private static string FindRepoRoot() {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "test", "datasets")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root with test/datasets not found");
    }
}
