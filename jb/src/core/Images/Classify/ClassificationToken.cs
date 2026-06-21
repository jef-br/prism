namespace Prism.Core;

/// <summary>A CLIP zero-shot classification result pairing a text prompt with its cosine similarity score.</summary>
public class ClassificationToken
{
    /// <summary>The text prompt used for zero-shot classification.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Cosine similarity score between the image embedding and this label's text embedding (0–1).</summary>
    public double Confidence { get; set; }
}
