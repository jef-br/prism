namespace Prism.Contracts;

/// <summary>
/// CLIP classification token collection attached to an image during the Classified stage.
/// </summary>
public sealed record TagCollection {
    /// <summary>Tokens whose cosine similarity score exceeded the influential threshold.</summary>
    public ClassificationToken[] Influential { get; init; } = [];

    /// <summary>Tokens below the influential threshold, retained for diagnostics.</summary>
    public ClassificationToken[] Trivial { get; init; } = [];
}
