using System;
using System.IO;

namespace Prism.Lib.Zip;

/// <summary>
/// Classifies zip members by content signature and accepted PRISM filename extensions.
/// </summary>
internal static class ZipMemberTriage
{
    // Each signature byte is named individually (not just the array) because S109 flags every
    // numeric literal, including ones inside an array initializer — only a literal assigned
    // directly to a const field is exempt.
    private const byte JpegByte0 = 0xFF;
    private const byte JpegByte1 = 0xD8;
    private const byte JpegByte2 = 0xFF;
    private static ReadOnlySpan<byte> JpegSignature => new byte[] { JpegByte0, JpegByte1, JpegByte2 };

    private const byte PngByte0 = 0x89;
    private const byte PngByte1 = 0x50;
    private const byte PngByte2 = 0x4E;
    private const byte PngByte3 = 0x47;
    private const byte PngByte4 = 0x0D;
    private const byte PngByte5 = 0x0A;
    private const byte PngByte6 = 0x1A;
    private const byte PngByte7 = 0x0A;
    private static ReadOnlySpan<byte> PngSignature => new byte[] { PngByte0, PngByte1, PngByte2, PngByte3, PngByte4, PngByte5, PngByte6, PngByte7 };

    private const byte GifByte0 = 0x47;
    private const byte GifByte1 = 0x49;
    private const byte GifByte2 = 0x46;
    private const byte GifByte3 = 0x38;
    private static ReadOnlySpan<byte> GifSignaturePrefix => new byte[] { GifByte0, GifByte1, GifByte2, GifByte3 };
    private const int GifVersionByteIndex = 4;
    private const int GifTerminatorByteIndex = 5;
    private const int GifSignatureLength = 6;
    private const byte Gif87aVersionByte = 0x37;
    private const byte Gif89aVersionByte = 0x39;
    private const byte GifSignatureTerminator = 0x61;

    private const byte BmpByte0 = 0x42;
    private const byte BmpByte1 = 0x4D;
    private static ReadOnlySpan<byte> BmpSignature => new byte[] { BmpByte0, BmpByte1 };

    private const byte TiffLeByte0 = 0x49;
    private const byte TiffLeByte1 = 0x49;
    private const byte TiffLeByte2 = 0x2A;
    private const byte TiffLeByte3 = 0x00;
    private static ReadOnlySpan<byte> TiffLittleEndianSignature => new byte[] { TiffLeByte0, TiffLeByte1, TiffLeByte2, TiffLeByte3 };
    private const byte TiffBeByte0 = 0x4D;
    private const byte TiffBeByte1 = 0x4D;
    private const byte TiffBeByte2 = 0x00;
    private const byte TiffBeByte3 = 0x2A;
    private static ReadOnlySpan<byte> TiffBigEndianSignature => new byte[] { TiffBeByte0, TiffBeByte1, TiffBeByte2, TiffBeByte3 };

    private const byte PdfByte0 = 0x25;
    private const byte PdfByte1 = 0x50;
    private const byte PdfByte2 = 0x44;
    private const byte PdfByte3 = 0x46;
    private static ReadOnlySpan<byte> PdfSignature => new byte[] { PdfByte0, PdfByte1, PdfByte2, PdfByte3 };

    private const byte WebpRiffByte0 = 0x52;
    private const byte WebpRiffByte1 = 0x49;
    private const byte WebpRiffByte2 = 0x46;
    private const byte WebpRiffByte3 = 0x46;
    private static ReadOnlySpan<byte> WebpRiffPrefix => new byte[] { WebpRiffByte0, WebpRiffByte1, WebpRiffByte2, WebpRiffByte3 };
    private const byte WebpFormatByte0 = 0x57;
    private const byte WebpFormatByte1 = 0x45;
    private const byte WebpFormatByte2 = 0x42;
    private const byte WebpFormatByte3 = 0x50;
    private static ReadOnlySpan<byte> WebpFormatMarker => new byte[] { WebpFormatByte0, WebpFormatByte1, WebpFormatByte2, WebpFormatByte3 };
    private const int WebpFormatMarkerOffset = 8;
    private const int WebpHeaderLength = 12;

