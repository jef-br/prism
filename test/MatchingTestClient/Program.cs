using System.Text.Json;
using Prism.Core;

// ── arg parsing ──────────────────────────────────────────────────────────────
string? folderArg = null;
bool skipClassification = false;
bool verbose = false;

for (int i = 0; i < args.Length; i++) {
    switch (args[i]) {
        case "--folder" when i + 1 < args.Length:
            folderArg = args[++i];
            break;
        case "--skip-classification":
            skipClassification = true;
            break;
        case "--verbose":
            verbose = true;
            break;
        default:
            Console.Error.WriteLine($"Unknown arg: {args[i]}");
            Console.Error.WriteLine("Usage: MatchingTestClient [--folder <path>] [--skip-classification] [--verbose]");
            return 1;
    }
}

string folder = folderArg is not null
    ? Path.GetFullPath(folderArg)
    : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "test", "datasets"));

if (!Directory.Exists(folder)) {
    Console.Error.WriteLine($"Folder not found: {folder}");
    return 1;
}

// ── discover inputs ───────────────────────────────────────────────────────────
string[] imageExtensions = [".jpg", ".jpeg", ".png", ".webp"];
string[] excelExtensions = [".xlsx", ".xls"];

var images = Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
    .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
    .OrderBy(f => f)
    .ToList();

var excels = Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly)
    .Where(f => excelExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
    .OrderBy(f => f)
    .ToList();

// ── unpack ZIPs using PRISM's own extraction logic ────────────────────────────
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
        return 1;
    }

    if (excels.Count == 0) {
        Console.Error.WriteLine($"No Excel files found in: {folder}");
        return 1;
    }

    Console.WriteLine($"Folder  : {folder}");
    Console.WriteLine($"Images  : {images.Count}");
    Console.WriteLine($"Excel   : {excels.Count}");
    Console.WriteLine($"Classify: {(skipClassification ? "skip" : "run")}");
    Console.WriteLine();

    // ── build request ─────────────────────────────────────────────────────────
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

    // ── run pipeline ──────────────────────────────────────────────────────────
    var service = new PrismService();
    var sw      = System.Diagnostics.Stopwatch.StartNew();

    var result = await service.Process(request, ev => {
        if (!string.IsNullOrWhiteSpace(ev.SafeMessage))
            Console.WriteLine($"  [{ev.Stage,-14}] {ev.SafeMessage}");
        return Task.CompletedTask;
    });

    sw.Stop();
    Console.WriteLine();

    // ── print results ─────────────────────────────────────────────────────────
    if (result.Status == "Failed") {
        Console.Error.WriteLine($"Pipeline failed: {result.FailureReason}");
        return 2;
    }

    foreach (var row in result.Manifest.ImageRows.OrderBy(r => r.SourceReference)) {
        string src = Path.GetFileName(row.SourceReference);
        if (row.Status == "Ok") {
            Console.WriteLine($"  OK  {src,-45}  {row.FamilyId}  det{row.DetOrder}");
        } else {
            Console.WriteLine($"  KO  {src,-45}  {row.KoReasonCode} — {row.KoSafeMessage}");
        }
    }

    Console.WriteLine();

    int ok    = result.Manifest.ImageRows.Count(r => r.Status == "Ok");
    int total = result.Manifest.ImageRows.Count;
    double pct = total == 0 ? 0 : ok * 100.0 / total;
    Console.WriteLine($"Summary: {ok}/{total} OK ({pct:F1}%)  [{sw.Elapsed.TotalSeconds:F1}s]");

    foreach (var w in result.Manifest.Warnings)
        Console.WriteLine($"  WARN: {w}");

    if (verbose) {
        Console.WriteLine();
        Console.WriteLine(JsonSerializer.Serialize(result.Manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    return 0;

} finally {
    if (Directory.Exists(tempDir))
        Directory.Delete(tempDir, recursive: true);
}
