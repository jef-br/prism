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

    // ─── Output record construction ───────────────────────────────────────────

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
                ByteLength      = path is not null && File.Exists(path) ? new FileInfo(path).Length : 0,
                ExportStatus    = "Ok"
            };
        }
    }

    // ─── ZIP export ───────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a ZIP archive containing manifest.json, OK images, KO images, and the first Excel file.
    /// </summary>
    private static ExportArtifacts BuildZip(ExportRequest request)
    {
        BatchManifest manifest = BuildManifest(request);
        MemoryStream ms = new();

        using (ZipArchive zip = new(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddTextEntry(zip, "manifest.json", JsonSerializer.Serialize(manifest));

            Dictionary<string, ImageRecord_INPUT> inputLookup = request.NormalizedImages
                .ToDictionary(r => r.InitialFullName, StringComparer.OrdinalIgnoreCase);

            foreach (ImageRecord_LAMBDA lambda in request.LambdaRecords)
            {
                if (lambda.IsKo) continue;

                string? artifactPath = lambda.OutputRecord?.ArtifactPath;
                if (artifactPath is null || !File.Exists(artifactPath)) continue;

                AddBytesEntry(zip, $"OK/{lambda.NewName}", File.ReadAllBytes(artifactPath));
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

        return new ExportArtifacts { ZipBytes = ms.ToArray(), Manifest = manifest };
    }

    // ─── JSON export ──────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the manifest for JSON output. ZIP bytes remain null; the API serializes the result via PrismJsonResultEnvelope.
    /// </summary>
    private static ExportArtifacts BuildJson(ExportRequest request)
    {
        return new ExportArtifacts { ZipBytes = null, Manifest = BuildManifest(request) };
    }

    // ─── Manifest builder ─────────────────────────────────────────────────────

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

    // ─── ZIP helpers ──────────────────────────────────────────────────────────

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
