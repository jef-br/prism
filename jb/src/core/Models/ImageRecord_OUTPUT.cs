namespace Prism.Core;

/*
Represents the final output image bytes and output filename metadata.
Populated by the Exported stage via Exporter.BuildOutputRecords.
*/

/// <summary>
/// Represents one image's export state after the Exported stage completes.
/// Attached to the parent <see cref="ImageRecord_LAMBDA"/> as <c>OutputRecord</c>.
/// Only non-KO lambda records receive an OutputRecord.
/// </summary>
public class ImageRecord_OUTPUT : ImageRecord_Base
{
    /// <summary>Output filename in the form <c>{Family}_det{DetOrder}.jpg</c>.</summary>
    public string? FinalFileName { get; init; }

    /// <summary>File extension, always <c>.jpg</c> for normalized pipeline output.</summary>
    public string? Extension { get; init; }

    /// <summary>MIME type, always <c>image/jpeg</c> for normalized pipeline output.</summary>
    public string? MimeType { get; init; }

    /// <summary>Absolute path to the normalized JPG artifact on disk.</summary>
    public string? ArtifactPath { get; init; }

    /// <summary>Size in bytes of the artifact at <see cref="ArtifactPath"/>. 0 when the file does not exist.</summary>
    public long ByteLength { get; init; }

    /// <summary>"Ok" when the image was exported successfully.</summary>
    public string? ExportStatus { get; init; }
}
