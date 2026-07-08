namespace Prism.Core;

/// <summary>
/// Outcome of image preprocessing, cropping, centering, stretching, and cleanup for one image.
/// Set by the Transformed stage; null until that stage runs.
/// </summary>
public sealed record ImageTransformationResult
{
    /// <summary>High-level outcome of the transform decision for this image.</summary>
    public TransformationStatus Status { get; init; }

    /// <summary>Name of the <see cref="IImageTransformation"/> implementation that handled this image.</summary>
    public string TransformerType { get; init; } = string.Empty;

    /// <summary>Source image width in pixels at the time of transform.</summary>
    public int InputWidth { get; init; }

    /// <summary>Source image height in pixels at the time of transform.</summary>
    public int InputHeight { get; init; }

    /// <summary>Output image width in pixels; null when pixel processing was not performed.</summary>
    public int? OutputWidth { get; init; }

    /// <summary>Output image height in pixels; null when pixel processing was not performed.</summary>
    public int? OutputHeight { get; init; }

    /// <summary>Crop rectangle applied to the source image; null when no crop was performed.</summary>
    public BoundingBox? CropRectangle { get; init; }

    /// <summary>Resize mode applied (e.g. "upscale", "downscale", "none"); empty when not performed.</summary>
    public string ResizeMode { get; init; } = string.Empty;

    /// <summary>Linear scale factor applied during resize; 1.0 when no resize was performed.</summary>
    public double ScaleFactor { get; init; } = 1.0;

    /// <summary>Background fill method used (e.g. "edge-extension", "solid"); empty when not performed.</summary>
    public string BackgroundFillMethod { get; init; } = string.Empty;

    /// <summary>Non-fatal quality warnings recorded during transform.</summary>
    public string[] Warnings { get; init; } = [];

    /// <summary>Human-readable reason when <see cref="Status"/> is <see cref="TransformationStatus.Ko"/>.</summary>
    public string? FailureReason { get; init; }

    /// <summary>One-line safe summary of the transform outcome, suitable for the manifest.</summary>
    public string SafeSummaryText { get; init; } = string.Empty;
}
