using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Services.Matching;

/// <summary>
/// In-process FeatureAnalysis implementation. Delegates to <see cref="ImageFeatureAnalyzer"/> — CPU-only,
/// no model assets. Loads the analyzer chain configuration once (cached process-wide); a missing
/// analyzer_Config.json fails the job loudly instead of degrading. Internal to Matching.
/// </summary>
public sealed class FeatureAnalysisService : IFeatureAnalysisService
{
    private const string YoloModelRelativePath = "Services/Matching/Analyzers/ONNX/yolo26s.onnx";

    private readonly AnalyzerConfig analyzerConfig;
    private readonly ProductTypeResolver productTypes;
    private readonly string? yoloModelPath;

    public FeatureAnalysisService()
    {
        string? cfgPath = PrismConfigLocator.FindFolderLocalConfig("analyzer_Config.json");
        if (cfgPath is null)
            throw new PrismConfigurationException(
                "analyzer_Config.json not found. Ensure analyzer_Config.json is present in the config directory next to Prism_Config.json.");

        analyzerConfig = ConfigCache.GetOrLoad(() => AnalyzerConfig.Load(cfgPath), cfgPath);

        string? mapPath = PrismConfigLocator.FindFolderLocalConfig("ProductTypeMap.json");
        if (mapPath is null)
            throw new PrismConfigurationException(
                "ProductTypeMap.json not found. Ensure ProductTypeMap.json is present in the config directory next to Prism_Config.json.");

        productTypes = ConfigCache.GetOrLoad(() => ProductTypeResolver.Load(mapPath), mapPath);

        // The 37 MB detector is not copied into build outputs; FindModelAsset resolves it from the
        // deployed location, the PRISM_ONNX_MODEL_DIR override, or the single source-tree copy.
        yoloModelPath = PrismConfigLocator.FindModelAsset(YoloModelRelativePath);
        if (yoloModelPath is null)
            throw new PrismConfigurationException(
                "YOLO26 ONNX model not found. Deploy Services/Matching/Analyzers/ONNX/yolo26s.onnx next to " +
                "Prism_Config.json, set PRISM_ONNX_MODEL_DIR, or keep the source-tree copy under jb/src/core/.");
    }

    /// <inheritdoc/>
    public void Analyze(Image<Rgba32> image, ImageFeatureSnapshot target)
        => ImageFeatureAnalyzer.Analyze(image, target, analyzerConfig);

    /// <inheritdoc/>
    public void Refine(ImageRecord_LAMBDA lambda, FamilyIDRecord? family, string? imagePath, PhenotypeRuleSet ruleSet)
        => ImageFeatureAnalyzer.Refine(lambda, family, imagePath, ruleSet, analyzerConfig, yoloModelPath, productTypes);
}
