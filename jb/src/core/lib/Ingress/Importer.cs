using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;

namespace Prism.Lib.Ingress;

/// <summary>
/// Normalizes all accepted input records into flat JPEG image artifacts and builds the Internal
/// Excel Model from accepted Excel workbooks.
/// Reads like a recipe: <see cref="Run"/> expresses the workflow; named helpers do each step.
/// </summary>
public sealed class Importer
{
    private readonly PrismConfiguration configuration;
    private readonly ModelBuilder modelBuilder;

    // Accepted extensions, sourced from Prism_Config.json (Input.Images.extensions / Input.EXCEL.extensions).
    private readonly HashSet<string> acceptedImageExtensions;
    private readonly HashSet<string> acceptedExcelExtensions;

    // Default JPEG encoding quality for normalized output.
    private const int NormalizedJpegQuality = 92;

    // Subfolder inside the job temp root where normalized images are written.
    private const string NormalizedSubfolder = "normalized";

    // Subfolder inside the job temp root where zip members are extracted.
    private const string ZipExtractSubfolder = "zip";

    /// <summary>
    /// Creates an Importer with its required configuration and an Excel model builder.
    /// </summary>
    /// <param name="configuration">Validated PRISM configuration.</param>
    /// <param name="modelBuilder">Pre-constructed Excel model builder pointing to ExcelConfig.json.</param>
    public Importer(PrismConfiguration configuration, ModelBuilder modelBuilder)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.modelBuilder  = modelBuilder  ?? throw new ArgumentNullException(nameof(modelBuilder));

