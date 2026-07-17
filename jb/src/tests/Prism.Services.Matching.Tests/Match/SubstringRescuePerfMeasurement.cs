using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace PrismCoreTests.Match;

/// <summary>
/// T-3800 item 2 measurement: times NumericMatcher.TryMatchBySubstringRescue's brute-force digit-index
/// scan at a synthetic representative-heavy-batch scale (jb/docs/PRISM-overview.md: ~2,500 images/batch),
/// with indexDigitRunsAllColumns=true and minSubstringRescueLength=7 matching production MatchingConfig.json.
/// Not a regression gate — a one-time measurement to decide whether an n-gram index is warranted.
/// </summary>
public class SubstringRescuePerfMeasurement
{
    private readonly ITestOutputHelper output;

    public SubstringRescuePerfMeasurement(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Theory]
    [InlineData(250)]   // generous slice of a 2,500-image heavy batch reaching Bracket 5 rescue
    [InlineData(2500)]  // pathological upper bound: every image in the heaviest documented batch unmatched
    public void TryMatchBySubstringRescue_HeavyBatchScale_ReportsElapsedTime(int unmatchedImageCount)
    {
        const int familyCount = 3000; // family-catalog scale matching a ~2,500-image heavy batch

        List<FamilyIDRecord> families = BuildSyntheticFamilies(familyCount);
        MatchingRule familyIdRule = new() { ExcelField = "FamilyID", Type = "numeric", Strategy = "NumericalMatcher", Weight = 1.0, MaxDistance = 1.478 };
        MatchingRule refCoRule    = new() { ExcelField = "RefCo",    Type = "numeric", Strategy = "NumericalMatcher", Weight = 1.0, MaxDistance = 1.478 };
        MatchingRule eanRule      = new() { ExcelField = "EAN",      Type = "numeric", Strategy = "NumericalMatcher", Weight = 1.0, MaxDistance = 1.478 };
        List<MatchingRule> rules  = [familyIdRule, refCoRule, eanRule];

        NumericMatcher matcher = new("FamilyID", minNumericTokenLength: 5, indexDigitRunsAllColumns: true, minSubstringRescueLength: 7);

        // Warm-up call builds and caches the digit index (excluded from the measured scan time — the
        // index is built once per Matched-stage run, not once per image, so it is not part of the
        // per-image cost the todo is asking about).
        matcher.TryMatchBySubstringRescue(MakeLambda("999999990.jpg"), families, rules);

        List<ImageRecord_LAMBDA> unmatchedImages = [];
        for (int i = 0; i < unmatchedImageCount; i++)
            unmatchedImages.Add(MakeLambda($"9999999{i:D2}.jpg")); // 9-digit token, guaranteed absent from the index → worst-case full scan every call

        Stopwatch stopwatch = Stopwatch.StartNew();
        foreach (ImageRecord_LAMBDA image in unmatchedImages)
            matcher.TryMatchBySubstringRescue(image, families, rules);
        stopwatch.Stop();

        double totalMs = stopwatch.Elapsed.TotalMilliseconds;
        double perImageMs = totalMs / unmatchedImageCount;

        output.WriteLine($"families={familyCount} unmatchedImages={unmatchedImageCount} totalMs={totalMs:F2} perImageMs={perImageMs:F4}");

        // Regression guard, not a tight benchmark assertion: measured totals are ~0.3-1.2s at this
        // scale (see T-3800 report), so 10s gives an order of magnitude of headroom for slower CI
        // hardware while still catching a genuine algorithmic blow-up (e.g. an accidental O(n^2) index
        // rebuild per call).
        Assert.True(totalMs < 10_000, $"TryMatchBySubstringRescue took {totalMs:F0}ms for {unmatchedImageCount} images against a {familyCount}-family index — investigate before assuming this is still negligible.");
    }

    private static List<FamilyIDRecord> BuildSyntheticFamilies(int count)
    {
        List<FamilyIDRecord> families = [];
        for (int i = 0; i < count; i++)
        {
            string familyId = (90000000 + i).ToString();
            FamilyIDRecord family = new(familyId);
            family.MergeProperty(new ExcelPropertyValue("RefCo", [$"{10000000 + i}"], []), ExcelColumnClassification.Numerical);
            family.MergeProperty(new ExcelPropertyValue("EAN", [$"84462710{i:D5}"], []), ExcelColumnClassification.Numerical);
            // Mixed compound label with embedded digit runs — mirrors real "MAN-Posy Green-1010930-60105"
            // catalogue cells and is what makes indexDigitRunsAllColumns=true expensive to index/scan.
            family.MergeProperty(new ExcelPropertyValue("label", [$"MAN-Style {i}-{1000000 + i}-{60000 + i % 999}"], []), ExcelColumnClassification.Mixed);
            families.Add(family);
        }
        return families;
    }

    private static ImageRecord_LAMBDA MakeLambda(string filename) => new() { InitialFullName = filename };
}
