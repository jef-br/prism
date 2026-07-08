namespace Prism.Services.Matching;

/// <summary>
/// One YOLOv8n object detection. Box coordinates are normalized to [0,1] of the original
/// image dimensions (x1,y1 = top-left; x2,y2 = bottom-right).
/// </summary>
public sealed record YoloDetection(int ClassId, string ClassName, float Confidence, float X1, float Y1, float X2, float Y2)
{
    /// <summary>Normalized box area (fraction of the image).</summary>
    public float Area => MathF.Max(0f, X2 - X1) * MathF.Max(0f, Y2 - Y1);

    /// <summary>True when this detection is the COCO "person" class.</summary>
    public bool IsPerson => ClassId == 0;
}
