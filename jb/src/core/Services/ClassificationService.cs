using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Core;

/// <summary>
/// Per-job Classification wrapper. Borrows the shared <see cref="ImageClassifier"/> and prompt catalogue
/// from <see cref="MatchingService"/> (which owns both for the app lifetime) and performs ONNX tagging and
/// perceptual-hash duplicate grouping. <see cref="Dispose"/> is a no-op — the classifier is not owned here.
/// </summary>
public sealed class ClassificationService : IClassificationService
{
    private readonly ImageClassifier classifier;
    private readonly ClipPromptCatalog promptCatalog;
    private readonly int maxHammingDistance;

    internal ClassificationService(ImageClassifier classifier, ClipPromptCatalog promptCatalog, int maxHammingDistance)
    {
        this.classifier         = classifier;
        this.promptCatalog      = promptCatalog;
        this.maxHammingDistance = maxHammingDistance;
    }

    /// <inheritdoc/>
    public bool IsReady => classifier.IsReady;

    /// <inheritdoc/>
    public void ApplyClipTags(Image<Rgba32> image, ImageRecord_LAMBDA lambda, double influentialThreshold, double cutoffThreshold)
    {
        ClassificationToken[] logitTokens = classifier.ClassifyImage(image, promptCatalog.BuildPrompts());
        if (logitTokens.Length == 0) return;

        // Resolve each prompt to the (feature, value) it represents; drop unrecognised prompts.
        var resolved = new List<(ClassificationToken Token, string Feature, string Value)>();
        foreach (ClassificationToken token in logitTokens)
        {
            if (promptCatalog.TryResolve(token.Label, out string feature, out string value))
                resolved.Add((token, feature, value));
        }

        var influential = new List<ClassificationToken>();
        var trivial     = new List<ClassificationToken>();

        // CLIP zero-shot: within each mutually-exclusive feature group (e.g. hero-orientation's
        // FRONT/BACK/SIDEON/...), softmax the prompt logits into a calibrated probability per value, then
        // take the winning value as the measurement for that feature.
        foreach (IGrouping<string, (ClassificationToken Token, string Feature, string Value)> group in resolved.GroupBy(r => r.Feature))
        {
            var members = group.ToList();
            double[] probabilities = Softmax(members.Select(m => m.Token.Confidence).ToArray());
            int winner = ArgMax(probabilities);
            double winningProbability = probabilities[winner];
            (ClassificationToken Token, string Feature, string Value) best = members[winner];

            var tag = new ClassificationToken { Label = best.Token.Label, Confidence = winningProbability };

            if (winningProbability >= influentialThreshold)
            {
                influential.Add(tag);
                lambda.Features.Set(best.Feature, best.Value, winningProbability, "clip");
            }
            else if (winningProbability >= cutoffThreshold)
            {
                trivial.Add(tag);
            }
        }

        lambda.Tags = new TagCollection { Influential = [.. influential], Trivial = [.. trivial] };
    }

    /// <summary>Numerically-stable softmax over a feature group's prompt logits.</summary>
    private static double[] Softmax(double[] logits)
    {
        double max = logits.Max();
        var exp = new double[logits.Length];
        double sum = 0;

        for (int i = 0; i < logits.Length; i++)
        {
            exp[i] = Math.Exp(logits[i] - max);
            sum += exp[i];
        }

        for (int i = 0; i < logits.Length; i++)
            exp[i] /= sum;

        return exp;
    }

    /// <summary>Index of the largest value.</summary>
    private static int ArgMax(double[] values)
    {
        int best = 0;
        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] > values[best]) best = i;
        }
        return best;
    }

    /// <inheritdoc/>
    public IReadOnlyList<DedupGroup> FindDuplicates(IReadOnlyList<(ImageRecord_INPUT Record, UInt128 Hash)> entries)
        => new VisualHasher(maxHammingDistance).FindDuplicates(entries);

    /// <inheritdoc/>
    public void Dispose() { }

    //  Config / asset loading 

    internal static ClipPromptCatalog LoadPromptCatalog()
    {
        string? clipPromptsPath = PrismConfigLocator.FindFolderLocalConfig("ClipPrompts.json");
        if (clipPromptsPath is null)
            throw new PrismConfigurationException(
                "ClipPrompts.json not found. Ensure ClipPrompts.json is present in the config directory next to Prism_Config.json.");

        return ClipPromptCatalog.Load(clipPromptsPath);
    }

    internal static void InitializeClassifier(ImageClassifier classifier)
    {
        string pathRoot = "Images/Classify/ONNX/clip-vit-b32-uint8";

        // The 146 MB model is not copied into build outputs; FindModelAsset resolves it from the deployed
        // location, the PRISM_ONNX_MODEL_DIR override, or the single source-tree copy.
        string? modelPath  = PrismConfigLocator.FindModelAsset($"{pathRoot}/model_uint8.onnx");
        string? vocabPath  = PrismConfigLocator.FindModelAsset($"{pathRoot}/vocab.json");
        string? mergesPath = PrismConfigLocator.FindModelAsset($"{pathRoot}/merges.txt");

        if (modelPath is null || vocabPath is null || mergesPath is null)
            throw new PrismConfigurationException(
                "CLIP ONNX assets not found. Deploy Images/Classify/ONNX/clip-vit-b32-uint8/ next to " +
                "Prism_Config.json, set PRISM_ONNX_MODEL_DIR, or keep the source-tree copy under jb/src/core/.");

        classifier.Initialize(modelPath, vocabPath, mergesPath);
    }
}
