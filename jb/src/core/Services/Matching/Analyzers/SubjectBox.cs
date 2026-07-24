namespace Prism.Services.Matching;

/// <summary>
/// The normalized [0,1] bounding box of the main subject in an image, with the confidence and
/// source of the estimate ("yolo" detection or "foreground" color-distance fallback).
/// </summary>
public sealed record SubjectBox(float X1, float Y1, float X2, float Y2, float Confidence, string Source) {
    public float Width  => MathF.Max(0f, X2 - X1);
    public float Height => MathF.Max(0f, Y2 - Y1);
    public float Area   => Width * Height;

    // Midpoint math (/2) — structural, never tuned.
#pragma warning disable S109
    public float CenterX => (X1 + X2) / 2f;
    public float CenterY => (Y1 + Y2) / 2f;
#pragma warning restore S109
}
