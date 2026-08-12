using System.Runtime.InteropServices;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Services.Matching;

/// <summary>
/// In-process FeatureAnalysis implementation. Delegates to <see cref="ImageFeatureAnalyzer"/>, which runs
/// the YOLO26 detector (GPU via DirectML, shared session) and the classical-CV subject detector on top of
/// the CPU analyzer chain. Composes the analyzer chain's parameter bundle and resolves the YOLO26 model
/// asset once at construction, so a missing or invalid analyzer_Config.json — or a missing model — fails
/// the host at startup instead of degrading. Internal to Matching.
/// </summary>
public sealed class FeatureAnalysisService : IFeatureAnalysisService {
    private readonly AnalyzerParameters analyzerParameters;
    private readonly ClassifyParameters classifyParameters;
    private readonly ProductTypeResolver productTypes;
    private readonly string? yoloModelPath;
    private readonly bool aiDetectionEnabled;
    private readonly SubjectDetector subjectDetector;

    public FeatureAnalysisService(PrismConfiguration configuration) {
        this.analyzerParameters = AnalyzerParameters.FromConfig();
        this.classifyParameters = ClassifyParameters.FromConfig();

        // Built once per service, not per image — same rule the Transform stage follows for its own
        // parameter bundle. A bad SubjectDetector section fails the host at startup, not mid-job.
        this.subjectDetector = SubjectDetector.FromConfig();

        this.productTypes = ProductTypeResolver.Load(ConfigLoader.RequireFile("ProductTypeMap.json"));

        // The 37 MB detector is not copied into build outputs; ModelAssetLocator resolves it from the
        // deployed location, the PRISM_ONNX_MODEL_DIR override, or the single source-tree copy. With
        // Models.Detection.UseIt false the asset is never resolved and the path stays null — the same
        // "no detector" state Refine already handles, so every analyzer still runs and simply receives an
        // empty detection list.
        this.aiDetectionEnabled = configuration.AiDetectionEnabled;
        if (!this.aiDetectionEnabled) return;

        this.yoloModelPath = ModelAssetLocator.Find(configuration.YoloModelPath);
        if (this.yoloModelPath is null)
            throw new PrismConfigurationException(
                $"YOLO26 ONNX model not found at '{configuration.YoloModelPath}'. Deploy it next to " +
                "Prism_Config.json, set PRISM_ONNX_MODEL_DIR, or keep the source-tree copy under jb/src/core/.");
    }

    /// <inheritdoc/>
    public void Analyze(Image<Rgba32> image, ImageFeatureSnapshot target)
        => ImageFeatureAnalyzer.Analyze(image, target, this.analyzerParameters, this.classifyParameters.ImageFeatureAnalyzer);

    /// <inheritdoc/>
    public void Refine(ImageRecord_LAMBDA lambda, FamilyIDRecord? family, string? imagePath, PhenotypeRuleSet ruleSet)
        => ImageFeatureAnalyzer.Refine(lambda, family, imagePath, ruleSet, this.analyzerParameters, this.yoloModelPath, this.aiDetectionEnabled, this.productTypes,
            (record, image) => this.DetectSubject(record, image, family));

    // Wave-3 subject isolation. Seeded with the Excel + CLIP signals resolved from the record and its
    // family, so the detector can decide whether CLAHE is worth its cost and how hard to work on a
    // non-flat background before it runs, rather than being told after the fact.
    private void DetectSubject(ImageRecord_LAMBDA lambda, Image<Rgba32> image, FamilyIDRecord? family) {
        // Edge-bleed shortcut: SubjectEdgeDetector (Classified stage) already measured every edge. When
        // the product touches all four, there is no background ring left to fit a box against — the
        // classical-CV pass on this kind of image was the T-4980 defect (a stray high-contrast patch, not
        // the garment, won promotion). The frame itself is the subject; skip detection.
        //
        // This is a positive detection, not a fallback, and it has to travel the same promotion path as
        // any other: ImagePreProcessor.PreprocessAsync overwrites lambda.BoundingBox unconditionally with
        // the legacy salient box before FinalizeGeometry runs, so a box written directly onto the record
        // here would be discarded and never reach Tx_DetailCropper, which reads lambda.BoundingBox.
        // IsWholeFrameFallback therefore stays false — it means "no subject found, keep the legacy box",
        // the opposite of what this branch concluded — and Confidence is 1.0 because all four edges were
        // measured directly rather than inferred. ImageTransformer.PreferSubjectGeometry then promotes
        // this box into lambda.BoundingBox after the overwrite, which is where the consumers read it.
        if (lambda.Features.GetValue("intersection-count") == "4") {
            lambda.Subject = new SubjectDetectionResult {
                Box = new BoundingBox { X = 0, Y = 0, Width = image.Width, Height = image.Height, Left = 0, Top = 0, Right = image.Width, Bottom = image.Height },
                IntersectsTop = true,
                IntersectsBottom = true,
                IntersectsLeft = true,
                IntersectsRight = true,
                IsWholeFrameFallback = false,
                Confidence = 1.0,
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
