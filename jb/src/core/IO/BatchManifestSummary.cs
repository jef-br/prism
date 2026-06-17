/// <summary>
/// Batch-level counts projected into the manifest.
/// </summary>
public sealed record BatchManifestSummary
{
    /// <summary>Number of accepted image records.</summary>
    public int ImageCount { get; init; }

    /// <summary>Number of accepted Excel records.</summary>
    public int ExcelCount { get; init; }

    /// <summary>Number of accepted zip records.</summary>
    public int ZipCount { get; init; }

    /// <summary>Number of OK renamed outputs.</summary>
    public int OkRenamed { get; init; }

    /// <summary>Number of KO records accumulated across all stages.</summary>
    public int KoRecords { get; init; }

    /// <summary>Number of non-KO images that received a transform decision.</summary>
    public int OkTransformed { get; init; }

    /// <summary>Number of images that were KO'd during the Transformed stage.</summary>
    public int KoTransformed { get; init; }

    /// <summary>Number of generated image records created by the Generated stage.</summary>
    public int GeneratedCount { get; init; }
}