    private const byte ZipMarkerByte0 = 0x50;
    private const byte ZipMarkerByte1 = 0x4B;
    private const int ZipSignatureLength = 4;
    private const int ZipRecordMarkerIndex = 2;
    private const int ZipRecordVersionIndex = 3;
    private const byte ZipLocalFileRecordMarker = 0x03;
    private const byte ZipCentralDirEndMarker = 0x05;
    private const byte ZipSpannedArchiveMarker = 0x07;
    private const byte ZipLocalFileRecordVersion = 0x04;
    private const byte ZipCentralDirEndVersion = 0x06;
    private const byte ZipSpannedArchiveVersion = 0x08;

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
        return headerBytes.Length >= JpegSignature.Length
            && headerBytes[..JpegSignature.Length].SequenceEqual(JpegSignature);
    }

    /// <summary>
    /// Determines whether bytes match a PNG signature.
    /// </summary>
    /// <param name="headerBytes">Initial member bytes.</param>
    /// <returns>True when the bytes start with a PNG marker.</returns>
    private static bool HasPngSignature(ReadOnlySpan<byte> headerBytes)
    {
        return headerBytes.Length >= PngSignature.Length
            && headerBytes[..PngSignature.Length].SequenceEqual(PngSignature);
    }

    /// <summary>
    /// Determines whether bytes match a GIF signature.
    /// </summary>
    /// <param name="headerBytes">Initial member bytes.</param>
    /// <returns>True when the bytes start with a GIF marker.</returns>
    private static bool HasGifSignature(ReadOnlySpan<byte> headerBytes)
    {
        return headerBytes.Length >= GifSignatureLength
            && headerBytes[..GifSignaturePrefix.Length].SequenceEqual(GifSignaturePrefix)
            && (headerBytes[GifVersionByteIndex] == Gif87aVersionByte || headerBytes[GifVersionByteIndex] == Gif89aVersionByte)
            && headerBytes[GifTerminatorByteIndex] == GifSignatureTerminator;
    }

    /// <summary>
    /// Determines whether bytes match a BMP signature.
    /// </summary>
    /// <param name="headerBytes">Initial member bytes.</param>
    /// <returns>True when the bytes start with a BMP marker.</returns>
    private static bool HasBmpSignature(ReadOnlySpan<byte> headerBytes)
    {
        return headerBytes.Length >= BmpSignature.Length
            && headerBytes[..BmpSignature.Length].SequenceEqual(BmpSignature);
    }

    /// <summary>
    /// Determines whether bytes match a TIFF signature.
    /// </summary>
    /// <param name="headerBytes">Initial member bytes.</param>
    /// <returns>True when the bytes start with a TIFF marker.</returns>
    private static bool HasTiffSignature(ReadOnlySpan<byte> headerBytes)
    {
        bool littleEndianTiff = headerBytes.Length >= TiffLittleEndianSignature.Length
            && headerBytes[..TiffLittleEndianSignature.Length].SequenceEqual(TiffLittleEndianSignature);

        bool bigEndianTiff = headerBytes.Length >= TiffBigEndianSignature.Length
            && headerBytes[..TiffBigEndianSignature.Length].SequenceEqual(TiffBigEndianSignature);

        return littleEndianTiff || bigEndianTiff;
    }

    /// <summary>
    /// Determines whether bytes match a PDF signature.
    /// </summary>
    /// <param name="headerBytes">Initial member bytes.</param>
    /// <returns>True when the bytes start with a PDF marker.</returns>
    private static bool HasPdfSignature(ReadOnlySpan<byte> headerBytes)
    {
        return headerBytes.Length >= PdfSignature.Length
            && headerBytes[..PdfSignature.Length].SequenceEqual(PdfSignature);
    }

    /// <summary>
    /// Determines whether bytes match a WebP signature.
    /// </summary>
    /// <param name="headerBytes">Initial member bytes.</param>
    /// <returns>True when the bytes start with a WebP RIFF marker.</returns>
    private static bool HasWebpSignature(ReadOnlySpan<byte> headerBytes)
    {
        return headerBytes.Length >= WebpHeaderLength
            && headerBytes[..WebpRiffPrefix.Length].SequenceEqual(WebpRiffPrefix)
            && headerBytes[WebpFormatMarkerOffset..WebpHeaderLength].SequenceEqual(WebpFormatMarker);
    }

    /// <summary>
    /// Determines whether bytes match a zip container signature.
    /// </summary>
    /// <param name="headerBytes">Initial member bytes.</param>
    /// <returns>True when the bytes start with a zip marker.</returns>
    private static bool HasZipSignature(ReadOnlySpan<byte> headerBytes)
    {
        return headerBytes.Length >= ZipSignatureLength
            && headerBytes[0] == ZipMarkerByte0
            && headerBytes[1] == ZipMarkerByte1
            && (headerBytes[ZipRecordMarkerIndex] == ZipLocalFileRecordMarker || headerBytes[ZipRecordMarkerIndex] == ZipCentralDirEndMarker || headerBytes[ZipRecordMarkerIndex] == ZipSpannedArchiveMarker)
            && (headerBytes[ZipRecordVersionIndex] == ZipLocalFileRecordVersion || headerBytes[ZipRecordVersionIndex] == ZipCentralDirEndVersion || headerBytes[ZipRecordVersionIndex] == ZipSpannedArchiveVersion);
    }
}
