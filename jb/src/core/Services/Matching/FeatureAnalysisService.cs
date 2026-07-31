using System.Runtime.InteropServices;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Services.Matching;

/// <summary>
/// In-process FeatureAnalysis implementation. Delegates to <see cref="ImageFeatureAnalyzer"/> — CPU-only,
/// no model assets. Composes the analyzer chain's parameter bundle once at construction, so a missing
/// or invalid analyzer_Config.json fails the host at startup instead of degrading. Internal to Matching.
/// </summary>
public sealed class FeatureAnalysisService : IFeatureAnalysisService {
    private const string YoloModelRelativePath = "Services/Matching/Analyzers/ONNX/yolo26s.onnx";

    private readonly AnalyzerParameters analyzerParameters;
    private readonly ClassifyParameters classifyParameters;
    private readonly ProductTypeResolver productTypes;
    private readonly string? yoloModelPath;
    private readonly SubjectDetector subjectDetector;

    public FeatureAnalysisService() {
        this.analyzerParameters = AnalyzerParameters.FromConfig();
        this.classifyParameters = ClassifyParameters.FromConfig();

        // Built once per service, not per image — same rule the Transform stage follows for its own
        // parameter bundle. A bad SubjectDetector section fails the host at startup, not mid-job.
        this.subjectDetector = SubjectDetector.FromConfig();

        this.productTypes = ProductTypeResolver.Load(ConfigLoader.RequireFile("ProductTypeMap.json"));

        // The 37 MB detector is not copied into build outputs; ModelAssetLocator resolves it from the
        // deployed location, the PRISM_ONNX_MODEL_DIR override, or the single source-tree copy.
        this.yoloModelPath = ModelAssetLocator.Find(YoloModelRelativePath);
        if (this.yoloModelPath is null)
            throw new PrismConfigurationException(
                "YOLO26 ONNX model not found. Deploy Services/Matching/Analyzers/ONNX/yolo26s.onnx next to " +
                "Prism_Config.json, set PRISM_ONNX_MODEL_DIR, or keep the source-tree copy under jb/src/core/.");
    }

    /// <inheritdoc/>
    public void Analyze(Image<Rgba32> image, ImageFeatureSnapshot target)
        => ImageFeatureAnalyzer.Analyze(image, target, this.analyzerParameters, this.classifyParameters.ImageFeatureAnalyzer);

    /// <inheritdoc/>
    public void Refine(ImageRecord_LAMBDA lambda, FamilyIDRecord? family, string? imagePath, PhenotypeRuleSet ruleSet)
        => ImageFeatureAnalyzer.Refine(lambda, family, imagePath, ruleSet, this.analyzerParameters, this.yoloModelPath, this.productTypes,
            (record, image) => this.DetectSubject(record, image, family));

    // Wave-3 subject isolation. Seeded with the Excel + CLIP signals resolved from the record and its
    // family, so the detector can decide whether CLAHE is worth its cost and how hard to work on a
    // non-flat background before it runs, rather than being told after the fact.
    private void DetectSubject(ImageRecord_LAMBDA lambda, Image<Rgba32> image, FamilyIDRecord? family) {
        // A real alpha channel captured at ingress is exact geometry; never overwrite it with a heuristic.
        if (lambda.Subject is not null) return;

        // Edge-bleed shortcut: SubjectEdgeDetector (Classified stage) already measured every edge. When
        // the product touches all four, there is no background ring left to fit a box against — the
        // classical-CV pass on this kind of image was the T-4980 defect (a stray high-contrast patch, not
        // the garment, won promotion). The frame itself is the subject; skip detection and crop square.
        if (lambda.Features.GetValue("intersection-count") == "4") {
            lambda.BoundingBox = new BoundingBox { X = 0, Y = 0, Width = image.Width, Height = image.Height, Left = 0, Top = 0, Right = image.Width, Bottom = image.Height };
            lambda.Subject = new SubjectDetectionResult {
                Box = lambda.BoundingBox.Value,
                IntersectsTop = true,
                IntersectsBottom = true,
                IntersectsLeft = true,
                IntersectsRight = true,
                IsWholeFrameFallback = true,
                Producer = "edge-bleed"
            };
            return;
        }

        SubjectSeedHint seed = SubjectSeedHint.Resolve(lambda.Features, family);
        using Mat bgr = ToBgrMat(image);
        lambda.Subject = this.subjectDetector.Detect(bgr, seed);
    }

    // ImageSharp RGBA → OpenCvSharp BGR. The analyzer chain already holds this image decoded, so this
    // conversion replaces a second decode from disk, not a first one.
    private static Mat ToBgrMat(Image<Rgba32> image) {
        int w = image.Width, h = image.Height;
        byte[] bgr = new byte[w * h * 3];
        image.ProcessPixelRows(accessor => {
            int i = 0;
            for (int y = 0; y < h; y++) {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < w; x++) {
                    bgr[i++] = row[x].B;
                    bgr[i++] = row[x].G;
                    bgr[i++] = row[x].R;
                }
            }
        });

        // Copy into the Mat's own buffer. Mat.SetArray rejects a byte[] against CV_8UC3 ("Mat data type is
        // not compatible"), and the Array-taking constructor wraps the caller's array without owning it,
        // which would leave the Mat pointing at collectable memory. A freshly allocated Mat is continuous,
        // so one linear copy is correct.
        Mat mat = new(h, w, MatType.CV_8UC3);
        Marshal.Copy(bgr, 0, mat.Data, bgr.Length);
        return mat;
    }
}
