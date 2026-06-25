using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Prism.Core;

/// <summary>
/// Packages the pipeline output into the requested export format (zip or JSON).
/// Zip: manifest.json at root + OK/{NewName}.jpg per non-KO image + KO/{InitialFullName}.jpg per import-OK/pipeline-KO image + first Excel file.
/// JSON: returns FinalManifest only; the API serializes it via PrismJsonResultEnvelope.
/// Reads everything it needs from a single explicit <see cref="ExportRequest"/>.
/// </summary>
internal static class Exporter
{
    /// <summary>
    /// Builds output records for all non-KO lambdas, then produces the requested export format.
    /// </summary>
    /// <param name="request">Final LAMBDA records, normalized images, and the accumulated manifest counts.</param>
    /// <returns>The canonical manifest plus ZIP bytes when ZIP format was requested.</returns>
    internal static ExportArtifacts Run(ExportRequest request)
    {
        BuildOutputRecords(request);

        return string.Equals(request.Format, "zip", StringComparison.OrdinalIgnoreCase)
            ? BuildZip(request)
            : BuildJson(request);
    }

    //  Output record construction 

    /// <summary>
    /// Attaches an <see cref="ImageRecord_OUTPUT"/> to each non-KO lambda record.
    /// Uses <see cref="ExportRequest.NormalizedImages"/> to resolve the artifact path.
    /// </summary>
    private static void BuildOutputRecords(ExportRequest request)
    {
        Dictionary<string, ImageRecord_INPUT> inputLookup = request.NormalizedImages
            .ToDictionary(r => r.InitialFullName, StringComparer.OrdinalIgnoreCase);

        foreach (ImageRecord_LAMBDA lambda in request.LambdaRecords)
        {
            if (lambda.IsKo) continue;

            if (!inputLookup.TryGetValue(lambda.InitialFullName, out ImageRecord_INPUT? input)) continue;

            string? path = input.NormalizedJpgPath;

            long byteLength = lambda.ProcessedBytes?.LongLength
                ?? (path is not null && File.Exists(path) ? new FileInfo(path).Length : 0);

            lambda.OutputRecord = new ImageRecord_OUTPUT
            {
                InitialFullName = lambda.InitialFullName,
                Family          = lambda.Family,
                DetOrder        = lambda.DetOrder,
                Width           = lambda.Width,
                Height          = lambda.Height,
                FinalFileName   = lambda.NewName,
                Extension       = ".jpg",
                MimeType        = "image/jpeg",
                ArtifactPath    = path,
                ByteLength      = byteLength,
                ExportStatus    = "Ok"
            };
        }
    }

    //  ZIP export

