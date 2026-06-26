using System.IO.Compression;
using System.Text.Json;

namespace Prism.Api;

/// <summary>
/// Reads and validates API multipart process requests before job creation.
/// </summary>
internal static class PrismProcessIngressReader
{
    /// <summary>
    /// Reads multipart form data and builds a core-facing request or pre-core error.
    /// </summary>
    public static async Task<PrismProcessIngressResult> Read(HttpRequest httpRequest, PrismApiConfiguration configuration)
    {
        if (!httpRequest.HasFormContentType)
        {
            return PrismProcessIngressResult.FromError(CreateError(
                httpRequest,
                "INVALID_PAYLOAD",
                "POST /PRISM/process requires multipart/form-data.",
                ["Content-Type must be multipart/form-data."],
                ["request:INVALID_PAYLOAD"]));
        }

        IFormCollection form = await httpRequest.ReadFormAsync();
        string? requestJson = await ReadRequestJson(form);
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            return PrismProcessIngressResult.FromError(CreateError(
                httpRequest,
                "INVALID_PAYLOAD",
                "The multipart request part is required.",
                ["Missing multipart part named request."],
                ["request:INVALID_PAYLOAD"]));
        }

        PrismProcessRequest? processRequest = DeserializeRequest(requestJson, httpRequest, out PrismPreCoreErrorResponse? parseError);
        if (parseError is not null)
        {
            return PrismProcessIngressResult.FromError(parseError);
        }

        if (!IsSupportedFormat(processRequest!.Format))
        {
            return PrismProcessIngressResult.FromError(CreateError(
                httpRequest,
                "INVALID_PAYLOAD",
                "The requested output format is not supported.",
                [$"format={processRequest.Format}"],
                ["format:INVALID_PAYLOAD"]));
        }

        List<ImageRecord_INPUT> images = [];
        List<InputExcelFileRecord> excelFiles = [];
        List<InputZipFileRecord> zipFiles = [];
        long totalSubmittedBytes;
        List<string> fieldErrors = [];

        Guid jobID = Guid.NewGuid();
        string jobTempDir = Path.Combine(Path.GetTempPath(), "prism", jobID.ToString());

        totalSubmittedBytes = form.Files
            .Where(f => string.Equals(f.Name, "input", StringComparison.OrdinalIgnoreCase))
            .Sum(f => f.Length);

        MediaTypeSets mediaTypes = BuildMediaTypeSets(configuration);
        await AddRemoteInputRecordsAsync(processRequest.Input, mediaTypes, configuration.FetchDispatcher, jobTempDir, images, excelFiles, zipFiles, httpRequest.HttpContext.RequestAborted);
        await AddUploadedInputRecords(form.Files, configuration, mediaTypes, jobTempDir, images, excelFiles, zipFiles, fieldErrors);

        if (configuration.MaximumRequestBytes > 0 && totalSubmittedBytes > configuration.MaximumRequestBytes)
        {
            CleanUpJobTempDir(jobTempDir);
            return PrismProcessIngressResult.FromError(CreateError(
                httpRequest,
                "REQUEST_TOO_LARGE",
                "The submitted files exceed the configured request size limit.",
                [$"submittedBytes={totalSubmittedBytes}", $"maxRequestBytes={configuration.MaximumRequestBytes}"],
                ["request:REQUEST_TOO_LARGE"]));
        }

        if (fieldErrors.Count > 0)
        {
            CleanUpJobTempDir(jobTempDir);
            return PrismProcessIngressResult.FromError(CreateError(
                httpRequest,
                "INVALID_PAYLOAD",
                "One or more submitted files failed pre-core validation.",
                fieldErrors,
                fieldErrors));
        }

        (int zipImages, int zipExcel) = PeekZipContents(zipFiles, mediaTypes);
        int effectiveImages = images.Count + zipImages;
        int effectiveExcel  = excelFiles.Count + zipExcel;
        if (effectiveImages < configuration.MinimumImageCount || effectiveExcel < configuration.MinimumExcelCount)
        {
            CleanUpJobTempDir(jobTempDir);
            return PrismProcessIngressResult.FromError(CreateError(
                httpRequest,
                "INCOMPLETE_PAYLOAD",
                "At least one accepted image representation and one accepted .xlsx Excel file are required.",
                [$"acceptedImages={effectiveImages}", $"acceptedExcelFiles={effectiveExcel}"],
                ["request.Input:INCOMPLETE_PAYLOAD"]));
        }

        PrismJobRequest coreRequest = new()
        {
            JobID = jobID,
            ClientRequestToken = processRequest.ClientRequestToken,
            ImageRecords = images,
            ExcelRecords = excelFiles,
            ZipFileRecords = zipFiles,
            PrismProcessingParameters = new PrismProcessingParameters
            {
                Rename = processRequest.Rename,
                Transform = processRequest.Transform,
                Generation = processRequest.Generation,
                Format = processRequest.Format,
                ReturnOriginalImages = processRequest.ReturnOriginalImages,
                SkipClassification = processRequest.SkipClassification
            }
        };

