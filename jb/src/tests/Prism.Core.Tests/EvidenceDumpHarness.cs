using System.Text.Json;
using Xunit;

namespace PrismCoreTests;

/// <summary>
/// TEMPORARY report harness — not a regression test. Runs Import → Match (classify, match, order,
/// rename) in-process for the four report datasets and dumps full per-image evidence (CLIP tags,
/// feature snapshot, phenotype, MatchEvidence, OrderEvidence) as JSON. Output dir comes from the
/// PRISM_EVIDENCE_OUT environment variable; the test is a no-op when it is not set.
/// Delete this file after the report run.
/// </summary>
public sealed class EvidenceDumpHarness {
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".tif", ".tiff", ".pdf", ".webp", ".bmp", ".gif"];
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [Fact]
    public async Task DumpEvidenceForReportDatasets() {
        string? outDir = Environment.GetEnvironmentVariable("PRISM_EVIDENCE_OUT");
        if (string.IsNullOrEmpty(outDir)) return;
        Directory.CreateDirectory(outDir);

        string fixtureRoot = PipelineFixture.ResolveTestFixturePath();
        (PrismConfiguration config, ModelBuilder modelBuilder) = LoadConfig();
        var tempCopies = new List<string>();
        var pipeline = new Pipeline(config, modelBuilder);
        try {
            foreach (string dataset in new[] { "CiMini", "TinyTest", "DEWITTE71", "SPACINI29" }) {
                PrismJobRequest request = BuildRequest(Path.Combine(fixtureRoot, dataset), tempCopies);
                IngestResult ingest = await pipeline.ImportAsync(request, null, CancellationToken.None);
                MatchingResult matched = await pipeline.MatchAsync(ingest, null, CancellationToken.None);
                string json = JsonSerializer.Serialize(Project(dataset, ingest, matched), JsonOptions);
                File.WriteAllText(Path.Combine(outDir, $"{dataset}-evidence.json"), json);
            }
        } finally {
            pipeline.Dispose();
            foreach (string path in tempCopies) { if (File.Exists(path)) File.Delete(path); }
        }
    }

    private static (PrismConfiguration, ModelBuilder) LoadConfig() {
        string configPath = PrismConfigLocator.FindPrismConfigPath() ?? throw new InvalidOperationException("Prism_Config.json not found");
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

    private static object Project(string dataset, IngestResult ingest, MatchingResult matched) {
        return new {
            Dataset = dataset,
            Import = new {
                ingest.OriginalImageCount,
                ingest.OriginalExcelCount,
                ingest.OriginalZipCount,
                NormalizedImageCount = ingest.NormalizedImages.Count,
                ingest.KoRecordCount,
                ingest.Warnings,
                FamilyRecordCount = ingest.FamilyRecords.Count,
                FamilyIds = ingest.FamilyRecords.Select(f => f.FamilyID).ToArray(),
                ImportedImages = ingest.NormalizedImages.Select(i => new {
                    i.InitialFullName, i.Width, i.Height,
                    ImportStatus = i.ImportStatus.ToString(),
                    i.KoReasonCode, i.KoSafeMessage
                }).ToArray()
            },
            Matching = new {
                matched.OkRenamedCount,
                matched.KoRecordCount,
                matched.DuplicatesRemoved,
                matched.PhenotypeAssignedCount,
                matched.Warnings
            },
            Images = matched.LambdaRecords.Select(l => new {
                l.InitialFullName,
                l.MatchingAlias,
                l.Width, l.Height,
                l.IsKo, l.KoReasonCode, l.KoSafeMessage,
                Family = string.IsNullOrEmpty(l.Family) ? null : l.Family,
                DetOrder = l.IsKo ? (int?)null : l.DetOrder,
                NewName = l.IsKo || string.IsNullOrEmpty(l.Family) ? null : l.NewName,
                l.SelectedPhenotype,
                l.CandidatePhenotypes,
                l.ProductTypeId,
                Features = l.Features.All.ToDictionary(kv => kv.Key, kv => new { kv.Value.Value, kv.Value.Confidence, kv.Value.Source }),
                TagsInfluential = l.Tags.Influential.Select(t => new { t.Label, t.Confidence, t.Feature, t.Value }).ToArray(),
                TagsTrivialTop = l.Tags.Trivial.OrderByDescending(t => t.Confidence).Take(8)
                    .Select(t => new { t.Label, t.Confidence, t.Feature, t.Value }).ToArray(),
                l.MatchEvidence,
                l.OrderEvidence
            }).ToArray()
        };
    }
}
