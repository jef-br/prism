using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Services.Matching;

/// <summary>
/// Per-job Classification wrapper. Borrows the shared <see cref="ImageClassifier"/> and prompt catalogue
/// from <see cref="MatchingService"/> (which owns both for the app lifetime) and performs ONNX tagging and
/// perceptual-hash duplicate grouping. <see cref="Dispose"/> is a no-op — the classifier is not owned here.
/// </summary>
public sealed class ClassificationService : IClassificationService
{
    private readonly ImageClassifier classifier;
    private readonly ClipPromptCatalog promptCatalog;
    private readonly PrismConfiguration configuration;

    internal ClassificationService(ImageClassifier classifier, ClipPromptCatalog promptCatalog, PrismConfiguration configuration)
    {
        this.classifier    = classifier;
        this.promptCatalog = promptCatalog;
        this.configuration = configuration;
    }

    /// <inheritdoc/>
    public bool IsReady => this.classifier.IsReady;

    /// <inheritdoc/>
    public void ApplyClipTags(Image<Rgba32> image, ImageRecord_LAMBDA lambda, double influentialThreshold, double cutoffThreshold)
    {
        ClassificationToken[] logitTokens = this.classifier.ClassifyImage(image, this.promptCatalog.BuildPrompts());
        this.ApplyTokens(logitTokens, lambda, influentialThreshold, cutoffThreshold);
    }

    /// <inheritdoc/>
    public void ApplyClipTagsBatch(IReadOnlyList<(Image<Rgba32> Image, ImageRecord_LAMBDA Lambda)> items, double influentialThreshold, double cutoffThreshold)
    {
        if (items.Count == 0) return;

        ClassificationToken[][] tokenSets = this.classifier.ClassifyImages(
            [.. items.Select(item => item.Image)], this.promptCatalog.BuildPrompts());

        for (int i = 0; i < items.Count; i++)
            this.ApplyTokens(tokenSets[i], items[i].Lambda, influentialThreshold, cutoffThreshold);
    }

    /// <summary>
    /// Converts raw per-prompt logits into influential/trivial tags and CLIP-derived feature values
    /// on one LAMBDA — the shared tail of the single and batched classification paths.
    /// </summary>
    private void ApplyTokens(ClassificationToken[] logitTokens, ImageRecord_LAMBDA lambda, double influentialThreshold, double cutoffThreshold)
    {
        if (logitTokens.Length == 0) return;

        // Resolve each prompt to the (feature, value) it represents; drop unrecognised prompts.
        var resolved = new List<(ClassificationToken Token, string Feature, string Value)>();
        foreach (ClassificationToken token in logitTokens)
        {
            if (this.promptCatalog.TryResolve(token.Label, out string feature, out string value))
                resolved.Add((token, feature, value));
        }

        var influential = new List<ClassificationToken>();
        var trivial     = new List<ClassificationToken>();

        // CLIP zero-shot: within each mutually-exclusive feature group (e.g. hero-orientation's
        // FRONT/BACK/SIDEON/...), softmax the prompt logits into a calibrated probability per value, then
        // take the winning value as the measurement for that feature. The influential bar is per-feature:
        // a 5-way softmax winner rarely reaches the confidence a 2-way winner does, so each group may
        // override the global threshold via Classification.Confidence_Thresholds.
        foreach (IGrouping<string, (ClassificationToken Token, string Feature, string Value)> group in resolved.GroupBy(r => r.Feature))
        {
            var members = group.ToList();
            double[] probabilities = Softmax(members.Select(m => m.Token.Confidence).ToArray());
            int winner = ArgMax(probabilities);
            double winningProbability = probabilities[winner];
            (ClassificationToken Token, string Feature, string Value) best = members[winner];

            var tag = new ClassificationToken
            {
                Label      = best.Token.Label,
                Confidence = winningProbability,
                Feature    = best.Feature,
                Value      = best.Value
            };

            double featureThreshold = this.configuration.InfluentialThresholdsByFeature.TryGetValue(best.Feature, out double overrideThreshold)
                ? overrideThreshold
                : influentialThreshold;

            if (winningProbability >= featureThreshold)
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
        => new VisualHasher(this.configuration.MaxHammingDistance).FindDuplicates(entries);

    /// <inheritdoc/>
    public void Dispose() { }

    //  Config / asset loading 

    internal static ClipPromptCatalog LoadPromptCatalog()
    {
        return ClipPromptCatalog.Load(ConfigLoader.RequireFile("ClipPrompts.json"));
    }

    /// <summary>
    /// Resolves and validates the CLIP model/tokenizer asset paths. Fails fast when any asset is
    /// missing — called before <see cref="ImageClassifier.GetShared"/> so a missing deployment asset
    /// is caught even on a call that finds the shared classifier already initialized.
    /// </summary>
    internal static (string ModelPath, string VocabPath, string MergesPath) ResolveClassifierPaths(PrismConfiguration configuration)
    {
        string pathRoot = configuration.ClipModelDir;

        // The 146 MB model is not copied into build outputs; ModelAssetLocator resolves it from the deployed
        // location, the PRISM_ONNX_MODEL_DIR override, or the single source-tree copy.
        string? modelPath  = ModelAssetLocator.Find($"{pathRoot}/{configuration.ClipModelFile}");
        string? vocabPath  = ModelAssetLocator.Find($"{pathRoot}/{configuration.ClipVocabFile}");
        string? mergesPath = ModelAssetLocator.Find($"{pathRoot}/{configuration.ClipMergesFile}");

        if (modelPath is null || vocabPath is null || mergesPath is null)
            throw new PrismConfigurationException(
                "CLIP ONNX assets not found. Deploy Images/Classify/ONNX/clip-vit-b32-uint8/ next to " +
                "Prism_Config.json, set PRISM_ONNX_MODEL_DIR, or keep the source-tree copy under jb/src/core/.");

        return (modelPath, vocabPath, mergesPath);
    }
}
