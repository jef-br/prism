namespace Prism.Core;

/// <summary>A CLIP zero-shot classification result pairing a text prompt with its cosine similarity score.</summary>
public class ClassificationToken
{
    /// <summary>The text prompt used for zero-shot classification.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Cosine similarity score between the image embedding and this label's text embedding (0–1).</summary>
    public double Confidence { get; set; }

    /// <summary>The ImageFeature id this prompt measures (e.g. "product-color"). Empty on raw logit tokens.</summary>
    public string Feature { get; set; } = string.Empty;

    /// <summary>The feature value this prompt represents (e.g. "red") — the token matched against Excel
    /// columns by <see cref="ClipLabelEnricher"/>. Empty on raw logit tokens.</summary>
    public string Value { get; set; } = string.Empty;
}
