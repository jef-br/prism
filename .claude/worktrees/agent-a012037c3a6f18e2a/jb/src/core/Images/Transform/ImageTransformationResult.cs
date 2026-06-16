/*
Represents the outcome of image preprocessing, cropping, centering, stretching, and cleanup.

*/

/// <summary>
/// Outcome of image preprocessing, cropping, centering, stretching, and cleanup.
/// </summary>
public sealed record ImageTransformationResult
{
    /// <summary>
    /// Human-readable status for the current transformation result.
    /// </summary>
    public string Status { get; init; } = string.Empty;
}
