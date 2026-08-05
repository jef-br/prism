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
    /// <summary>
    /// Position on the configured-slot axis the Ordered stage placed this image at: the slot index for a
    /// phenotype winner, the filename-hint or unhinted anchor for an overflow image. Fractional by design —
    /// an overflow image sits *between* configured slots. Det compaction orders on this, not on DetOrder,
    /// so an overflow image is not pushed behind a late configured slot it should precede.
    /// </summary>
    public double DetOrderAxis { get; set; }
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
