using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace PrismCoreTests.Classify;

/// <summary>
/// T-4990: scores <see cref="SubjectEdgeDetector"/> against the hand-verified intersection counts in
/// <c>test/datasets/SPACINI29/RAW IMAGES/dataset notes.md</c> (user-authored, do-not-edit). This is the
/// regression guard on the calibration: the previous thresholds scored 65/86 with 21 under-counts, and
/// an under-count is the consequential direction because <c>intersection-count = 0</c> is the hard gate
/// on every full-product, packshot and ghost phenotype — a false zero routes an edge-cropped model shot
/// to a full-product transform. Loads 86 real images, so it is slower than the unit tests around it.
/// </summary>
public class SubjectEdgeDetectorAccuracyTests {
    // Calibrated result, 2026-08-05: 84 correct, 0 under-counted, 2 over-counted.
    private const int MinimumCorrect = 84;
    private const int TotalImages = 86;

    [Fact]
    public void Detect_ScoredAgainstHandVerifiedCounts_NeverUnderCounts() {
        (int correct, int under, int over, List<string> wrong) = ScoreDataset();

        // Stated separately from the accuracy bar: raising overall accuracy while re-introducing an
        // under-count would be a worse detector for phenotype routing, and must still fail here.
        Assert.True(under == 0, $"{under} image(s) under-counted — the failure mode that mislabels an edge-cropped shot as full-product: {string.Join(", ", wrong)}");
        Assert.True(correct >= MinimumCorrect, $"accuracy regressed to {correct}/{TotalImages} (was {MinimumCorrect}); over-counted {over}: {string.Join(", ", wrong)}");
    }

    [Fact]
    public void Detect_TwoEdgeImages_AreAllCorrect() {
        // 70 of the 86 are cut top and bottom — the ordinary catalogue crop, and the case the old
        // 0.20 area bar missed on 15 of them.
        Dictionary<string, int> truth = LoadTruth();
        string dir = DatasetDirectory();
        List<string> wrong = [];

        foreach ((string file, int expected) in truth) {
            if (expected != 2) continue;
            if (Measure(Path.Combine(dir, file)) != 2) wrong.Add(file);
        }

        Assert.Empty(wrong);
    }

    //  Helpers

    private static (int correct, int under, int over, List<string> wrong) ScoreDataset() {
        Dictionary<string, int> truth = LoadTruth();
        string dir = DatasetDirectory();
        int correct = 0, under = 0, over = 0;
        List<string> wrong = [];

        foreach ((string file, int expected) in truth) {
            int measured = Measure(Path.Combine(dir, file));
            if (measured == expected) { correct++; continue; }
            wrong.Add($"{file} truth={expected} got={measured}");
            if (measured < expected) under++; else over++;
        }

        return (correct, under, over, wrong);
    }

    private static int Measure(string imagePath) {
        using Image<Rgba32> image = Image.Load<Rgba32>(imagePath);
        return SubjectEdgeDetector.Detect(image).IntersectionCount;
    }

    // SPACINI29 is 487 MB and gitignored, so a CI checkout never contains it. Same resolution shape as
    // ModelAssetLocator: source tree first, then a machine-local store named by PRISM_DATASET_DIR.
    private static string DatasetDirectory() {
        string inRepo = Path.Combine(RepoRoot(), "test", "datasets", "SPACINI29", "RAW IMAGES");
        if (Directory.Exists(inRepo)) return inRepo;

        string? datasetRoot = Environment.GetEnvironmentVariable("PRISM_DATASET_DIR");
        if (!string.IsNullOrWhiteSpace(datasetRoot)) {
            string overridden = Path.Combine(datasetRoot, "SPACINI29", "RAW IMAGES");
            if (Directory.Exists(overridden)) return overridden;
        }

        // Fail loud rather than skip: a silently-absent dataset would turn this into a vacuous pass.
        throw new DirectoryNotFoundException($"SPACINI29 not found at {inRepo}, and PRISM_DATASET_DIR names no copy either.");
    }

    // The notes file is UTF-16 and marked do-not-edit; it is parsed rather than transcribed so the
    // test cannot drift from the ground truth the user authored.
    private static Dictionary<string, int> LoadTruth() {
        string[] lines = File.ReadAllLines(Path.Combine(DatasetDirectory(), "dataset notes.md"), Encoding.Unicode);
        Dictionary<string, int> truth = [];
        int section = 0;

        foreach (string raw in lines) {
            string line = raw.Trim();
            if (line.Contains("These have 1 intersection", StringComparison.Ordinal)) { section = 1; continue; }
            if (line.Contains("These have 2 intersections", StringComparison.Ordinal)) { section = 2; continue; }
            if (section != 0 && line.StartsWith("- ", StringComparison.Ordinal) && line.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                truth[line[2..].Trim()] = section;
        }

        Assert.Equal(TotalImages, truth.Count);
        return truth;
    }

    private static string RepoRoot() {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "jb", "src", "PRISM.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Repo root not found walking up from the test assembly.");
    }
}
