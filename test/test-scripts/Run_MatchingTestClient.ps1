param(
    [switch]$SkipClassification
)
$ErrorActionPreference = "Stop"

$repoRoot    = (Resolve-Path "$PSScriptRoot/../..").Path
$datasets    = Join-Path $repoRoot "test\datasets"
$project     = Join-Path $repoRoot "test\MatchingTestClient"
$logFile     = Join-Path $repoRoot "matching-testlogs.txt"
$classifyTag = if ($SkipClassification) { "match-only|skip-classification" } else { "match-only" }

$folders = Get-ChildItem $datasets -Directory | Sort-Object Name
Write-Host "Datasets : $($folders.Count)  |  $classifyTag"
Write-Host ""

foreach ($dir in $folders) {
    Write-Host "--- $($dir.Name) ---"

    $runArgs = @("run", "--project", $project, "--no-build", "--", "--folder", $dir.FullName)
    if ($SkipClassification) { $runArgs += "--skip-classification" }

    Push-Location $repoRoot
    try {
        $output = & dotnet @runArgs 2>&1
    } finally {
        Pop-Location
    }

    $output | ForEach-Object { Write-Host $_ }

    # Parse "Summary: X/Y OK (Z%)  [Ns]" from client output
    $summaryLine = $output | Where-Object { $_ -match "^Summary:" } | Select-Object -Last 1
    if ($summaryLine -match "Summary:\s*(\d+)/(\d+)\s*OK\s*\(([0-9.]+)%\)\s*\[([0-9.]+)s\]") {
        $ok      = $Matches[1]
        $total   = $Matches[2]
        $pct     = $Matches[3]
        $elapsed = $Matches[4]
        $ko      = [int]$total - [int]$ok
        $date    = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        $entry   = "$date | $($dir.Name) | $classifyTag | images=$total ok=$ok ko=$ko | OK rate: $pct% | duration: ${elapsed}s"
        Add-Content -Path $logFile -Value $entry
        Write-Host "  -> logged"
    } else {
        $date  = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        $entry = "$date | $($dir.Name) | $classifyTag | FAILED or no summary"
        Add-Content -Path $logFile -Value $entry
        Write-Host "  -> logged (no summary)"
    }

    Write-Host ""
}

Write-Host "Done. Log: $logFile"
