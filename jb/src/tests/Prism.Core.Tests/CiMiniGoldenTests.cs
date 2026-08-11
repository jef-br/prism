using System.Text.Json;
using Xunit;

namespace PrismCoreTests;

/// <summary>
/// T-4980 item 2 — the visibility gap. Until this existed, <c>expected-manifest.json</c> and
/// <c>expected-match.json</c> were read only by <c>test/ci/Invoke-CiPipeline.ps1</c>, so
/// <c>dotnet test jb/src/PRISM.sln</c> stayed green while the E2E golden gate was red, and every
/// golden-detectable regression was invisible to the normal test command. These tests compare the
/// shared <see cref="PipelineFixture"/> run against the same committed goldens the pwsh script uses.
///
/// The fixture's Default run is JSON + Transform, which produces Status / FamilyId / FinalFileName /
/// DetOrder — the four fields the Full-mode golden asserts. It does not produce a ZIP, so the
/// "every expected file is present in the archive" half of the script's Full mode stays with the
/// script; that is a packaging assertion, not a pipeline-behaviour one.
///
/// <para><b>Full coverage as of 2026-08-06.</b> CiMini's content was replaced wholesale (CiGolden +
/// JBComplete merged in — see the dataset README's history note); <see cref="PipelineFixture"/> was
/// widened the same day to submit every loose .jpg/.png (including subfolders) plus the zip archive,
/// so every golden row now has a counterpart here. Before the merge, the goldens were captured from a
/// 14-image run (11 loose + <c>3 images.zip</c>) and the fixture submitted loose files only, so three
/// zip-only rows had no counterpart. That gap is closed now, not just widened — see git history for
/// the old <c>ZipOnlySources</c> exclusion list if it ever needs resurrecting.</para>
/// </summary>
public class CiMiniGoldenTests : IClassFixture<PipelineFixture> {
    // Data-driven KO reasons the CI script also tolerates — a visual duplicate is a property of the
    // fixture, not a defect, and must not fail the build. Kept in sync with Invoke-CiPipeline.ps1.
    private static readonly string[] ToleratedKo = ["VISUAL_DUPLICATE"];

    private readonly PipelineFixture fixture;

    public CiMiniGoldenTests(PipelineFixture fixture) {
        this.fixture = fixture;
    }

    private sealed record GoldenRow(string SourceReference, string Status, string? FamilyId, string? FinalFileName, int? DetOrder);

    [Fact]
    public void CiMini_Manifest_MatchesCommittedGolden() {
        Dictionary<string, GoldenRow> golden = LoadGolden("expected-manifest.json");
        Dictionary<string, ManifestImageRow> actual = ActualRows();

        List<string> issues = [];

        foreach ((string source, GoldenRow expected) in golden) {
            if (!actual.TryGetValue(source, out ManifestImageRow? row)) {
                issues.Add($"{source}: expected in manifest, absent from the run");
                continue;
            }

            if (row.Status == "Ko" && row.KoReasonCode is { } code && ToleratedKo.Contains(code)) continue;

            if (!string.Equals(expected.Status, row.Status, StringComparison.Ordinal))
                issues.Add($"{source}: Status expected '{expected.Status}' got '{row.Status}' (KO={row.KoReasonCode})");
            if (!NullableEquals(expected.FamilyId, row.FamilyId))
                issues.Add($"{source}: FamilyId expected '{expected.FamilyId}' got '{row.FamilyId}'");
            if (!NullableEquals(expected.FinalFileName, row.FinalFileName))
                issues.Add($"{source}: FinalFileName expected '{expected.FinalFileName}' got '{row.FinalFileName}'");
            if (expected.DetOrder != row.DetOrder)
                issues.Add($"{source}: DetOrder expected '{expected.DetOrder}' got '{row.DetOrder}'");
        }

        Assert.True(issues.Count == 0,
            $"{issues.Count} golden mismatch(es) against expected-manifest.json:\n  {string.Join("\n  ", issues)}\n\n" +
            "KNOWN-RED (pre-2026-08-06 merge): family 94613033 only. T-5060 fixed family 90861052 "
            + "(compaction now orders on the configured-slot axis, so overflow images keep their anchor "
            + "position instead of being pushed behind every configured slot). What is left is upstream of "
            + "ordering: CLIP reads all three Pareo images as BACK at 0.35-0.48 confidence, so the packshot "
            + "claims bottomwear det1 and the two on-model shots the filenames call front (_F1, _F2) land at "
            + "det5 and overflow. That is T-4970's orientation-argmax error class, tracked by T-5080 — "
            + "ordering has no way to fix it.\n\n"
            + "KNOWN-RED (2026-08-06 merge, PipelineFixture widening) — UPDATE 2026-08-11: originally 4 "
            + "families listed here (99985047, 99147525, 98636303, plus 87186790's own family). All 4 "
            + "have since been re-examined and none currently show a FamilyId/Status mismatch — only "
            + "DetOrder/FinalFileName, i.e. the plain ordering-drift category below, not a CLIP-transport "
            + "classification difference. 98636303 (OMB-E180-BV_1..6) was NOT a CLIP transport issue at "
            + "all — root cause was [[T-5100]], a Bracket-3/SiblingPropagator matching defect. "
            + "87186790_1/_2.jpg (feeding families 99984905/99985047) was [[T-5090]] territory "
            + "(SubstringRescue ambiguity), also not CLIP. Both fixed and re-blessed 2026-08-11. "
            + "99985047's remaining member (26182-Denim-801/a (1).jpg) and 99147525 (3 "
            + "C153KB460011_*.png files) now only show DetOrder drift, same as every other family in the "
            + "next KNOWN-RED entry — whether a genuine CLIP-transport confidence difference is still "
            + "hiding underneath that ordering noise is unresolved; [[T-2840]]'s original evidence (0.39 "
            + "vs 0.40 confidence on OMB-E180-BV) was a case that turned out to be [[T-5100]]'s bug, not "
            + "CLIP's — so the same caution applies here before re-asserting a live transport-sensitivity "
            + "defect on these 2. [[T-2840]] did separately confirm real batch-composition sensitivity "
            + "via a controlled isolated-vs-full-batch experiment (max 0.045 delta, 0.069 signed range) "
            + "and the `hero-orientation` threshold was raised (0.33 → 0.42) in response — that finding "
            + "stands regardless of what these 2 families turn out to be.\n\n"
            + "KNOWN-RED (2026-08-11, T-5100 fix side effect): family 98636303's real images "
            + "(OMB-E166-BV_1..4.jpg) shifted DetOrder once OMB-E180-BV_1..6 correctly stopped competing "
            + "for its det-slots — FamilyId was re-blessed (T-5100 fixed a real defect there), DetOrder "
            + "deliberately was NOT: current overflow ordering does not track the filename's own _1.._4 "
            + "suffix order, which user direction says it should (0,1,2,3 expected, shuffling among "
            + "_1..._4 tolerated, arbitrary placement like _4 landing at det0 is not). Tracked by "
            + "[[T-5120]] (sequence-token-driven det ordering), which is deliberately gated pending a "
            + "clean/fresh/roomy session — do not fix this ordering as a side quest of something else.\n\n"
            + "Do NOT re-bless expected-manifest.json to clear any of these categories: the golden's order "
            + "is the correct one, and this assertion is the only thing making these defects visible.");
    }

