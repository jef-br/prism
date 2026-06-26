namespace Prism.Api;

/// <summary>
/// Reads multipart form data for the <c>POST /PRISM/match/lite</c> route.
/// Images: filename extracted from Content-Disposition only — bytes are not spilled to disk.
/// Excel: spilled to a job-scoped temp folder for parsing, then cleaned up by the caller.
/// </summary>
internal static class PrismMatchLiteIngressReader
{
    /// <summary>
    /// Parses the multipart request and returns filename-only image inputs and temp-spilled Excel inputs,
    /// or a pre-core error response when the payload is invalid.
    /// </summary>
    public static async Task<PrismMatchLiteIngressResult> Read(HttpRequest httpRequest, PrismApiConfiguration configuration)
    {
        if (!httpRequest.HasFormContentType)
        {
            return PrismMatchLiteIngressResult.FromError(CreateError(
                httpRequest, "INVALID_PAYLOAD",
                "POST /PRISM/match/lite requires multipart/form-data.",
                ["Content-Type must be multipart/form-data."],
                ["request:INVALID_PAYLOAD"]));
        }

        IFormCollection form = await httpRequest.ReadFormAsync();

        List<ImageRecord_INPUT> images = [];
        List<InputExcelFileRecord> excelFiles = [];
        List<string> fieldErrors = [];

        string jobTempDir = Path.Combine(Path.GetTempPath(), "prism", Guid.NewGuid().ToString());
        MediaTypeSets mediaTypes = new(
            new HashSet<string>(configuration.ImageMediaTypes, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(configuration.ExcelMediaTypes, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(configuration.ZipMediaTypes, StringComparer.OrdinalIgnoreCase));

        int inputIndex = 0;
        foreach (IFormFile file in form.Files.Where(f => string.Equals(f.Name, "input", StringComparison.OrdinalIgnoreCase)))
        {
            string extension = Path.GetExtension(file.FileName);
            string fieldPath = $"multipart.input[{inputIndex}]";

            if (mediaTypes.Images.Contains(extension))
            {
                // Lite path: filename only — no body read, no disk write.
                images.Add(new ImageRecord_INPUT { InitialFullName = file.FileName });
            }
            else if (mediaTypes.Excel.Contains(extension))
            {
                if (configuration.MaximumExcelBytes > 0 && file.Length > configuration.MaximumExcelBytes)
                    fieldErrors.Add($"{fieldPath}:FILE_TOO_LARGE");
                else
                {
                    string tempPath = await SpillToTempAsync(file, jobTempDir, inputIndex);
                    excelFiles.Add(new InputExcelFileRecord { SourceReference = file.FileName, ByteLength = file.Length, TempFilePath = tempPath });
                }
            }

            inputIndex++;
        }

        if (fieldErrors.Count > 0)
        {
            CleanUpTempDir(jobTempDir);
            return PrismMatchLiteIngressResult.FromError(CreateError(
                httpRequest, "INVALID_PAYLOAD",
                "One or more submitted files failed pre-core validation.",
                fieldErrors, fieldErrors));
        }

        if (images.Count == 0 || excelFiles.Count == 0)
        {
            CleanUpTempDir(jobTempDir);
            return PrismMatchLiteIngressResult.FromError(CreateError(
                httpRequest, "INCOMPLETE_PAYLOAD",
                "At least one accepted image and one accepted .xlsx Excel file are required.",
                [$"acceptedImages={images.Count}", $"acceptedExcelFiles={excelFiles.Count}"],
                ["request.Input:INCOMPLETE_PAYLOAD"]));
        }

        return PrismMatchLiteIngressResult.FromData(images, excelFiles, jobTempDir);
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

    private static void CleanUpTempDir(string jobTempDir)
    {
        try { if (Directory.Exists(jobTempDir)) Directory.Delete(jobTempDir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static PrismPreCoreErrorResponse CreateError(
        HttpRequest request, string code, string message,
        IReadOnlyList<string> details, IReadOnlyList<string> fieldErrors)
        => PrismPreCoreErrorResponse.Create(request.HttpContext.TraceIdentifier, code, message, details, fieldErrors);
}

/// <summary>Result of lite ingress validation.</summary>
internal sealed record PrismMatchLiteIngressResult
{
    public IReadOnlyList<ImageRecord_INPUT>? Images { get; init; }
    public IReadOnlyList<InputExcelFileRecord>? ExcelFiles { get; init; }
    public string? JobTempDir { get; init; }
    public PrismPreCoreErrorResponse? Error { get; init; }

    public static PrismMatchLiteIngressResult FromData(
        IReadOnlyList<ImageRecord_INPUT> images,
        IReadOnlyList<InputExcelFileRecord> excelFiles,
        string jobTempDir)
        => new() { Images = images, ExcelFiles = excelFiles, JobTempDir = jobTempDir };

    public static PrismMatchLiteIngressResult FromError(PrismPreCoreErrorResponse error)
        => new() { Error = error };

    /// <summary>Deletes the temp folder that holds spilled Excel files.</summary>
    public void CleanUp()
    {
        if (JobTempDir is null) return;
        try { if (Directory.Exists(JobTempDir)) Directory.Delete(JobTempDir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
