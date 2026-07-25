namespace Prism.Lib.Zip;

/// <summary>
/// Describes how a zip member should be handled by the zip foundation module.
/// </summary>
public enum ZipMemberMediaKind {
    /// <summary>
    /// The member is not processable by PRISM import rules and is omitted silently.
    /// </summary>
    Ignored = 0,

    /// <summary>
    /// The member is an accepted image or image-like document.
    /// </summary>
    Image = 1,

    /// <summary>
    /// The member is an accepted Excel workbook.
    /// </summary>
    Excel = 2,

    /// <summary>
    /// The member is a nested zip archive whose contents should be inspected.
    /// </summary>
    NestedZip = 3
}
