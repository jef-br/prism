/// <summary>Base fields shared by all image record types across the pipeline.</summary>
public class ImageRecord_Base
{
    /// <summary>Original filename as provided at import time.</summary>
    public string InitialFullName { get; set; } = string.Empty;

    /// <summary>Image width in pixels, set to normalized dimensions by the Imported stage.</summary>
    public int Width { get; set; }

    /// <summary>Image height in pixels, set to normalized dimensions by the Imported stage.</summary>
    public int Height { get; set; }

    /// <summary>FamilyID assigned by the Matched stage and confirmed by the Ordered stage.</summary>
    public string Family { get; set; } = string.Empty;

    /// <summary>Zero-based det-slot index assigned by the Ordered stage.</summary>
    public int DetOrder { get; set; }

    /// <summary>Computed output filename in the form <c>{Family}_det{DetOrder}.jpg</c>. Consumed by the Exported stage.</summary>
    public string NewName => $"{Family}_det{DetOrder}.jpg";
}
