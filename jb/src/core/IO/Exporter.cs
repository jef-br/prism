using System.IO.Compression;
using System.Text;
using System.Text.Json;

/// <summary>
/// Packages the pipeline output into the requested export format (zip or JSON).
/// Zip: manifest.json at root + OK/{NewName}.jpg per non-KO image + KO/{InitialFullName}.jpg per import-OK/pipeline-KO image + first Excel file.
/// JSON: sets FinalManifest on context; API serializes it via PrismJsonResultEnvelope.
/// </summary>
internal static class Exporter
{
    /// <summary>
    /// Builds output records for all non-KO lambdas, then produces the requested export format.
    /// Sets <see cref="PipelineContext.ExportResult"/> before returning.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        BuildOutputRecords(context);

        string format = context.Parameters.Format ?? "json";

        context.ExportResult = string.Equals(format, "zip", StringComparison.OrdinalIgnoreCase)
            ? BuildZip(context)
            : BuildJson(context);
    }

    // ─── Output record construction ───────────────────────────────────────────

    /// <summary>
    /// Attaches an <see cref="ImageRecord_OUTPUT"/> to each non-KO lambda record.
    /// Uses <see cref="PipelineContext.NormalizedImages"/> to resolve the artifact path.
    /// </summary>
    private static void BuildOutputRecords(PipelineContext context)
    {
        Dictionary<string, ImageRecord_INPUT> inputLookup = context.NormalizedImages
            .ToDictionary(r => r.InitialFullName, StringComparer.OrdinalIgnoreCase);

        foreach (ImageRecord_LAMBDA lambda in context.LambdaRecords)
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
    private static ExportStageResult BuildZip(PipelineContext context)
    {
        BatchManifest manifest = BuildManifest(context);
        MemoryStream ms = new();

        using (ZipArchive zip = new(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddTextEntry(zip, "manifest.json", JsonSerializer.Serialize(manifest));

            Dictionary<string, ImageRecord_INPUT> inputLookup = context.NormalizedImages
                .ToDictionary(r => r.InitialFullName, StringComparer.OrdinalIgnoreCase);

            foreach (ImageRecord_LAMBDA lambda in context.LambdaRecords)
            {
                if (lambda.IsKo) continue;

                string? artifactPath = lambda.OutputRecord?.ArtifactPath;
                if (artifactPath is null || !File.Exists(artifactPath)) continue;

                AddBytesEntry(zip, $"OK/{lambda.NewName}", File.ReadAllBytes(artifactPath));
            }

            foreach (ImageRecord_LAMBDA lambda in context.LambdaRecords)
            {
                if (!lambda.IsKo) continue;

                if (!inputLookup.TryGetValue(lambda.InitialFullName, out ImageRecord_INPUT? input)) continue;

                string? koPath = input.NormalizedJpgPath;
                if (koPath is null || !File.Exists(koPath)) continue;

                AddBytesEntry(zip, $"KO/{lambda.InitialFullName}", File.ReadAllBytes(koPath));
            }

            if (context.ExcelRecords.Count > 0)
            {
                string? excelPath = context.ExcelRecords[0].TempFilePath;
                if (excelPath is not null && File.Exists(excelPath))
                    AddBytesEntry(zip, Path.GetFileName(excelPath), File.ReadAllBytes(excelPath));
            }
        }

        return new ExportStageResult { ZipBytes = ms.ToArray(), FinalManifest = manifest };
    }

    // ─── JSON export ──────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the manifest for JSON output. ZIP bytes remain null; the API serializes the result via PrismJsonResultEnvelope.
    /// </summary>
    private static ExportStageResult BuildJson(PipelineContext context)
    {
        return new ExportStageResult { ZipBytes = null, FinalManifest = BuildManifest(context) };
    }

    // ─── Manifest builder ─────────────────────────────────────────────────────

    /// <summary>
    /// Builds the canonical <see cref="BatchManifest"/> from the current pipeline context.
    /// Called once per export; the result is stored on <see cref="ExportStageResult.FinalManifest"/>
    /// so Pipeline.BuildSuccessResult can reuse it without rebuilding.
    /// </summary>
    private static BatchManifest BuildManifest(PipelineContext context)
    {
        IReadOnlyList<ManifestImageRow> rows = context.LambdaRecords
            .Select(ToManifestRow)
            .ToList();

        return new BatchManifest
        {
            JobID     = context.JobID,
            Summary   = new BatchManifestSummary
            {
                ImageCount     = context.ImageRecords.Count,
                ExcelCount     = context.ExcelRecords.Count,
                ZipCount       = context.ZipFileRecords.Count,
                OkRenamed      = context.OkRenamedCount,
                KoRecords      = context.KoRecordCount,
                OkTransformed  = context.OkTransformedCount,
                KoTransformed  = 0,
                GeneratedCount = context.GeneratedCount
            },
            ImageRows      = rows,
            RouteSummaries = Pipeline.StageOrder.Select(stage => $"{stage}: completed.").ToArray(),
            Warnings       = context.Warnings
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