        acceptedImageExtensions = new HashSet<string>(configuration.AcceptedImageExtensions, StringComparer.OrdinalIgnoreCase);
        acceptedExcelExtensions = new HashSet<string>(configuration.AcceptedExcelExtensions, StringComparer.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Entry point
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs the Imported stage for one job.
    /// Normalizes all accepted image inputs, unpacks zip archives, and builds the IEM.
    /// Records KO items without stopping the batch.
    /// </summary>
    /// <param name="jobID">Job identifier used to build the job temp folder name.</param>
    /// <param name="imageRecords">Direct image input records from the API or caller.</param>
    /// <param name="excelRecords">Direct Excel input records from the API or caller.</param>
    /// <param name="zipRecords">Direct zip archive input records from the API or caller.</param>
    /// <param name="jobTempRoot">Root temp folder for this server; job subfolder is created inside.</param>
    /// <returns>Structured import result with normalized images, family records, and KO records.</returns>
    public ImportStageResult Run(
        Guid jobID,
        IReadOnlyList<ImageRecord_INPUT> imageRecords,
        IReadOnlyList<InputExcelFileRecord> excelRecords,
        IReadOnlyList<InputZipFileRecord> zipRecords,
        string jobTempRoot)
    {
        string jobTempFolder = PrepareJobTempFolder(jobID, jobTempRoot);

        ConcurrentBag<ImageRecord_INPUT> normalizedImages  = [];
        List<ExcelProcessingDiagnostic>  excelDiagnostics  = [];
        ConcurrentBag<ImportKoRecord>    imageKoRecords    = [];
        List<ZipMemberKoRecord>          zipKoRecords      = [];
        List<string>                     excelFilePaths    = [];

        // Shared across both image loops so normalized filenames stay unique job-wide
        // regardless of parallel completion order. Array wrapper: a `ref int` local cannot
        // be captured inside the Parallel.ForEach lambdas below, but an array reference can.
        int[] normalizedFileNameCounter = [0];

        ProcessZipRecords(zipRecords, jobTempFolder, normalizedImages, excelFilePaths, imageKoRecords, zipKoRecords, normalizedFileNameCounter);
        ProcessDirectImageRecords(imageRecords, jobTempFolder, normalizedImages, imageKoRecords, normalizedFileNameCounter);
        ProcessDirectExcelRecords(excelRecords, excelFilePaths);

        IReadOnlyList<FamilyIDRecord> familyRecords = BuildFamilyRecords(excelFilePaths, excelDiagnostics);

        // ConcurrentBag enumeration order varies per run (per-thread stacks). Sort into a stable
        // order so every downstream stage — matching aggregation, det-order tie-breaking, manifest
        // rows — sees the same sequence on every run with the same input (T-2820).
        return new ImportStageResult
        {
            NormalizedImages  = SortDeterministically(normalizedImages),
            FamilyRecords     = familyRecords,
            ExcelDiagnostics  = excelDiagnostics,
            ImageKoRecords    = imageKoRecords.OrderBy(k => k.OriginalFileName, StringComparer.Ordinal).ToList(),
            ZipKoRecords      = zipKoRecords,
            JobTempFolder     = jobTempFolder
        };
    }

    /// <summary>
    /// Orders normalized records by input-content keys only (never by the racy normalized-file
    /// counter): original filename, then byte length, then pixel size. Identical files compare
    /// equal on every key, so their relative order is irrelevant.
    /// </summary>
    private static List<ImageRecord_INPUT> SortDeterministically(ConcurrentBag<ImageRecord_INPUT> records)
    {
        return records
            .OrderBy(r => r.InitialFullName, StringComparer.Ordinal)
            .ThenBy(r => r.ByteLength ?? 0)
            .ThenBy(r => r.NormalizedWidth)
            .ThenBy(r => r.NormalizedHeight)
            .ToList();
    }

    // -------------------------------------------------------------------------
    // Zip handling
    // -------------------------------------------------------------------------

    /// <summary>
    /// Expands each zip archive using ZipHandler, then routes extracted members
    /// to the image or Excel collection.
    /// </summary>
    private void ProcessZipRecords(
        IReadOnlyList<InputZipFileRecord> zipRecords,
        string jobTempFolder,
        ConcurrentBag<ImageRecord_INPUT> normalizedImages,
        List<string> excelFilePaths,
        ConcurrentBag<ImportKoRecord> imageKoRecords,
        List<ZipMemberKoRecord> zipKoRecords,
        int[] normalizedFileNameCounter)
    {
        string zipExtractionRoot = Path.Combine(jobTempFolder, ZipExtractSubfolder);
        ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = Environment.ProcessorCount };

        foreach (InputZipFileRecord zipRecord in zipRecords)
        {
            string zipFilePath = ResolveReadablePath(zipRecord.TempFilePath, zipRecord.SourceReference);

            if (!File.Exists(zipFilePath))
            {
                continue;
            }

            ZipExtractionResult extraction = ZipHandler.ExtractProcessableMembers(
                zipFilePath,
                zipExtractionRoot,
                BuildZipPolicy());

            zipKoRecords.AddRange(extraction.KoRecords);

            // Excel routing stays sequential (excelFilePaths is a plain, non-concurrent List<T>).
            foreach (ZipExtractedMember excelMember in extraction.ExtractedMembers.Where(member => member.MediaKind == ZipMemberMediaKind.Excel))
            {
                excelFilePaths.Add(excelMember.ExtractedFilePath);
            }

            List<ZipExtractedMember> imageMembers = extraction.ExtractedMembers
                .Where(member => member.MediaKind == ZipMemberMediaKind.Image)
                .ToList();

            Parallel.ForEach(imageMembers, parallelOptions, member =>
            {
                NormalizeAndRecord(
                    member.ExtractedFilePath,
                    member.OriginalFileName,
                    ImageSourceKind.ZipMember,
                    null,
                    member.ExpandedByteLength,
                    jobTempFolder,
                    normalizedImages,
                    imageKoRecords,
                    normalizedFileNameCounter);
            });
        }
    }

    // -------------------------------------------------------------------------
    // Direct image handling
    // -------------------------------------------------------------------------

    /// <summary>
    /// Processes image records supplied directly (not from a zip archive).
    /// </summary>
    private void ProcessDirectImageRecords(
        IReadOnlyList<ImageRecord_INPUT> imageRecords,
        string jobTempFolder,
        ConcurrentBag<ImageRecord_INPUT> normalizedImages,
        ConcurrentBag<ImportKoRecord> imageKoRecords,
        int[] normalizedFileNameCounter)
    {
        ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = Environment.ProcessorCount };

