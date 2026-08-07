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
            + "KNOWN-RED (2026-08-06 merge, PipelineFixture widening): 4 more families — 99985047 "
            + "(26182-Denim-801/a (1).jpg vs 87186790_2.jpg), 99147525 (3 C153KB460011_*.png files), and "
            + "98636303 (OMB-E180-BV_1/2/3.jpg). CLIP produces different classification output for the same "
            + "images depending on submission transport (PipelineFixture's direct in-process call vs "
            + "Invoke-CiPipeline.ps1's HTTP multipart+zip-repack), close enough to the 0.33 orientation "
            + "threshold to flip which image wins a tied slot. Root cause under investigation: [[T-2840]] "
            + "(a T-2820 recurrence, now confirmed cross-transport rather than same-transport-flaky).\n\n"
            + "Do NOT re-bless expected-manifest.json to clear either category: the golden's order is the "
            + "correct one, and this assertion is the only thing making these defects visible.");
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
