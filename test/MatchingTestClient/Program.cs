using System.Text.Json;
using Prism.Core;

// ── arg parsing ──────────────────────────────────────────────────────────────
string? folderArg = null;
string? allRootArg = null;
bool runAll = false;
bool skipClassification = false;
bool verbose = false;

for (int i = 0; i < args.Length; i++) {
    switch (args[i]) {
        case "--folder" when i + 1 < args.Length:
            folderArg = args[++i];
            break;
        case "--all":
            runAll = true;
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                allRootArg = args[++i];
            break;
        case "--skip-classification":
            skipClassification = true;
            break;
        case "--verbose":
            verbose = true;
            break;
        default:
            Console.Error.WriteLine($"Unknown arg: {args[i]}");
            Console.Error.WriteLine("Usage: MatchingTestClient [--folder <path>] [--all [<root>]] [--skip-classification] [--verbose]");
            return 1;
    }
}

string defaultDatasetRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "test", "datasets"));

if (runAll) {
    string root = allRootArg is not null ? Path.GetFullPath(allRootArg) : defaultDatasetRoot;
    if (!Directory.Exists(root)) {
        Console.Error.WriteLine($"Root not found: {root}");
        return 1;
    }

    int aggregateOk = 0, aggregateTotal = 0, aggregateNotInCatalog = 0;
    List<string> summaryLines = [];

    foreach (string dataset in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase)) {
        DatasetResult? result = await RunDataset(dataset, skipClassification, verbose);
        if (result is null)
            continue;

        aggregateOk += result.OkCount;
        aggregateTotal += result.TotalCount;
        aggregateNotInCatalog += result.NotInCatalogCount;
        summaryLines.Add(FormatSummaryLine(Path.GetFileName(dataset), result));
    }

    Console.WriteLine();
    Console.WriteLine("──────────────────────── batch summary ────────────────────────");
    foreach (string line in summaryLines)
        Console.WriteLine(line);

    int matchable = aggregateTotal - aggregateNotInCatalog;
    double aggregatePct = aggregateTotal == 0 ? 0 : aggregateOk * 100.0 / aggregateTotal;
    double matchablePct = matchable == 0 ? 0 : aggregateOk * 100.0 / matchable;
    Console.WriteLine();
    Console.WriteLine($"TOTAL: {aggregateOk}/{aggregateTotal} OK ({aggregatePct:F1}%) — matchable-OK {aggregateOk}/{matchable} ({matchablePct:F1}%), NOT_IN_CATALOG {aggregateNotInCatalog}");
    return 0;
}

string folder = folderArg is not null ? Path.GetFullPath(folderArg) : defaultDatasetRoot;

if (!Directory.Exists(folder)) {
    Console.Error.WriteLine($"Folder not found: {folder}");
    return 1;
}

DatasetResult? single = await RunDataset(folder, skipClassification, verbose);
return single is null ? 1 : 0;

