namespace Prism.Contracts;

/// <summary>
/// One image's outcome after the Transformed and Exported stages — the OUTPUT end of the
/// Base → INPUT → LAMBDA → OUTPUT record lifecycle. Attached to the parent
/// <see cref="ImageRecord_LAMBDA"/> as <c>OutputRecord</c>.
/// <para>
/// Written by two stages. Transform creates the record and fills the transform block; Export
/// enriches the same instance with the export block and re-copies the identity fields, which
/// CompactDetOrder may have renumbered since Transform ran. A KO-at-transform image keeps its
/// record but is skipped by Export, so it never receives the export block.
/// </para>
/// </summary>
public class ImageRecord_OUTPUT : ImageRecord_Base
{
    //  Transform block — written by the Transformed stage

    // Null when the Transformed stage never evaluated this image; distinct from NotEvaluated.
    public TransformationStatus? TransformStatus { get; set; }

    public string TransformerType { get; set; } = string.Empty;

    public int InputWidth { get; set; }

    public int InputHeight { get; set; }

    public int? OutputWidth { get; set; }

    public int? OutputHeight { get; set; }

    public BoundingBox? CropRectangle { get; set; }

    public string ResizeMode { get; set; } = string.Empty;

    public double ScaleFactor { get; set; } = 1.0;

    public string BackgroundFillMethod { get; set; } = string.Empty;

    public string[] Warnings { get; set; } = [];

    public string? FailureReason { get; set; }

    public string SafeSummaryText { get; set; } = string.Empty;

    //  Export block — written by the Exported stage

    public string? FinalFileName { get; set; }

    public string? Extension { get; set; }

    public string? MimeType { get; set; }

    public string? ArtifactPath { get; set; }

    public long ByteLength { get; set; }

    public string? ExportStatus { get; set; }
}