        return PrismProcessIngressResult.FromRequest(coreRequest);
    }

    private static async Task<string?> ReadRequestJson(IFormCollection form)
    {
        if (form.TryGetValue("request", out Microsoft.Extensions.Primitives.StringValues requestValues)
            && requestValues.Count > 0)
        {
            return requestValues[0];
        }

        IFormFile? requestFile = form.Files.FirstOrDefault(file => string.Equals(file.Name, "request", StringComparison.OrdinalIgnoreCase));
        if (requestFile is null)
        {
            return null;
        }

        using StreamReader reader = new(requestFile.OpenReadStream());
        return await reader.ReadToEndAsync();
    }

    private static PrismProcessRequest? DeserializeRequest(
        string requestJson,
        HttpRequest httpRequest,
        out PrismPreCoreErrorResponse? parseError)
    {
        try
        {
            parseError = null;
            return JsonSerializer.Deserialize<PrismProcessRequest>(
                requestJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException exception)
        {
            parseError = CreateError(
                httpRequest,
                "INVALID_PAYLOAD",
                "The request JSON part is invalid.",
                [exception.Message],
                ["request:INVALID_PAYLOAD"]);
            return null;
        }
    }

    private static bool IsSupportedFormat(string format)
    {
        return string.Equals(format, "zip", StringComparison.OrdinalIgnoreCase)
            || string.Equals(format, "json", StringComparison.OrdinalIgnoreCase);
    }

    private static MediaTypeSets BuildMediaTypeSets(PrismApiConfiguration configuration)
    {
        return new MediaTypeSets(
            new HashSet<string>(configuration.ImageMediaTypes, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(configuration.ExcelMediaTypes, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(configuration.ZipMediaTypes, StringComparer.OrdinalIgnoreCase));
    }

    private static async Task AddRemoteInputRecordsAsync(
        IReadOnlyList<string> remoteInputs,
        MediaTypeSets mediaTypes,
        FetchDispatcher dispatcher,
        string jobTempFolder,
        List<ImageRecord_INPUT> images,
        List<InputExcelFileRecord> excelFiles,
        List<InputZipFileRecord> zipFiles,
        CancellationToken ct)
    {
        foreach (string input in remoteInputs.Where(i => !string.IsNullOrWhiteSpace(i)))
        {
            if (dispatcher.CanHandle(input))
            {
                ImageRecord_INPUT fetched = await dispatcher.FetchAsync(input, jobTempFolder, input, ct);
                string ext = Path.GetExtension(fetched.InitialFullName ?? string.Empty);
                if (mediaTypes.Images.Contains(ext))
                    images.Add(fetched);
                else if (mediaTypes.Excel.Contains(ext))
                    excelFiles.Add(new InputExcelFileRecord {
                        SourceReference = fetched.InitialFullName ?? input,
                        TempFilePath    = fetched.TempFilePath,
                        ByteLength      = fetched.ByteLength });
                else if (mediaTypes.Zip.Contains(ext))
                    zipFiles.Add(new InputZipFileRecord {
                        SourceReference = fetched.InitialFullName ?? input,
                        TempFilePath    = fetched.TempFilePath,
                        ByteLength      = fetched.ByteLength });
                // else: extension unrecognised after fetch — silently dropped
                continue;
            }

            // Fallback: no matching strategy — route by URL extension (existing behaviour)
            string extension = Path.GetExtension(input);
            if (mediaTypes.Images.Contains(extension))
                images.Add(new ImageRecord_INPUT { InitialFullName = input });
            else if (mediaTypes.Excel.Contains(extension))
                excelFiles.Add(new InputExcelFileRecord { SourceReference = input });
            else if (mediaTypes.Zip.Contains(extension))
                zipFiles.Add(new InputZipFileRecord { SourceReference = input });
        }
    }

    private static async Task AddUploadedInputRecords(
        IFormFileCollection files,
        PrismApiConfiguration configuration,
        MediaTypeSets mediaTypes,
        string jobTempDir,
        List<ImageRecord_INPUT> images,
        List<InputExcelFileRecord> excelFiles,
        List<InputZipFileRecord> zipFiles,
        List<string> fieldErrors)
    {
        int inputIndex = 0;
        foreach (IFormFile file in files.Where(file => string.Equals(file.Name, "input", StringComparison.OrdinalIgnoreCase)))
        {
            string extension = Path.GetExtension(file.FileName);
            string fieldPath = $"multipart.input[{inputIndex}]";

            if (mediaTypes.Images.Contains(extension))
            {
                ValidateFileLength(file, configuration.MinimumImageBytes, configuration.MaximumImageBytes, fieldPath, fieldErrors);
                string tempPath = await SpillToTempAsync(file, jobTempDir, inputIndex);
                images.Add(new ImageRecord_INPUT
                {
                    InitialFullName = file.FileName,
                    TempFilePath = tempPath
                });
            }
            else if (mediaTypes.Excel.Contains(extension))
            {
                ValidateFileLength(file, configuration.MinimumExcelBytes, configuration.MaximumExcelBytes, fieldPath, fieldErrors);
                string tempPath = await SpillToTempAsync(file, jobTempDir, inputIndex);
                excelFiles.Add(new InputExcelFileRecord
                {
                    SourceReference = file.FileName,
                    ByteLength = file.Length,
                    TempFilePath = tempPath
                });
            }
            else if (mediaTypes.Zip.Contains(extension))
            {
                ValidateFileLength(file, 0, configuration.MaximumZipBytes, fieldPath, fieldErrors);
                string tempPath = await SpillToTempAsync(file, jobTempDir, inputIndex);
                zipFiles.Add(new InputZipFileRecord
                {
                    SourceReference = file.FileName,
                    ByteLength = file.Length,
                    TempFilePath = tempPath
                });
            }

            inputIndex++;
        }
    }

    private static async Task<string> SpillToTempAsync(IFormFile file, string jobTempDir, int index)
    {
        Directory.CreateDirectory(jobTempDir);
        string safeFileName = $"{index:D4}_{Path.GetFileName(file.FileName)}";
        string tempPath = Path.Combine(jobTempDir, safeFileName);

        await using FileStream dest = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(dest);

        return tempPath;
    }

    private static (int images, int excel) PeekZipContents(List<InputZipFileRecord> zipFiles, MediaTypeSets mediaTypes)
    {
        int images = 0;
        int excel  = 0;
        foreach (InputZipFileRecord zr in zipFiles)
        {
            if (zr.TempFilePath is null || !File.Exists(zr.TempFilePath))
                continue;
            try {
                using ZipArchive archive = ZipFile.OpenRead(zr.TempFilePath);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string ext = Path.GetExtension(entry.FullName);
                    if (mediaTypes.Images.Contains(ext)) images++;
                    else if (mediaTypes.Excel.Contains(ext)) excel++;
                }
            } catch { /* corrupt / encrypted ZIP — let Import stage report the error */ }
        }
        return (images, excel);
    }

    private static void CleanUpJobTempDir(string jobTempDir)
    {
        try
        {
            if (Directory.Exists(jobTempDir))
            {
                Directory.Delete(jobTempDir, recursive: true);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void ValidateFileLength(
        IFormFile file,
        long minimumBytes,
        long maximumBytes,
        string fieldPath,
        List<string> fieldErrors)
    {
        if (minimumBytes > 0 && file.Length < minimumBytes)
        {
            fieldErrors.Add($"{fieldPath}:FILE_TOO_SMALL");
        }

        if (maximumBytes > 0 && file.Length > maximumBytes)
        {
            fieldErrors.Add($"{fieldPath}:FILE_TOO_LARGE");
        }
    }

    private static PrismPreCoreErrorResponse CreateError(
        HttpRequest request,
        string code,
        string message,
        IReadOnlyList<string> details,
        IReadOnlyList<string> fieldErrors)
    {
        return PrismPreCoreErrorResponse.Create(
            request.HttpContext.TraceIdentifier,
            code,
            message,
            details,
            fieldErrors);
    }
}

/// <summary>
/// Accepted input extensions grouped by record type, sourced from Prism_Config.json.
/// </summary>
internal sealed record MediaTypeSets(HashSet<string> Images, HashSet<string> Excel, HashSet<string> Zip);

/// <summary>
/// API request JSON shape sent in the multipart request part.
/// </summary>
internal sealed record PrismProcessRequest
{
    public string? ClientRequestToken { get; init; }
    public bool Rename { get; init; } = true;
    public bool Transform { get; init; } = true;
    public bool Generation { get; init; } = true;
    public string Format { get; init; } = "zip";
    public bool ReturnOriginalImages { get; init; }
    public bool SkipClassification { get; init; }
    public IReadOnlyList<string> Input { get; init; } = [];
}

/// <summary>
/// Result of API ingress validation and core request construction.
/// </summary>
internal sealed record PrismProcessIngressResult
{
    public PrismJobRequest? Request { get; init; }
    public PrismPreCoreErrorResponse? Error { get; init; }

    public static PrismProcessIngressResult FromRequest(PrismJobRequest request)
    {
        return new PrismProcessIngressResult { Request = request };
    }

    public static PrismProcessIngressResult FromError(PrismPreCoreErrorResponse error)
    {
        return new PrismProcessIngressResult { Error = error };
    }
}
