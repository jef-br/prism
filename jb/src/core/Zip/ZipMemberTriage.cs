using System;
using System.IO;

namespace Prism.Core;

/// <summary>
/// Classifies zip members by content signature and accepted PRISM filename extensions.
/// </summary>
internal static class ZipMemberTriage
{
    /// <summary>
    /// Determines whether a zip member has a filename that PRISM would try to process.
    /// </summary>
    /// <param name="memberPath">Member path from the zip archive.</param>
    /// <returns>True when the member extension is image, document, Excel, or zip.</returns>
    public static bool HasProcessableFileName(string memberPath)
    {
        string extension = Path.GetExtension(memberPath);

        return IsImageOrDocumentExtension(extension)
            || IsExcelExtension(extension)
            || IsZipExtension(extension);
    }

    /// <summary>
    /// Triages a zip member into the processable media kind PRISM should handle.
    /// </summary>
    /// <param name="memberPath">Member path from the zip archive.</param>
    /// <param name="headerBytes">Initial bytes read from the member stream.</param>
    /// <returns>The detected media kind, or Ignored for non-processable members.</returns>
    public static ZipMemberMediaKind TriageProcessableMediaKind(
        string memberPath,
        ReadOnlySpan<byte> headerBytes)
    {
        if (HasImageOrDocumentSignature(headerBytes))
        {
            return ZipMemberMediaKind.Image;
        }

        if (!HasZipSignature(headerBytes))
        {
            return ZipMemberMediaKind.Ignored;
        }

        string extension = Path.GetExtension(memberPath);

        if (IsExcelExtension(extension))
        {
            return ZipMemberMediaKind.Excel;
        }

        if (IsZipExtension(extension))
        {
            return ZipMemberMediaKind.NestedZip;
        }

        return ZipMemberMediaKind.Ignored;
    }

    /// <summary>
    /// Determines whether a media kind is extracted as an importer input.
    /// </summary>
    /// <param name="mediaKind">Triaged media kind.</param>
    /// <returns>True for image and Excel members.</returns>
    public static bool IsImporterInput(ZipMemberMediaKind mediaKind)
    {
        return mediaKind is ZipMemberMediaKind.Image or ZipMemberMediaKind.Excel;
    }

    /// <summary>
    /// Determines whether the supplied bytes match a supported image or document signature.
    /// </summary>
    /// <param name="headerBytes">Initial member bytes.</param>
    /// <returns>True when the bytes match a PRISM image/document input.</returns>
    private static bool HasImageOrDocumentSignature(ReadOnlySpan<byte> headerBytes)
    {
        return HasJpegSignature(headerBytes)
            || HasPngSignature(headerBytes)
            || HasGifSignature(headerBytes)
            || HasBmpSignature(headerBytes)
            || HasTiffSignature(headerBytes)
            || HasPdfSignature(headerBytes)
            || HasWebpSignature(headerBytes);
    }

