using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// Reads central-directory metadata needed by the zip foundation module.
/// </summary>
internal static class ZipCentralDirectoryReader
{
    private const uint EndOfCentralDirectorySignature = 0x06054B50;
    private const uint CentralDirectoryHeaderSignature = 0x02014B50;
    private const ushort EncryptedEntryFlag = 0x0001;
    private const ushort Utf8EntryNameFlag = 0x0800;
    private const int EndOfCentralDirectoryMinimumLength = 22;
    private const int MaximumZipCommentBytes = 65_535;
    private const int CentralDirectoryFixedHeaderLength = 46;

    /// <summary>
    /// Reads encryption flags by entry name from a zip file central directory.
    /// </summary>
    /// <param name="zipFilePath">Path to the zip archive.</param>
    /// <returns>A dictionary where each key is an entry name and each value indicates encryption.</returns>
    public static IReadOnlyDictionary<string, bool> ReadEncryptionFlagsByEntryName(string zipFilePath)
    {
        using FileStream zipStream = File.OpenRead(zipFilePath);
        long centralDirectoryOffset = FindCentralDirectoryOffset(zipStream);
        return ReadCentralDirectoryEntries(zipStream, centralDirectoryOffset);
    }

    /// <summary>
    /// Finds the central-directory offset by reading the end-of-central-directory record.
    /// </summary>
    /// <param name="zipStream">Seekable zip file stream.</param>
    /// <returns>The central-directory offset.</returns>
    private static long FindCentralDirectoryOffset(FileStream zipStream)
    {
        if (zipStream.Length < EndOfCentralDirectoryMinimumLength)
        {
            throw new InvalidDataException("The zip archive is too small to contain a central directory.");
        }

        int bytesToRead = (int)Math.Min(
            zipStream.Length,
            EndOfCentralDirectoryMinimumLength + MaximumZipCommentBytes);

        byte[] tailBytes = new byte[bytesToRead];
        zipStream.Seek(zipStream.Length - bytesToRead, SeekOrigin.Begin);
        ReadExactly(zipStream, tailBytes);

        for (int offset = tailBytes.Length - EndOfCentralDirectoryMinimumLength; offset >= 0; offset--)
        {
            uint signature = BinaryPrimitives.ReadUInt32LittleEndian(tailBytes.AsSpan(offset, 4));
            if (signature != EndOfCentralDirectorySignature)
            {
                continue;
            }

            uint centralDirectoryOffset = BinaryPrimitives.ReadUInt32LittleEndian(
                tailBytes.AsSpan(offset + 16, 4));

            if (centralDirectoryOffset == uint.MaxValue)
            {
                throw new InvalidDataException("Zip64 central directory metadata is not supported yet.");
            }

            return centralDirectoryOffset;
        }

        throw new InvalidDataException("The zip archive does not contain a central directory.");
    }

    /// <summary>
    /// Reads central-directory file headers and extracts encryption flags.
    /// </summary>
    /// <param name="zipStream">Seekable zip file stream.</param>
    /// <param name="centralDirectoryOffset">Central-directory offset in the zip stream.</param>
    /// <returns>A dictionary of entry names and encryption flags.</returns>
    private static IReadOnlyDictionary<string, bool> ReadCentralDirectoryEntries(
        FileStream zipStream,
        long centralDirectoryOffset)
    {
        Dictionary<string, bool> encryptedEntriesByName = new(StringComparer.Ordinal);

        zipStream.Seek(centralDirectoryOffset, SeekOrigin.Begin);

        while (zipStream.Position + CentralDirectoryFixedHeaderLength <= zipStream.Length)
        {
            byte[] fixedHeaderBytes = new byte[CentralDirectoryFixedHeaderLength];
            ReadExactly(zipStream, fixedHeaderBytes);

            uint signature = BinaryPrimitives.ReadUInt32LittleEndian(fixedHeaderBytes.AsSpan(0, 4));
            if (signature != CentralDirectoryHeaderSignature)
            {
                break;
            }

            ushort generalPurposeFlags = BinaryPrimitives.ReadUInt16LittleEndian(
                fixedHeaderBytes.AsSpan(8, 2));
            ushort fileNameLength = BinaryPrimitives.ReadUInt16LittleEndian(
                fixedHeaderBytes.AsSpan(28, 2));
            ushort extraFieldLength = BinaryPrimitives.ReadUInt16LittleEndian(
                fixedHeaderBytes.AsSpan(30, 2));
            ushort fileCommentLength = BinaryPrimitives.ReadUInt16LittleEndian(
                fixedHeaderBytes.AsSpan(32, 2));

            byte[] fileNameBytes = new byte[fileNameLength];
            ReadExactly(zipStream, fileNameBytes);

            string entryName = DecodeEntryName(fileNameBytes, generalPurposeFlags);
            bool isEncrypted = (generalPurposeFlags & EncryptedEntryFlag) == EncryptedEntryFlag;
            encryptedEntriesByName[entryName] = isEncrypted;

            long metadataBytesToSkip = extraFieldLength + fileCommentLength;
            zipStream.Seek(metadataBytesToSkip, SeekOrigin.Current);
        }

        return encryptedEntriesByName;
    }

    /// <summary>
    /// Decodes an entry name from central-directory bytes.
    /// </summary>
    /// <param name="fileNameBytes">Raw filename bytes.</param>
    /// <param name="generalPurposeFlags">Zip general-purpose bit flags.</param>
    /// <returns>The decoded entry name.</returns>
    private static string DecodeEntryName(byte[] fileNameBytes, ushort generalPurposeFlags)
    {
        bool isUtf8 = (generalPurposeFlags & Utf8EntryNameFlag) == Utf8EntryNameFlag;
        Encoding encoding = isUtf8 ? Encoding.UTF8 : Encoding.ASCII;
        return encoding.GetString(fileNameBytes);
    }

    /// <summary>
    /// Reads exactly the requested number of bytes into a buffer.
    /// </summary>
    /// <param name="stream">Stream to read from.</param>
    /// <param name="buffer">Destination buffer.</param>
    private static void ReadExactly(Stream stream, byte[] buffer)
    {
        int totalReadBytes = 0;

        while (totalReadBytes < buffer.Length)
        {
            int readBytes = stream.Read(buffer, totalReadBytes, buffer.Length - totalReadBytes);
            if (readBytes == 0)
            {
                throw new EndOfStreamException("Unexpected end of zip metadata.");
            }

            totalReadBytes += readBytes;
        }
    }
}
