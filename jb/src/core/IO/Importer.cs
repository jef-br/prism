using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Prism.Core;

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

        List<ImageRecord_INPUT>     normalizedImages  = [];
        List<ExcelProcessingDiagnostic> excelDiagnostics = [];
        List<ImportKoRecord>        imageKoRecords    = [];
        List<ZipMemberKoRecord>     zipKoRecords      = [];
        List<string>                excelFilePaths    = [];

        ProcessZipRecords(zipRecords, jobTempFolder, normalizedImages, excelFilePaths, imageKoRecords, zipKoRecords);
        ProcessDirectImageRecords(imageRecords, jobTempFolder, normalizedImages, imageKoRecords);
        ProcessDirectExcelRecords(excelRecords, excelFilePaths);

        IReadOnlyList<FamilyRecord> familyRecords = BuildFamilyRecords(excelFilePaths, excelDiagnostics);

        return new ImportStageResult
        {
            NormalizedImages  = normalizedImages,
            FamilyRecords     = familyRecords,
            ExcelDiagnostics  = excelDiagnostics,
            ImageKoRecords    = imageKoRecords,
            ZipKoRecords      = zipKoRecords,
            JobTempFolder     = jobTempFolder
        };
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
        List<ImageRecord_INPUT> normalizedImages,
        List<string> excelFilePaths,
        List<ImportKoRecord> imageKoRecords,
        List<ZipMemberKoRecord> zipKoRecords)
    {
        string zipExtractionRoot = Path.Combine(jobTempFolder, ZipExtractSubfolder);

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

            foreach (ZipExtractedMember member in extraction.ExtractedMembers)
            {
                if (member.MediaKind == ZipMemberMediaKind.Excel)
                {
                    excelFilePaths.Add(member.ExtractedFilePath);
                    continue;
                }

                if (member.MediaKind == ZipMemberMediaKind.Image)
                {
                    NormalizeAndRecord(
                        member.ExtractedFilePath,
                        member.OriginalFileName,
                        ImageSourceKind.ZipMember,
                        null,
                        member.ExpandedByteLength,
                        jobTempFolder,
                        normalizedImages,
                        imageKoRecords);
                }
            }
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
        List<ImageRecord_INPUT> normalizedImages,
        List<ImportKoRecord> imageKoRecords)
    {
        foreach (ImageRecord_INPUT record in imageRecords)
        {
            // Prefer TempFilePath (API-spilled file) over InitialFullName (direct local path).
            string sourcePath = ResolveReadablePath(record.TempFilePath, record.InitialFullName);

            if (!File.Exists(sourcePath))
            {
                imageKoRecords.Add(ImportKoRecord.CorruptImage(
                    record.InitialFullName,
                    record.InitialFullName,
                    "The input file could not be found at the expected path."));
                continue;
            }

            string extension = Path.GetExtension(record.InitialFullName);

            if (!acceptedImageExtensions.Contains(extension))
            {
                imageKoRecords.Add(ImportKoRecord.UnsupportedFormat(
                    record.InitialFullName,
                    record.InitialFullName));
                continue;
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
                continue;
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
                continue;
            }

            NormalizeAndRecord(
                sourcePath,
                record.InitialFullName,
                record.SourceKind == ImageSourceKind.Unknown ? ImageSourceKind.LocalPath : record.SourceKind,
                record.OriginalContentType,
                byteLength,
                jobTempFolder,
                normalizedImages,
                imageKoRecords);
        }
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
    /// projects the result into a FamilyRecord list.
    /// </summary>
    private IReadOnlyList<FamilyRecord> BuildFamilyRecords(
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
        List<ImageRecord_INPUT> normalizedImages,
        List<ImportKoRecord> imageKoRecords)
    {
        string normalizedFolder = Path.Combine(jobTempFolder, NormalizedSubfolder);
        Directory.CreateDirectory(normalizedFolder);

        string normalizedFileName = BuildNormalizedFileName(originalFileName, normalizedImages.Count);
        string normalizedPath     = Path.Combine(normalizedFolder, normalizedFileName);

        bool normalizedSuccessfully = TryNormalizeToJpeg(
            sourcePath,
            originalFileName,
            normalizedPath,
            out int normalizedWidth,
            out int normalizedHeight,
            out ImportKoRecord? koRecord);

        if (!normalizedSuccessfully)
        {
            if (koRecord is not null)
            {
                imageKoRecords.Add(koRecord);
            }

            return;
        }

        normalizedImages.Add(new ImageRecord_INPUT
        {
            InitialFullName     = originalFileName,
            SourceKind          = sourceKind,
            OriginalContentType = originalContentType,
            ByteLength          = byteLength,
            NormalizedJpgPath   = normalizedPath,
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
    /// <param name="koRecord">KO record when normalization fails.</param>
    /// <returns>True when normalization succeeded.</returns>
    private bool TryNormalizeToJpeg(
        string sourcePath,
        string originalFileName,
        string destinationPath,
        out int width,
        out int height,
        out ImportKoRecord? koRecord)
    {
        width    = 0;
        height   = 0;
        koRecord = null;

        try
        {
            using Image sourceImage = LoadImageWithExifOrientation(sourcePath);

            width  = sourceImage.Width;
            height = sourceImage.Height;

            JpegEncoder encoder = new() { Quality = NormalizedJpegQuality };
            sourceImage.SaveAsJpeg(destinationPath, encoder);

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