    /// <summary>
    /// Determines whether an extension belongs to supported image or document media.
    /// </summary>
    /// <param name="extension">Filename extension including the dot.</param>
    /// <returns>True when the extension is processable as image/document input.</returns>
    private static bool IsImageOrDocumentExtension(string extension)
    {
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether an extension belongs to accepted Excel input.
    /// </summary>
    /// <param name="extension">Filename extension including the dot.</param>
    /// <returns>True for .xlsx.</returns>
    private static bool IsExcelExtension(string extension)
    {
        return extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether an extension belongs to a nested zip archive.
    /// </summary>
    /// <param name="extension">Filename extension including the dot.</param>
    /// <returns>True for .zip.</returns>
    private static bool IsZipExtension(string extension)
    {
        return extension.Equals(".zip", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether bytes match a JPEG signature.
    /// </summary>
    /// <param name="headerBytes">Initial member bytes.</param>
    /// <returns>True when the bytes start with a JPEG marker.</returns>
    private static bool HasJpegSignature(ReadOnlySpan<byte> headerBytes)
    {
        return headerBytes.Length >= 3
            && headerBytes[0] == 0xFF
            && headerBytes[1] == 0xD8
            && headerBytes[2] == 0xFF;
    }

    /// <summary>
    /// Determines whether bytes match a PNG signature.
    /// </summary>
    /// <param name="headerBytes">Initial member bytes.</param>
    /// <returns>True when the bytes start with a PNG marker.</returns>
    private static bool HasPngSignature(ReadOnlySpan<byte> headerBytes)
    {
        return headerBytes.Length >= 8
            && headerBytes[0] == 0x89
            && headerBytes[1] == 0x50
            && headerBytes[2] == 0x4E
            && headerBytes[3] == 0x47
            && headerBytes[4] == 0x0D
            && headerBytes[5] == 0x0A
            && headerBytes[6] == 0x1A
            && headerBytes[7] == 0x0A;
    }

    /// <summary>
    /// Determines whether bytes match a GIF signature.
    /// </summary>
    /// <param name="headerBytes">Initial member bytes.</param>
    /// <returns>True when the bytes start with a GIF marker.</returns>
    private static bool HasGifSignature(ReadOnlySpan<byte> headerBytes)
    {
        return headerBytes.Length >= 6
            && headerBytes[0] == 0x47
            && headerBytes[1] == 0x49
            && headerBytes[2] == 0x46
            && headerBytes[3] == 0x38
            && (headerBytes[4] == 0x37 || headerBytes[4] == 0x39)
            && headerBytes[5] == 0x61;
    }

    /// <summary>
    /// Determines whether bytes match a BMP signature.
    /// </summary>
    /// <param name="headerBytes">Initial member bytes.</param>
    /// <returns>True when the bytes start with a BMP marker.</returns>
    private static bool HasBmpSignature(ReadOnlySpan<byte> headerBytes)
    {
        return headerBytes.Length >= 2
            && headerBytes[0] == 0x42
            && headerBytes[1] == 0x4D;
    }

    /// <summary>
    /// Determines whether bytes match a TIFF signature.
    /// </summary>
    /// <param name="headerBytes">Initial member bytes.</param>
    /// <returns>True when the bytes start with a TIFF marker.</returns>
    private static bool HasTiffSignature(ReadOnlySpan<byte> headerBytes)
    {
        bool littleEndianTiff = headerBytes.Length >= 4
            && headerBytes[0] == 0x49
            && headerBytes[1] == 0x49
            && headerBytes[2] == 0x2A
            && headerBytes[3] == 0x00;

        bool bigEndianTiff = headerBytes.Length >= 4
            && headerBytes[0] == 0x4D
            && headerBytes[1] == 0x4D
            && headerBytes[2] == 0x00
            && headerBytes[3] == 0x2A;

        return littleEndianTiff || bigEndianTiff;
    }

    /// <summary>
    /// Determines whether bytes match a PDF signature.
    /// </summary>
    /// <param name="headerBytes">Initial member bytes.</param>
    /// <returns>True when the bytes start with a PDF marker.</returns>
    private static bool HasPdfSignature(ReadOnlySpan<byte> headerBytes)
    {
        return headerBytes.Length >= 4
            && headerBytes[0] == 0x25
            && headerBytes[1] == 0x50
            && headerBytes[2] == 0x44
            && headerBytes[3] == 0x46;
    }

    /// <summary>
    /// Determines whether bytes match a WebP signature.
    /// </summary>
    /// <param name="headerBytes">Initial member bytes.</param>
    /// <returns>True when the bytes start with a WebP RIFF marker.</returns>
    private static bool HasWebpSignature(ReadOnlySpan<byte> headerBytes)
    {
        return headerBytes.Length >= 12
            && headerBytes[0] == 0x52
            && headerBytes[1] == 0x49
            && headerBytes[2] == 0x46
            && headerBytes[3] == 0x46
            && headerBytes[8] == 0x57
            && headerBytes[9] == 0x45
            && headerBytes[10] == 0x42
            && headerBytes[11] == 0x50;
    }

    /// <summary>
    /// Determines whether bytes match a zip container signature.
    /// </summary>
    /// <param name="headerBytes">Initial member bytes.</param>
    /// <returns>True when the bytes start with a zip marker.</returns>
    private static bool HasZipSignature(ReadOnlySpan<byte> headerBytes)
    {
        return headerBytes.Length >= 4
            && headerBytes[0] == 0x50
            && headerBytes[1] == 0x4B
            && (headerBytes[2] == 0x03 || headerBytes[2] == 0x05 || headerBytes[2] == 0x07)
            && (headerBytes[3] == 0x04 || headerBytes[3] == 0x06 || headerBytes[3] == 0x08);
    }
}
