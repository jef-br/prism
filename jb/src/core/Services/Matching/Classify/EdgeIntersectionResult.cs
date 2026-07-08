namespace Prism.Core;

/// <summary>
/// Per-edge subject-to-boundary intersection state produced by <see cref="SubjectEdgeDetector"/>.
/// </summary>
public readonly record struct EdgeIntersectionResult(
    bool IntersectsTop,
    bool IntersectsBottom,
    bool IntersectsLeft,
    bool IntersectsRight,
    int  IntersectionCount)
{
    /// <summary>True when the subject does not intersect any image edge.</summary>
    public bool FullyInFrame => IntersectionCount == 0;
}