// ── per-dataset runner ────────────────────────────────────────────────────────
static async Task<DatasetResult?> RunDataset(string folder, bool skipClassification, bool verbose) {
    string[] imageExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    string[] excelExtensions = [".xlsx", ".xls"];

    var images = Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
        .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
        .OrderBy(f => f)
        .ToList();

    var excels = Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly)
        .Where(f => excelExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
        .Where(f => !Path.GetFileName(f).StartsWith("~$")) // skip Excel owner-lock temp files (workbook open)
        .Where(f => !Path.GetFileName(f).StartsWith("matched_", StringComparison.OrdinalIgnoreCase)) // gold outputs, not inputs
        .OrderBy(f => f)
        .ToList();

    // ── unpack ZIPs using PRISM's own extraction logic ────────────────────────
    string tempDir = Path.Combine(Path.GetTempPath(), "prism-mtc", Guid.NewGuid().ToString("N"));
    try {
        var zips = Directory.GetFiles(folder, "*.zip", SearchOption.TopDirectoryOnly).OrderBy(f => f).ToList();
        foreach (string zipPath in zips) {
            ZipExtractionResult extracted = ZipHandler.ExtractProcessableMembers(zipPath, tempDir);
            foreach (ZipExtractedMember member in extracted.ExtractedMembers) {
                if (member.MediaKind == ZipMemberMediaKind.Image)
                    images.Add(member.ExtractedFilePath);
                else if (member.MediaKind == ZipMemberMediaKind.Excel)
                    excels.Add(member.ExtractedFilePath);
            }
        }
        images.Sort();

        if (images.Count == 0) {
            Console.Error.WriteLine($"No images found in: {folder}");
            return null;
        }

        if (excels.Count == 0) {
            Console.Error.WriteLine($"No Excel files found in: {folder}");
            return null;
        }

        Console.WriteLine($"Folder  : {folder}");
        Console.WriteLine($"Images  : {images.Count}");
        Console.WriteLine($"Excel   : {excels.Count}");
        Console.WriteLine($"Classify: {(skipClassification ? "skip" : "run")}");
        Console.WriteLine();

        // ── build request ─────────────────────────────────────────────────────
        var request = new PrismJobRequest {
            JobID = Guid.NewGuid(),
            ImageRecords = images.Select(p => new ImageRecord_INPUT {
                InitialFullName = p,
                SourceKind      = ImageSourceKind.LocalPath,
                ImportStatus    = ImportStatus.Pending
            }).ToList(),
            ExcelRecords = excels.Select(p => new InputExcelFileRecord {
                SourceReference = p
            }).ToList(),
            PrismProcessingParameters = new PrismProcessingParameters {
                SkipClassification = skipClassification,
                Transform          = false,
                Generation         = false,
                Format             = "json",
                Rename             = true
            }
        };

        // ── run pipeline ──────────────────────────────────────────────────────
        var service = new PrismService();
        var sw      = System.Diagnostics.Stopwatch.StartNew();

        var result = await service.Process(request, ev => {
            if (verbose && !string.IsNullOrWhiteSpace(ev.SafeMessage))
                Console.WriteLine($"  [{ev.Stage,-14}] {ev.SafeMessage}");
            return Task.CompletedTask;
        });

        sw.Stop();

        // ── print results ─────────────────────────────────────────────────────
        if (result.Status == "Failed") {
            Console.Error.WriteLine($"Pipeline failed: {result.FailureReason}");
            return null;
        }

        foreach (var row in result.Manifest.ImageRows.OrderBy(r => r.SourceReference)) {
            string src = Path.GetFileName(row.SourceReference);
            if (row.Status == "Ok") {
                Console.WriteLine($"  OK  {src,-45}  {row.FamilyId}  det{row.DetOrder}  [{row.MatchedBy}]");
            } else {
                Console.WriteLine($"  KO  {src,-45}  {row.KoReasonCode} — {row.KoSafeMessage}");
            }
        }

        Console.WriteLine();

        int ok    = result.Manifest.ImageRows.Count(r => r.Status == "Ok");
        int total = result.Manifest.ImageRows.Count;
        double pct = total == 0 ? 0 : ok * 100.0 / total;
        Console.WriteLine($"Summary: {ok}/{total} OK ({pct:F1}%)  [{sw.Elapsed.TotalSeconds:F1}s]");

        var matcherHistogram = result.Manifest.ImageRows
            .Where(r => r.Status == "Ok" && r.MatchedBy is not null)
            .GroupBy(r => r.MatchedBy!)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count());

        var koHistogram = result.Manifest.ImageRows
            .Where(r => r.Status == "Ko")
            .GroupBy(r => r.KoReasonCode ?? "UNKNOWN")
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count());

        if (matcherHistogram.Count > 0)
            Console.WriteLine("  Brackets: " + string.Join("  ", matcherHistogram.Select(kv => $"{kv.Key}={kv.Value}")));
        if (koHistogram.Count > 0)
            Console.WriteLine("  KO      : " + string.Join("  ", koHistogram.Select(kv => $"{kv.Key}={kv.Value}")));

        foreach (var w in result.Manifest.Warnings)
            Console.WriteLine($"  WARN: {w}");

        if (verbose) {
            Console.WriteLine();
            Console.WriteLine(JsonSerializer.Serialize(result.Manifest, new JsonSerializerOptions { WriteIndented = true }));
        }

        return new DatasetResult {
            OkCount           = ok,
            TotalCount        = total,
            NotInCatalogCount = koHistogram.TryGetValue("NOT_IN_CATALOG", out int nic) ? nic : 0,
            MatcherHistogram  = matcherHistogram,
            KoHistogram       = koHistogram
        };

    } finally {
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
    }
}

static string FormatSummaryLine(string name, DatasetResult result) {
    int matchable = result.TotalCount - result.NotInCatalogCount;
    double pct = result.TotalCount == 0 ? 0 : result.OkCount * 100.0 / result.TotalCount;
    double matchablePct = matchable == 0 ? 0 : result.OkCount * 100.0 / matchable;
    return $"{name,-14} {result.OkCount,5}/{result.TotalCount,-5} OK ({pct,5:F1}%)  matchable {matchablePct,5:F1}%  " +
           string.Join("  ", result.KoHistogram.Select(kv => $"{kv.Key}={kv.Value}"));
}

/// <summary>Per-dataset outcome counters for the --all batch summary.</summary>
internal sealed record DatasetResult {
    public required int OkCount { get; init; }
    public required int TotalCount { get; init; }
    public required int NotInCatalogCount { get; init; }
    public required Dictionary<string, int> MatcherHistogram { get; init; }
    public required Dictionary<string, int> KoHistogram { get; init; }
}
