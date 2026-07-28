using System.Text.Json;
using Xunit;

namespace PrismCoreTests;

/// <summary>
/// Ad-hoc evidence dump harness for pipeline reports — copied in from
/// .claude/skills/prism-evidence-report/harness/, run once via dotnet test, then DELETED.
/// No-op unless PRISM_EVIDENCE_OUT is set. Env vars:
///   PRISM_EVIDENCE_OUT       output directory (required)
///   PRISM_EVIDENCE_DATASETS  comma-separated test/datasets folder names (default: CiMini)
///   PRISM_EVIDENCE_SECTIONS  comma-separated: import,tags,features,phenotype,match,order,transform
///                            (default: all except transform — transform also runs Generate+Transform stages, slow)
/// Writes one {Dataset}-evidence.json per dataset containing only the requested sections.
/// </summary>
public sealed class EvidenceDumpHarness {
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".tif", ".tiff", ".pdf", ".webp", ".bmp", ".gif"];
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [Fact]
    public async Task DumpRequestedEvidence() {
        string? outDir = Environment.GetEnvironmentVariable("PRISM_EVIDENCE_OUT");
        if (string.IsNullOrEmpty(outDir)) return;
        Directory.CreateDirectory(outDir);

        string[] datasets = (Environment.GetEnvironmentVariable("PRISM_EVIDENCE_DATASETS") ?? "CiMini")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        HashSet<string> sections = (Environment.GetEnvironmentVariable("PRISM_EVIDENCE_SECTIONS") ?? "import,tags,features,phenotype,match,order")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string fixtureRoot = PipelineFixture.ResolveTestFixturePath();
        (PrismConfiguration config, ModelBuilder modelBuilder) = LoadConfig();
        var tempCopies = new List<string>();
        var pipeline = new Pipeline(config, modelBuilder);
        try {
            foreach (string dataset in datasets) {
                PrismJobRequest request = BuildRequest(Path.Combine(fixtureRoot, dataset), tempCopies);
                IngestResult ingest = await pipeline.ImportAsync(request, null, CancellationToken.None);
                MatchingResult matched = await pipeline.MatchAsync(ingest, null, CancellationToken.None);
                IReadOnlyList<ImageRecord_GENERATED> generated = [];
                if (sections.Contains("transform")) {
                    (matched, generated) = await pipeline.GenerateAsync(matched, true, null, CancellationToken.None);
                    await pipeline.TransformAsync(matched, true, false, null, CancellationToken.None);
                }
                string json = JsonSerializer.Serialize(Project(dataset, ingest, matched, generated, sections), JsonOptions);
                File.WriteAllText(Path.Combine(outDir, $"{dataset}-evidence.json"), json);
            }
        } finally {
            pipeline.Dispose();
            foreach (string path in tempCopies) { if (File.Exists(path)) File.Delete(path); }
        }
    }

    private static (PrismConfiguration, ModelBuilder) LoadConfig() {
        // PrismConfigLocator/ConfigCache were deleted by T-4560 — ConfigLoader is the only resolver now.
        string configPath = ConfigLoader.RequireFile(PrismConfiguration.FileName);
        PrismConfiguration config = PrismConfiguration.LoadPrismConfig(configPath);
        string coreDir = Path.GetDirectoryName(configPath)!;
        return (config, ModelBuilder.FromConfigFile(Path.Combine(coreDir, "ExcelConfig.json")));
    }

    private static PrismJobRequest BuildRequest(string datasetDir, List<string> tempCopies) {
        var images = new List<ImageRecord_INPUT>();
        var excels = new List<InputExcelFileRecord>();
        var zips = new List<InputZipFileRecord>();

        foreach (string file in Directory.GetFiles(datasetDir, "*", SearchOption.AllDirectories)) {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            string name = Path.GetFileName(file);
            long length = new FileInfo(file).Length;
            if (ImageExtensions.Contains(ext)) {
                images.Add(new ImageRecord_INPUT { InitialFullName = name, TempFilePath = file });
            } else if (ext == ".xlsx") {
                // copy so an Excel instance holding the original open cannot block the Importer
                string tempPath = Path.Combine(Path.GetTempPath(), $"PRISM-EVIDENCE-{Guid.NewGuid():N}.xlsx");
                File.Copy(file, tempPath, overwrite: true);
                tempCopies.Add(tempPath);
                excels.Add(new InputExcelFileRecord { SourceReference = name, ByteLength = length, TempFilePath = tempPath });
            } else if (ext == ".zip") {
                zips.Add(new InputZipFileRecord { SourceReference = name, ByteLength = length, TempFilePath = file });
            }
        }

        return new PrismJobRequest {
            JobID = Guid.NewGuid(),
            ClientRequestToken = $"evidence-{Path.GetFileName(datasetDir)}",
            ImageRecords = images,
            ExcelRecords = excels,
            ZipFileRecords = zips,
            PrismProcessingParameters = new PrismProcessingParameters {
                Rename = true, Transform = false, Generation = false, SkipClassification = false, Format = "json"
            }
        };
    }

    private static object Project(string dataset, IngestResult ingest, MatchingResult matched, IReadOnlyList<ImageRecord_GENERATED> generated, HashSet<string> sections) {
        var root = new Dictionary<string, object?> {
            ["Dataset"] = dataset,
            ["Matching"] = new {
                matched.OkRenamedCount, matched.KoRecordCount, matched.DuplicatesRemoved,
                matched.PhenotypeAssignedCount, matched.Warnings
            }
        };
        if (sections.Contains("import")) {
            root["Import"] = new {
                ingest.OriginalImageCount, ingest.OriginalExcelCount, ingest.OriginalZipCount,
                NormalizedImageCount = ingest.NormalizedImages.Count,
                ingest.KoRecordCount, ingest.Warnings,
                FamilyRecordCount = ingest.FamilyRecords.Count,
                FamilyIds = ingest.FamilyRecords.Select(f => f.FamilyID).ToArray(),
                ImportedImages = ingest.NormalizedImages.Select(i => new {
                    i.InitialFullName, i.Width, i.Height,
                    ImportStatus = i.ImportStatus.ToString(),
                    i.KoReasonCode, i.KoSafeMessage
                }).ToArray()
            };
        }
        if (sections.Contains("transform")) root["GeneratedCount"] = generated.Count;

        root["Images"] = matched.LambdaRecords.Select(l => {
            var img = new Dictionary<string, object?> {
                ["Name"] = l.InitialFullName,
                ["Width"] = l.Width, ["Height"] = l.Height,
                ["IsKo"] = l.IsKo, ["KoReasonCode"] = l.KoReasonCode, ["KoSafeMessage"] = l.KoSafeMessage,
                ["Family"] = string.IsNullOrEmpty(l.Family) ? null : l.Family
            };
            if (sections.Contains("tags")) {
                img["TagsInfluential"] = l.Tags.Influential.Select(t => new { t.Label, t.Confidence, t.Feature, t.Value }).ToArray();
                img["TagsTrivialTop"] = l.Tags.Trivial.OrderByDescending(t => t.Confidence).Take(8)
                    .Select(t => new { t.Label, t.Confidence, t.Feature, t.Value }).ToArray();
            }
            if (sections.Contains("features")) {
                img["Features"] = l.Features.All.ToDictionary(kv => kv.Key, kv => new { kv.Value.Value, kv.Value.Confidence, kv.Value.Source });
            }
            if (sections.Contains("phenotype")) {
                img["SelectedPhenotype"] = l.SelectedPhenotype;
                img["CandidatePhenotypes"] = l.CandidatePhenotypes;
            }
            if (sections.Contains("match")) {
                img["MatchingAlias"] = l.MatchingAlias;
                img["MatchEvidence"] = l.MatchEvidence;
            }
            if (sections.Contains("order")) {
                img["DetOrder"] = l.IsKo ? null : (int?)l.DetOrder;
                img["NewName"] = l.IsKo || string.IsNullOrEmpty(l.Family) ? null : l.NewName;
                img["OrderEvidence"] = l.OrderEvidence;
                img["ProductTypeId"] = l.ProductTypeId;
            }
            if (sections.Contains("transform")) {
                img["GenerationRouteState"] = l.GenerationRouteState.ToString();
                img["TransformStatus"] = l.OutputRecord?.TransformStatus?.ToString();
                img["OutputSize"] = l.OutputRecord is null ? null : $"{l.OutputRecord.OutputWidth}x{l.OutputRecord.OutputHeight}";

                // Subject-isolation evidence (T-4800). Both boxes are dumped so the promoted subject box
                // can be compared against the salient box it replaced without a second run. MaskPng is
                // deliberately excluded — it is a full-resolution PNG per image and would bloat the dump.
                img["SubjectProducer"] = l.Subject?.Producer;
                img["SubjectConfidence"] = l.Subject?.Confidence;
                img["SubjectBox"] = l.Subject is null ? null : BoxOf(l.Subject.Box);
                img["SubjectIsWholeFrameFallback"] = l.Subject?.IsWholeFrameFallback;
                img["SubjectHasHardShadowEvidence"] = l.Subject?.HasHardShadowEvidence;
                img["SubjectHardShadowStrippedFraction"] = l.Subject?.HardShadowStrippedFraction;
                img["SubjectIntersects"] = l.Subject is null ? null
                    : $"{(l.Subject.IntersectsTop ? "T" : "-")}{(l.Subject.IntersectsBottom ? "B" : "-")}{(l.Subject.IntersectsLeft ? "L" : "-")}{(l.Subject.IntersectsRight ? "R" : "-")}";
                img["LegacySalientBox"] = l.LegacySalientBox is null ? null : BoxOf(l.LegacySalientBox.Value);
                img["PromotedSubjectGeometry"] = l.LegacySalientBox is not null;
                img["FinalBoundingBox"] = l.BoundingBox is null ? null : BoxOf(l.BoundingBox.Value);
                img["ShadowPresentFeature"] = l.Features.GetValue("shadow-present");
                img["BackgroundTypeFeature"] = l.Features.GetValue("background-type");
                img["ProductColorFeature"] = l.Features.GetValue("product-color");
                img["BackgroundColorFeature"] = l.Features.GetValue("background-color");
                img["SafeSummaryText"] = l.OutputRecord?.SafeSummaryText;
            }
            return img;
        }).ToArray();
        return root;
    }

    private static object BoxOf(BoundingBox b) => new { b.X, b.Y, b.Width, b.Height, b.Left, b.Top, b.Right, b.Bottom };
}
