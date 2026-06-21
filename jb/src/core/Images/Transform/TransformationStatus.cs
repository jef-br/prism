namespace Prism.Core;

/// <summary>
/// Outcome of the transform routing and execution decision for a single image.
/// </summary>
public enum TransformationStatus
{
    /// <summary>The Transformed stage has not yet evaluated this image.</summary>
    NotEvaluated,

    /// <summary>Transformation was skipped because the job's Transform parameter was false.</summary>
    Skipped,

    /// <summary>Transform route was selected but pixel processing is deferred — preprocessor unavailable.</summary>
    Gated,

    /// <summary>Image was successfully transformed and pixels were updated.</summary>
    Ok,

    /// <summary>Transform failed; image is KO'd by the Transformed stage.</summary>
    Ko
}
