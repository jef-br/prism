using Prism.Services.Matching;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace PrismCoreTests.Classify;

/// <summary>
/// Guards the wiring between the refinement chain and the subject detector (T-4800), end to end through
/// the real ImageSharp→OpenCvSharp conversion. This exists because a defect slipped past 466 green unit
/// tests: every detector test builds its Mat with OpenCvSharp directly, so none of them crossed the
/// conversion boundary, and the exception it threw on every real image was swallowed by the refinement
/// chain's deliberate non-fatal catch. Detection silently produced nothing on a full dataset while the
/// suite stayed green. Any test here must therefore go through FeatureAnalysisService.Refine, not
/// SubjectDetector.Detect.
/// </summary>
public class SubjectDetectionWiringTests : IDisposable {
    private readonly string tempDir;
    private readonly PrismConfiguration configuration =
        PrismConfiguration.LoadPrismConfig(ConfigLoader.RequireFile(PrismConfiguration.FileName));

    public SubjectDetectionWiringTests() {
        this.tempDir = Path.Combine(Path.GetTempPath(), $"prism-subject-wiring-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.tempDir);
    }

    public void Dispose() {
        if (Directory.Exists(this.tempDir)) Directory.Delete(this.tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    // A saturated block on white, large enough to clear the detector's minimum-area filters. Pixels are
    // written directly — ImageSharp.Drawing is not a dependency of this test project.
    private string WriteProductOnWhiteJpeg(string name, int size) {
        string path = Path.Combine(this.tempDir, name);
        int lo = size / 4, hi = size - (size / 4);
        using Image<Rgba32> image = new(size, size, new Rgba32(255, 255, 255));
        image.ProcessPixelRows(accessor => {
            for (int y = lo; y < hi; y++) {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = lo; x < hi; x++) row[x] = new Rgba32(200, 40, 40);
            }
        });
        image.Save(path, new JpegEncoder { Quality = 95 });
        return path;
    }

    [Fact]
    public void Refine_PopulatesSubjectOnTheRecord() {
        string path = this.WriteProductOnWhiteJpeg("product.jpg", 800);
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "product.jpg", ImportStatus = ImportStatus.Ok };
        PhenotypeRuleSet ruleSet = PhenotypeRuleSet.Load(ConfigLoader.RequireFile("ImageRoles.json"));

        new FeatureAnalysisService(this.configuration).Refine(lambda, family: null, path, ruleSet);

        // The whole point: a real image, through the real conversion, leaves a real detection behind.
        Assert.NotNull(lambda.Subject);
        Assert.Equal("classical-cv", lambda.Subject!.Producer);
        Assert.False(lambda.Subject.IsWholeFrameFallback, "detector degraded to whole-frame on a plainly separable product");
        Assert.True(lambda.Subject.Box.Width > 0 && lambda.Subject.Box.Height > 0);
    }

    [Fact]
    public void Refine_PublishesShadowPresentFeature() {
        // shadow-present must be measured before the phenotype is finalized; if the detector step is
        // skipped or throws, this stays UNKNOWN — which is exactly how the wiring defect presented.
        string path = this.WriteProductOnWhiteJpeg("shadow.jpg", 800);
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "shadow.jpg", ImportStatus = ImportStatus.Ok };
        PhenotypeRuleSet ruleSet = PhenotypeRuleSet.Load(ConfigLoader.RequireFile("ImageRoles.json"));

        new FeatureAnalysisService(this.configuration).Refine(lambda, family: null, path, ruleSet);

        Assert.NotEqual("UNKNOWN", lambda.Features.GetValue("shadow-present"));
    }

    [Fact]
    public void Refine_DoesNotThrow_SoRefinementIsNeverSilentlyLost() {
        // The refinement chain catches everything and only increments a counter, so an exception here
        // costs the phenotype AND the subject with no failure surfaced. Assert the happy path is clean.
        string path = this.WriteProductOnWhiteJpeg("clean.jpg", 800);
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "clean.jpg", ImportStatus = ImportStatus.Ok };
        PhenotypeRuleSet ruleSet = PhenotypeRuleSet.Load(ConfigLoader.RequireFile("ImageRoles.json"));

        Exception? thrown = Record.Exception(() => new FeatureAnalysisService(this.configuration).Refine(lambda, family: null, path, ruleSet));

        Assert.Null(thrown);
    }
}
