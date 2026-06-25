using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Core;

/// <summary>
/// Internal to Matching — not visible to the orchestrator. ONNX CLIP inference plus visual
/// deduplication; both steps are compute-heavy and both require the image in memory, so they live
/// together. Holds the CLIP session for the duration of a job and is disposed when matching finishes.
/// </summary>
public interface IClassificationService : IDisposable
{
    /// <summary>True when the CLIP session initialized and is ready to classify.</summary>
    bool IsReady { get; }

    /// <summary>Classifies the pre-loaded image and writes influential/trivial tags plus CLIP-derived features onto the LAMBDA.</summary>
    void ApplyClipTags(Image<Rgba32> image, ImageRecord_LAMBDA lambda, double influentialThreshold, double cutoffThreshold);

    /// <summary>Groups visually duplicate images by perceptual hash for post-classification suppression.</summary>
    IReadOnlyList<DedupGroup> FindDuplicates(IReadOnlyList<(ImageRecord_INPUT Record, ulong Hash)> entries);
}