    [Fact]
    public void CiMini_NoGoldenRowsAreMissingFromTheRun() {
        // Guards the direction the per-row loop in CiMini_Manifest_MatchesCommittedGolden cannot: a
        // run that silently drops images would otherwise pass every comparison it did make. Every
        // golden row has a counterpart here since PipelineFixture submits the whole dataset (loose
        // .jpg/.png, subfolders, and the zip archive) — nothing should ever be absent.
        Dictionary<string, GoldenRow> golden = LoadGolden("expected-manifest.json");
        Dictionary<string, ManifestImageRow> actual = ActualRows();

        Assert.Empty(golden.Keys.Where(k => !actual.ContainsKey(k)));

        // Nothing may appear in the run that the golden does not describe.
        Assert.Empty(actual.Keys.Where(k => !golden.ContainsKey(k)));
    }

    [Fact]
    public void CiMini_MatchGolden_FamilyAssignmentsHold() {
        // expected-match.json is the fast PR gate's golden: SourceReference -> FamilyId only. Asserted
        // separately from the full manifest so a matcher regression is distinguishable from an
        // ordering or transform regression in the failure message.
        Dictionary<string, GoldenRow> golden = LoadGolden("expected-match.json");
        Dictionary<string, ManifestImageRow> actual = ActualRows();

        List<string> issues = [];
        foreach ((string source, GoldenRow expected) in golden) {
            if (!actual.TryGetValue(source, out ManifestImageRow? row)) continue;
            if (row.Status == "Ko" && row.KoReasonCode is { } code && ToleratedKo.Contains(code)) continue;
            if (!NullableEquals(expected.FamilyId, row.FamilyId))
                issues.Add($"{source}: FamilyId expected '{expected.FamilyId}' got '{row.FamilyId}' (Status={row.Status}, KO={row.KoReasonCode})");
        }

        Assert.True(issues.Count == 0, $"{issues.Count} match-golden mismatch(es):\n  {string.Join("\n  ", issues)}");
    }

    //  Helpers

    private Dictionary<string, ManifestImageRow> ActualRows() =>
        this.fixture.Default.Manifest.ImageRows.ToDictionary(r => r.SourceReference, StringComparer.Ordinal);

    private Dictionary<string, GoldenRow> LoadGolden(string fileName) {
        string path = Path.Combine(this.fixture.FixturePath, "CiMini", fileName);
        // Fail loud rather than skip: a golden that quietly went missing would turn every assertion
        // above into a vacuous pass, which is the exact failure mode this class exists to close.
        if (!File.Exists(path)) throw new FileNotFoundException($"CiMini golden not found: {path}");

        List<GoldenRow> rows = JsonSerializer.Deserialize<List<GoldenRow>>(
            File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"Golden {fileName} did not deserialize to a row list.");

        return rows.ToDictionary(r => r.SourceReference, StringComparer.Ordinal);
    }

    // A golden written with an absent/null field means "no expectation recorded", which must not be
    // read as "expected empty" — the CI script treats "" and null alike for the same reason.
    private static bool NullableEquals(string? expected, string? actual) =>
        string.IsNullOrEmpty(expected) ? string.IsNullOrEmpty(actual) : string.Equals(expected, actual, StringComparison.Ordinal);
}