        Parallel.ForEach(imageRecords, parallelOptions, record =>
        {
            // Prefer TempFilePath (API-spilled file) over InitialFullName (direct local path).
            string sourcePath = ResolveReadablePath(record.TempFilePath, record.InitialFullName);

            if (!File.Exists(sourcePath))
            {
                imageKoRecords.Add(ImportKoRecord.CorruptImage(
                    record.InitialFullName,
                    record.InitialFullName,
                    "The input file could not be found at the expected path."));
                return;
            }

            string extension = Path.GetExtension(record.InitialFullName);

            if (!acceptedImageExtensions.Contains(extension))
            {
                imageKoRecords.Add(ImportKoRecord.UnsupportedFormat(
                    record.InitialFullName,
                    record.InitialFullName));
                return;
            }

            long byteLength = record.ByteLength ?? new FileInfo(sourcePath).Length;

            if (byteLength < configuration.MinBytesPerImg)
            {
                imageKoRecords.Add(new ImportKoRecord
                {
                    OriginalFileName = record.InitialFullName,
                    SourceProvenance = record.InitialFullName,
                    ReasonCode       = ImportKoRecord.FileTooSmallReason,
                    KoGroup          = ImportKoRecord.CorruptImagesKoGroup,
                    SafeMessage      = "The input image is smaller than the configured minimum file size.",
                    BatchContinues   = true
                });
                return;
            }

            if (byteLength > configuration.MaxBytesPerImg)
            {
                imageKoRecords.Add(new ImportKoRecord
                {
                    OriginalFileName = record.InitialFullName,
                    SourceProvenance = record.InitialFullName,
                    ReasonCode       = ImportKoRecord.FileTooLargeReason,
                    KoGroup          = ImportKoRecord.OversizedKoGroup,
                    SafeMessage      = "The input image exceeds the configured maximum file size.",
                    BatchContinues   = true
                });
                return;
            }

            NormalizeAndRecord(
                sourcePath,
                record.InitialFullName,
                record.SourceKind == ImageSourceKind.Unknown ? ImageSourceKind.LocalPath : record.SourceKind,
                record.OriginalContentType,
                byteLength,
                jobTempFolder,
                normalizedImages,
                imageKoRecords,
                normalizedFileNameCounter);
        });
    }

    // -------------------------------------------------------------------------
    // Direct Excel handling
    // -------------------------------------------------------------------------

    /// <summary>
    /// Collects readable paths for directly supplied Excel records.
    /// </summary>
    private void ProcessDirectExcelRecords(
        IReadOnlyList<InputExcelFileRecord> excelRecords,
        List<string> excelFilePaths)
    {
        foreach (InputExcelFileRecord excelRecord in excelRecords)
        {
            string readablePath = ResolveReadablePath(excelRecord.TempFilePath, excelRecord.SourceReference);

            if (File.Exists(readablePath)
                && acceptedExcelExtensions.Contains(Path.GetExtension(readablePath)))
            {
                excelFilePaths.Add(readablePath);
            }
        }
    }

    // -------------------------------------------------------------------------
    // IEM construction
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds the Internal Excel Model from all collected Excel file paths and
    /// projects the result into a FamilyIDRecord list.
    /// </summary>
    private IReadOnlyList<FamilyIDRecord> BuildFamilyRecords(
        IReadOnlyList<string> excelFilePaths,
        List<ExcelProcessingDiagnostic> diagnostics)
    {
        if (excelFilePaths.Count == 0)
        {
            return [];
        }

        ExcelModelBuildResult buildResult = modelBuilder.BuildFromExcelFiles(excelFilePaths);
        diagnostics.AddRange(buildResult.Diagnostics);
        return buildResult.FamilyRecords;
    }

    // -------------------------------------------------------------------------
    // Image normalization
    // -------------------------------------------------------------------------

    /// <summary>
    /// Normalizes one image file to a flat JPEG with EXIF orientation applied.
    /// On success, appends an OK <see cref="ImageRecord_INPUT"/> to the collection.
    /// On failure, appends a KO record and does not stop the batch.
    /// </summary>
    private void NormalizeAndRecord(
        string sourcePath,
        string originalFileName,
        ImageSourceKind sourceKind,
        string? originalContentType,
        long byteLength,
        string jobTempFolder,
        ConcurrentBag<ImageRecord_INPUT> normalizedImages,
        ConcurrentBag<ImportKoRecord> imageKoRecords,
        int[] normalizedFileNameCounter)
    {
        string normalizedFolder = Path.Combine(jobTempFolder, NormalizedSubfolder);
        Directory.CreateDirectory(normalizedFolder);

        int uniqueIndex = Interlocked.Increment(ref normalizedFileNameCounter[0]) - 1;
        string normalizedFileName = BuildNormalizedFileName(originalFileName, uniqueIndex);
        string normalizedPath     = Path.Combine(normalizedFolder, normalizedFileName);

        bool normalizedSuccessfully = TryNormalizeToJpeg(
            sourcePath,
            originalFileName,
            normalizedPath,
            out int normalizedWidth,
            out int normalizedHeight,
            out byte[]? normalizedBytes,
            out ImportKoRecord? koRecord);

        if (!normalizedSuccessfully)
        {
            if (koRecord is not null)
            {
                imageKoRecords.Add(koRecord);
            }

            return;
        }

        // The salient object can never reach MinInputSizeInPixels when the whole image is smaller —
        // KO here instead of spending classify/match/order effort before Transform rejects it anyway.
        if (Math.Max(normalizedWidth, normalizedHeight) < configuration.MinInputSizeInPixels)
        {
            imageKoRecords.Add(new ImportKoRecord
            {
                OriginalFileName = originalFileName,
                SourceProvenance = sourcePath,
                ReasonCode       = ImportKoRecord.ImageTooSmallReason,
                KoGroup          = ImportKoRecord.UndersizedKoGroup,
                SafeMessage      = $"Image is {normalizedWidth}x{normalizedHeight}px; the accepted input minimum is {configuration.MinInputSizeInPixels}px on the longest side.",
                BatchContinues   = true
            });
            return;
        }

        normalizedImages.Add(new ImageRecord_INPUT
        {
            InitialFullName     = originalFileName,
            SourceKind          = sourceKind,
            OriginalContentType = originalContentType,
            ByteLength          = byteLength,
            NormalizedJpgPath   = normalizedPath,
            NormalizedJpegBytes = normalizedBytes,
            NormalizedWidth     = normalizedWidth,
            NormalizedHeight    = normalizedHeight,
            Width               = normalizedWidth,
            Height              = normalizedHeight,
            ImportStatus        = ImportStatus.Ok
        });
    }

    /// <summary>
    /// Opens an image, applies EXIF orientation, flattens transparency to white,
    /// and writes a flat JPEG to the destination path.
    /// </summary>
    /// <param name="sourcePath">Readable source file path.</param>
    /// <param name="originalFileName">Original filename for KO provenance.</param>
    /// <param name="destinationPath">Absolute path for the normalized JPEG output.</param>
    /// <param name="width">Normalized image width when successful.</param>
    /// <param name="height">Normalized image height when successful.</param>
    /// <param name="normalizedBytes">
    /// Encoded normalized JPEG bytes when the full decode/re-encode path ran (see
    /// <see cref="ImageRecord_INPUT.NormalizedJpegBytes"/>). Null on the fast path, where the source
    /// bytes are copied unchanged and no in-memory encoded image exists to hand forward.
    /// </param>
    /// <param name="koRecord">KO record when normalization fails.</param>
    /// <returns>True when normalization succeeded.</returns>
    private bool TryNormalizeToJpeg(
        string sourcePath,
        string originalFileName,
        string destinationPath,
        out int width,
        out int height,
        out byte[]? normalizedBytes,
        out ImportKoRecord? koRecord)
    {
        width           = 0;
        height          = 0;
        normalizedBytes = null;
        koRecord        = null;

        if (TryFastPathCopyConformingJpeg(sourcePath, destinationPath, out width, out height))
        {
            return true;
        }

        try
        {
            using Image sourceImage = LoadImageWithExifOrientation(sourcePath);

            width  = sourceImage.Width;
            height = sourceImage.Height;

            JpegEncoder encoder = new() { Quality = NormalizedJpegQuality };

            // Encode once to memory, then write those exact bytes to disk. Avoids a second decode of
            // NormalizedJpgPath downstream: the in-process Match stage reads these bytes directly
            // (ImageRecord_INPUT.NormalizedJpegBytes) instead of re-opening this file (T-3500).
            using MemoryStream encodedStream = new();
            sourceImage.SaveAsJpeg(encodedStream, encoder);
            normalizedBytes = encodedStream.ToArray();
            File.WriteAllBytes(destinationPath, normalizedBytes);

            return true;
        }
        catch (UnknownImageFormatException)
        {
            koRecord = ImportKoRecord.CorruptImage(
                originalFileName,
                sourcePath,
                "The image format could not be identified. The file may be corrupt or unsupported.");
            return false;
        }
        catch (InvalidImageContentException)
        {
            koRecord = ImportKoRecord.CorruptImage(
                originalFileName,
                sourcePath,
                "The image file appears to be corrupt or partially damaged.");
            return false;
        }
        catch (ImageProcessingException)
        {
            koRecord = ImportKoRecord.ConversionFailure(
                originalFileName,
                sourcePath,
                "The image could not be converted to JPEG.");
            return false;
        }
        catch (Exception)
        {
            koRecord = ImportKoRecord.CorruptImage(
                originalFileName,
                sourcePath,
                "The image could not be opened or read.");
            return false;
        }
    }

    /// <summary>
    /// Fast path: when the source is already a conforming JPEG (baseline JPEG carries no alpha
    /// channel by definition, and EXIF orientation is absent or already normal so AutoOrient
    /// would be a no-op), copies the source file into the normalized folder unchanged instead of
    /// decoding and re-encoding it. Detection reads metadata only via <see cref="Image.Identify(string)"/>
    /// — no full pixel decode. Returns false on any exception or non-conforming result; the caller
    /// falls through to the existing full decode/composite/encode path unchanged.
    /// </summary>
    /// <param name="sourcePath">Readable source file path.</param>
    /// <param name="destinationPath">Absolute path for the normalized JPEG output.</param>
    /// <param name="width">Image width when the fast path is taken.</param>
    /// <param name="height">Image height when the fast path is taken.</param>
    /// <returns>True when the source already conformed and was copied unchanged.</returns>
    private static bool TryFastPathCopyConformingJpeg(
        string sourcePath,
        string destinationPath,
        out int width,
        out int height)
    {
        width  = 0;
        height = 0;

        try
        {
            ImageInfo info = Image.Identify(sourcePath);

            if (info.Metadata.DecodedImageFormat != JpegFormat.Instance)
            {
                return false;
            }

            if (info.Metadata.ExifProfile is not null
                && info.Metadata.ExifProfile.TryGetValue(ExifTag.Orientation, out IExifValue<ushort>? orientationValue)
                && orientationValue.Value != ExifOrientationMode.TopLeft)
            {
                return false;
            }

            File.Copy(sourcePath, destinationPath, overwrite: true);
            width  = info.Size.Width;
            height = info.Size.Height;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Loads an image and applies EXIF orientation so the output is correct-side-up.
    /// Composites onto a white background before JPEG encoding so transparent pixels become
    /// #ffffff as required by the import spec.
    /// </summary>
    /// <param name="sourcePath">Readable source file path.</param>
    /// <returns>The loaded and orientation-corrected image. Caller disposes.</returns>
    private static Image LoadImageWithExifOrientation(string sourcePath)
    {
        Image image = Image.Load(sourcePath);

        // Apply EXIF orientation correction so downstream stages see the correct orientation.
        // Missing EXIF orientation renders the file as-is per spec.
        image.Mutate(context =>
        {
            // AutoOrient reads the EXIF orientation tag and rotates/flips accordingly.
            context.AutoOrient();

            // Flatten transparency onto white so JPEG encoding produces #ffffff for alpha pixels.
            // Always applied — JPEG does not support transparency and any alpha must be composited.
            context.BackgroundColor(SixLabors.ImageSharp.Color.White);
        });

        return image;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolves the readable file path, preferring the temp path when present.
    /// </summary>
    /// <param name="tempPath">Optional temp path written by the API or caller.</param>
    /// <param name="fallback">Fallback path (source reference or initial filename).</param>
    /// <returns>The best available readable path.</returns>
    private static string ResolveReadablePath(string? tempPath, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(tempPath))
        {
            return tempPath;
        }

        return fallback;
    }

    /// <summary>
    /// Prepares the job-specific temp folder inside the server temp root.
    /// </summary>
    /// <param name="jobID">Job identifier.</param>
    /// <param name="jobTempRoot">Server-level temp root.</param>
    /// <returns>Absolute path to the job temp folder.</returns>
    private static string PrepareJobTempFolder(Guid jobID, string jobTempRoot)
    {
        string jobTempFolder = Path.Combine(jobTempRoot, jobID.ToString("N"));
        Directory.CreateDirectory(jobTempFolder);
        return jobTempFolder;
    }

    /// <summary>
    /// Builds a collision-safe normalized JPEG filename from the original filename.
    /// </summary>
    /// <param name="originalFileName">Original source filename.</param>
    /// <param name="currentIndex">Current normalized-image count (used for uniqueness).</param>
    /// <returns>A safe filename with .jpg extension.</returns>
    private static string BuildNormalizedFileName(string originalFileName, int currentIndex)
    {
        string baseName = Path.GetFileNameWithoutExtension(originalFileName);
        string safeName = string.Join(
            "_",
            baseName
                .Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => segment.Trim())
                .Where(segment => segment.Length > 0));

        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "image";
        }

        return $"{currentIndex:D6}_{safeName}.jpg";
    }

    /// <summary>
    /// Builds a <see cref="ZipExtractionPolicy"/> from the validated PRISM configuration.
    /// </summary>
    private ZipExtractionPolicy BuildZipPolicy()
    {
        return new ZipExtractionPolicy(
            MaxZipArchiveBytes   : configuration.MaxZipBytes,
            MaxImageMemberBytes  : configuration.MaxBytesPerImg,
            MaxExcelMemberBytes  : configuration.MaxXLSBytes,
            MaxNestedZipDepth    : configuration.MaxNestDepthZip,
            HeaderProbeBytes     : 16);
    }
}
