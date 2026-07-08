namespace Prism.Contracts;

/// <summary> Describes where an image originated. </summary>
public enum ImageSourceKind {
    /// <summary>Source kind not yet determined.</summary>
    Unknown = 0,

    /// <summary>Local file path supplied directly.</summary>
    LocalPath = 1,

    /// <summary>Member of a local folder scan.</summary>
    FolderMember = 2,

    /// <summary>In-memory stream with caller-supplied metadata.</summary>
    Stream = 3,

    /// <summary>Multipart upload from the API.</summary>
    MultipartUpload = 4,

    /// <summary>Fetched from a remote URL.</summary>
    RemoteUrl = 5,

    /// <summary>Extracted member from a zip archive.</summary>
    ZipMember = 6
}