    /// <summary>
    /// Builds a ZIP archive containing manifest.json, OK images, KO images, and the first Excel file.
    /// </summary>
    private static ExportArtifacts BuildZip(ExportRequest request)
    {
        BatchManifest manifest = BuildManifest(request);
        IReadOnlyList<ImageJourneyItem> journeyItems = BuildJourneyItems(request);
        MemoryStream ms = new();

        using (ZipArchive zip = new(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddTextEntry(zip, "manifest.json", JsonSerializer.Serialize(manifest));

            Dictionary<string, ImageRecord_INPUT> inputLookup = request.NormalizedImages
                .ToDictionary(r => r.InitialFullName, StringComparer.OrdinalIgnoreCase);

            foreach (ImageRecord_LAMBDA lambda in request.LambdaRecords)
            {
                if (lambda.IsKo) continue;

                byte[]? imageBytes = lambda.ProcessedBytes;
                if (imageBytes is null)
                {
                    string? artifactPath = lambda.OutputRecord?.ArtifactPath;
                    if (artifactPath is null || !File.Exists(artifactPath)) continue;
                    imageBytes = File.ReadAllBytes(artifactPath);
                }

                AddBytesEntry(zip, $"OK/{lambda.NewName}", imageBytes);
            }

            foreach (ImageRecord_LAMBDA lambda in request.LambdaRecords)
            {
                if (!lambda.IsKo) continue;

                if (!inputLookup.TryGetValue(lambda.InitialFullName, out ImageRecord_INPUT? input)) continue;

                string? koPath = input.NormalizedJpgPath;
                if (koPath is null || !File.Exists(koPath)) continue;

                AddBytesEntry(zip, $"KO/{lambda.InitialFullName}", File.ReadAllBytes(koPath));
            }

            if (request.FirstExcelTempPath is not null && File.Exists(request.FirstExcelTempPath))
                AddBytesEntry(zip, Path.GetFileName(request.FirstExcelTempPath), File.ReadAllBytes(request.FirstExcelTempPath));
        }

        return new ExportArtifacts { Manifest = manifest, ZipBytes = ms.ToArray(), JourneyItems = journeyItems };
    }

    //  JSON export

    /// <summary>
    /// Builds the manifest for JSON output. ZIP bytes remain null; the API serializes the result via PrismJsonResultEnvelope.
    /// </summary>
    private static ExportArtifacts BuildJson(ExportRequest request)
    {
        return new ExportArtifacts { Manifest = BuildManifest(request), ZipBytes = null, JourneyItems = BuildJourneyItems(request) };
    }

    //  Manifest builder 

    /// <summary>
    /// Builds the canonical <see cref="BatchManifest"/> from the export request.
    /// Called once per export; the result is reused for both the response and ZIP entry.
    /// </summary>
    private static BatchManifest BuildManifest(ExportRequest request)
    {
        IReadOnlyList<ManifestImageRow> rows = request.LambdaRecords
            .Select(ToManifestRow)
            .ToList();

        return new BatchManifest
        {
            JobID     = request.JobID,
            Summary   = new BatchManifestSummary
            {
                ImageCount     = request.ImageCount,
                ExcelCount     = request.ExcelCount,
                ZipCount       = request.ZipCount,
                OkRenamed      = request.OkRenamedCount,
                KoRecords      = request.KoRecordCount,
                OkTransformed  = request.OkTransformedCount,
                KoTransformed  = 0,
                GeneratedCount = request.GeneratedCount
            },
            ImageRows      = rows,
            RouteSummaries = Pipeline.StageOrder.Select(stage => $"{stage}: completed.").ToArray(),
            Warnings       = request.Warnings
        };
    }

    /// <summary>
    /// Projects one <see cref="ImageRecord_LAMBDA"/> into a <see cref="ManifestImageRow"/>.
    /// </summary>
    private static ManifestImageRow ToManifestRow(ImageRecord_LAMBDA lambda)
    {
        return new ManifestImageRow
        {
            SourceReference      = lambda.InitialFullName,
            FinalFileName        = lambda.IsKo ? null : lambda.NewName,
            Status               = lambda.IsKo ? "Ko" : "Ok",
            KoReasonCode         = lambda.KoReasonCode,
            KoSafeMessage        = lambda.KoSafeMessage,
            FamilyId             = string.IsNullOrEmpty(lambda.Family) ? null : lambda.Family,
            DetOrder             = lambda.IsKo ? null : lambda.DetOrder,
            TransformerType      = lambda.TransformationResult?.TransformerType,
            TransformationStatus = lambda.TransformationResult?.Status.ToString()
        };
    }

    //  Journey items builder

    /// <summary>
    /// Projects all LAMBDA records into the bounded per-image journey items for the JSON result envelope.
    /// </summary>
    private static IReadOnlyList<ImageJourneyItem> BuildJourneyItems(ExportRequest request)
    {
        return request.LambdaRecords
            .Select(ToImageJourneyItem)
            .ToList();
    }

    /// <summary>
    /// Projects one <see cref="ImageRecord_LAMBDA"/> into an <see cref="ImageJourneyItem"/>.
    /// Stages are emitted in pipeline order; each carries its name, status, and optional safe message.
    /// </summary>
    private static ImageJourneyItem ToImageJourneyItem(ImageRecord_LAMBDA lambda)
    {
        return new ImageJourneyItem
        {
            SourceReference = lambda.InitialFullName,
            Lambda          = new ImageLambdaJourney { Stages = BuildSteps(lambda) },
            Output          = lambda.IsKo ? null : lambda.OutputRecord,
            KoReasonCode    = lambda.IsKo ? lambda.KoReasonCode : null
        };
    }

    private static IReadOnlyList<ImageStageStep> BuildSteps(ImageRecord_LAMBDA lambda)
    {
        return
        [
            BuildImportStep(),
            BuildClassifyStep(),
            BuildMatchStep(lambda),
            BuildTransformStep(lambda)
        ];
    }

    private static ImageStageStep BuildImportStep()
    {
        return new ImageStageStep { StageName = PipelineStageNames.Imported, Status = "Ok" };
    }

    private static ImageStageStep BuildClassifyStep()
    {
        return new ImageStageStep { StageName = PipelineStageNames.Classified, Status = "Ok" };
    }

    private static ImageStageStep BuildMatchStep(ImageRecord_LAMBDA lambda)
    {
        bool koAtMatch = lambda.IsKo && lambda.KoReasonCode?.StartsWith("MATCH", StringComparison.Ordinal) == true;
        string status  = lambda.MatchEvidence is null ? "Skipped" : (koAtMatch ? "Ko" : "Ok");

        return new ImageStageStep
        {
            StageName   = PipelineStageNames.Matched,
            Status      = status,
            SafeMessage = koAtMatch ? lambda.KoSafeMessage : null
        };
    }

    private static ImageStageStep BuildTransformStep(ImageRecord_LAMBDA lambda)
    {
        string status      = lambda.TransformationResult?.Status.ToString() ?? (lambda.IsKo ? "Skipped" : "Ok");
        bool koAtTransform = status == "Ko";

        return new ImageStageStep
        {
            StageName   = PipelineStageNames.Transformed,
            Status      = status,
            SafeMessage = koAtTransform ? lambda.KoSafeMessage : null
        };
    }

    //  ZIP helpers

    private static void AddTextEntry(ZipArchive zip, string entryName, string content)
    {
        ZipArchiveEntry entry = zip.CreateEntry(entryName);
        using StreamWriter writer = new(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private static void AddBytesEntry(ZipArchive zip, string entryName, byte[] bytes)
    {
        ZipArchiveEntry entry = zip.CreateEntry(entryName);
        using Stream stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }
}
