using System.Text.Json;

/// <summary>
/// Reads and validates API multipart process requests before job creation.
/// </summary>
internal static class PrismProcessIngressReader
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".tif",
        ".tiff",
        ".pdf",
        ".webp",
        ".bmp",
        ".gif"
    };

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
        long totalSubmittedBytes = 0;
        List<string> fieldErrors = [];

        AddRemoteInputRecords(processRequest.Input, images, excelFiles, zipFiles);
        AddUploadedInputRecords(form.Files, configuration, images, excelFiles, zipFiles, ref totalSubmittedBytes, fieldErrors);

        if (configuration.MaximumRequestBytes > 0 && totalSubmittedBytes > configuration.MaximumRequestBytes)
        {
            return PrismProcessIngressResult.FromError(CreateError(
                httpRequest,
                "REQUEST_TOO_LARGE",
                "The submitted files exceed the configured request size limit.",
                [$"submittedBytes={totalSubmittedBytes}", $"maxRequestBytes={configuration.MaximumRequestBytes}"],
                ["request:REQUEST_TOO_LARGE"]));
        }

        if (fieldErrors.Count > 0)
        {
            return PrismProcessIngressResult.FromError(CreateError(
                httpRequest,
                "INVALID_PAYLOAD",
                "One or more submitted files failed pre-core validation.",
                fieldErrors,
                fieldErrors));
        }

        if (images.Count < configuration.MinimumImageCount || excelFiles.Count < configuration.MinimumExcelCount)
        {
            return PrismProcessIngressResult.FromError(CreateError(
                httpRequest,
                "INCOMPLETE_PAYLOAD",
                "At least one accepted image representation and one accepted .xlsx Excel file are required.",
                [$"acceptedImages={images.Count}", $"acceptedExcelFiles={excelFiles.Count}"],
                ["request.Input:INCOMPLETE_PAYLOAD"]));
        }

        Guid jobID = Guid.NewGuid();
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
                ReturnOriginalImages = processRequest.ReturnOriginalImages
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

    private static void AddRemoteInputRecords(
        IReadOnlyList<string> remoteInputs,
        List<ImageRecord_INPUT> images,
        List<InputExcelFileRecord> excelFiles,
        List<InputZipFileRecord> zipFiles)
    {
        foreach (string remoteInput in remoteInputs.Where(input => !string.IsNullOrWhiteSpace(input)))
        {
            string extension = Path.GetExtension(remoteInput);
            if (ImageExtensions.Contains(extension))
            {
                images.Add(new ImageRecord_INPUT { InitialFullName = remoteInput });
                continue;
            }

            if (string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                excelFiles.Add(new InputExcelFileRecord { SourceReference = remoteInput });
                continue;
            }

            if (string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
            {
                zipFiles.Add(new InputZipFileRecord { SourceReference = remoteInput });
            }
        }
    }

    private static void AddUploadedInputRecords(
        IFormFileCollection files,
        PrismApiConfiguration configuration,
        List<ImageRecord_INPUT> images,
        List<InputExcelFileRecord> excelFiles,
        List<InputZipFileRecord> zipFiles,
        ref long totalSubmittedBytes,
        List<string> fieldErrors)
    {
        int inputIndex = 0;
        foreach (IFormFile file in files.Where(file => string.Equals(file.Name, "input", StringComparison.OrdinalIgnoreCase)))
        {
            totalSubmittedBytes += file.Length;
            string extension = Path.GetExtension(file.FileName);
            string fieldPath = $"multipart.input[{inputIndex}]";

            if (ImageExtensions.Contains(extension))
            {
                ValidateFileLength(file, configuration.MinimumImageBytes, configuration.MaximumImageBytes, fieldPath, fieldErrors);
                images.Add(new ImageRecord_INPUT
                {
                    InitialFullName = file.FileName
                });
            }
            else if (string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ValidateFileLength(file, configuration.MinimumExcelBytes, configuration.MaximumExcelBytes, fieldPath, fieldErrors);
                excelFiles.Add(new InputExcelFileRecord
                {
                    SourceReference = file.FileName,
                    ByteLength = file.Length
                });
            }
            else if (string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
            {
                ValidateFileLength(file, 0, configuration.MaximumZipBytes, fieldPath, fieldErrors);
                zipFiles.Add(new InputZipFileRecord
                {
                    SourceReference = file.FileName,
                    ByteLength = file.Length
                });
            }

            inputIndex++;
        }
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
