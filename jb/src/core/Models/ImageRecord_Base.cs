namespace Prism.Contracts;

/// <summary>Base fields shared by all image record types across the pipeline.</summary>
public class ImageRecord_Base {
    public string InitialFullName { get; set; } = string.Empty;
    /// <summary>Image width in pixels, set during Ingress.</summary>
    public int Width { get; set; }
    /// <summary>Image height in pixels, set during Ingress.</summary>
    public int Height { get; set; }
    /// <summary>FamilyID assigned by the Matched stage and confirmed by the Ordered stage.</summary>
    public string Family { get; set; } = string.Empty;
    /// <summary>Zero-based det-slot index assigned by the Ordered stage.</summary>
    public int DetOrder { get; set; }
    /// <summary>Computed output filename in the form <c>{Family}_det{DetOrder}.jpg</c>. Consumed by the Exported stage.</summary>
    public string NewName => $"{this.Family}_det{this.DetOrder}.jpg";
    public string? Checksum { get; set; }
    /// <summary>Import outcome set by the Imported stage.</summary>
    public ImportStatus ImportStatus { get; set; } = ImportStatus.Pending;
    /// <summary>Machine-readable KO reason code set by whichever stage rejects this image.</summary>
    public string? KoReasonCode { get; set; }
    /// <summary>Human-readable KO message for the manifest.</summary>
    public string? KoSafeMessage { get; set; }
}
